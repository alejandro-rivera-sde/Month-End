using Close_Portal.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Services;

namespace Close_Portal.Pages.Admin {

    // ── ViewModel ──────────────────────────────────────────────────────────
    public class LocationViewModel {
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public bool Active { get; set; }
        public int UserCount { get; set; }

        public string StatusLabel => Active ? "Activa" : "Inactiva";
        public string StatusBadge => Active ? "success" : "secondary";
    }

    public partial class WarehouseManagement : SecurePage {
        protected override int RequiredRoleId => RoleLevel.Administrador;

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        // ============================================================
        // PAGE LOAD
        // ============================================================
        protected void Page_Load(object sender, EventArgs e) {
            if (!IsPostBack) {
                LoadStats();
                LoadLocations();
            }
        }

        // ============================================================
        // LOAD STATS
        // ============================================================
        private void LoadStats() {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    string sql = @"
                        SELECT
                            COUNT(*)                                          AS Total,
                            SUM(CASE WHEN Active = 1 THEN 1 ELSE 0 END)      AS Activas,
                            SUM(CASE WHEN Active = 0 THEN 1 ELSE 0 END)      AS Inactivas
                        FROM MonthEnd_Locations";

                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            if (r.Read()) {
                                litTotal.Text = r["Total"].ToString();
                                litActive.Text = r["Activas"].ToString();
                                litInactive.Text = r["Inactivas"].ToString();
                            }
                        }
                    }

                    string sqlUnassigned = @"
                        SELECT COUNT(*)
                        FROM MonthEnd_Locations wl
                        WHERE NOT EXISTS (
                            SELECT 1 FROM MonthEnd_Users_Location ul
                            WHERE ul.Location_Id = wl.Location_Id
                        )";
                    using (SqlCommand cmd = new SqlCommand(sqlUnassigned, conn)) {
                        litUnassigned.Text = cmd.ExecuteScalar().ToString();
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadStats: {ex.Message}");
            }
        }

        // ============================================================
        // LOAD LOCATIONS TABLE
        // ============================================================
        private void LoadLocations() {
            try {
                string sql = @"
                    SELECT
                        wl.Location_Id,
                        wl.Location_Name,
                        wl.Active,
                        (SELECT COUNT(*) FROM MonthEnd_Users_Location ul
                         WHERE ul.Location_Id = wl.Location_Id) AS UserCount
                    FROM MonthEnd_Locations wl
                    ORDER BY wl.Location_Name";

                var list = new List<LocationViewModel>();

                using (SqlConnection conn = new SqlConnection(_connStr))
                using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            list.Add(new LocationViewModel {
                                LocationId = (int)r["Location_Id"],
                                LocationName = r["Location_Name"].ToString(),
                                Active = (bool)r["Active"],
                                UserCount = (int)r["UserCount"]
                            });
                        }
                    }
                }

                pnlEmpty.Visible = list.Count == 0;
                rptLocations.DataSource = list;
                rptLocations.DataBind();

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadLocations: {ex.Message}");
            }
        }

        // ============================================================
        // WEBMETHOD — Detalle de locación para editar
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetLocationDetail(int locationId) {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT Location_Id, Location_Name, Active
                        FROM MonthEnd_Locations
                        WHERE Location_Id = @LocationId";

                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            if (!r.Read())
                                return new { Success = false, Message = "Locación no encontrada" };

                            return new {
                                Success = true,
                                LocationId = locationId,
                                LocationName = r["Location_Name"].ToString(),
                                Active = (bool)r["Active"]
                            };
                        }
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetLocationDetail: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — Guardar locación (INSERT o UPDATE)
        // locationId = 0 → INSERT  |  locationId > 0 → UPDATE
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SaveLocation(int locationId, string locationName, bool active) {
            try {
                if (string.IsNullOrWhiteSpace(locationName))
                    return new { Success = false, Message = "El nombre de la locación es requerido" };

                locationName = locationName.Trim();

                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction()) {
                        try {
                            if (locationId == 0) {
                                string sqlIns = @"
                                    INSERT INTO MonthEnd_Locations (Location_Name, Active)
                                    VALUES (@LocationName, @Active);
                                    SELECT SCOPE_IDENTITY();";

                                using (SqlCommand cmd = new SqlCommand(sqlIns, conn, tx)) {
                                    cmd.Parameters.AddWithValue("@LocationName", locationName);
                                    cmd.Parameters.AddWithValue("@Active", active);
                                    locationId = Convert.ToInt32(cmd.ExecuteScalar());
                                }
                            } else {
                                string sqlUpd = @"
                                    UPDATE MonthEnd_Locations
                                    SET Location_Name = @LocationName,
                                        Active        = @Active
                                    WHERE Location_Id = @LocationId";

                                using (SqlCommand cmd = new SqlCommand(sqlUpd, conn, tx)) {
                                    cmd.Parameters.AddWithValue("@LocationName", locationName);
                                    cmd.Parameters.AddWithValue("@Active", active);
                                    cmd.Parameters.AddWithValue("@LocationId", locationId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();
                            System.Diagnostics.Debug.WriteLine($"✓ SaveLocation LocationId={locationId}");
                            return new {
                                Success = true,
                                Message = locationId == 0
                                    ? "Locación creada correctamente"
                                    : "Locación actualizada correctamente"
                            };

                        } catch (Exception inner) {
                            tx.Rollback();
                            System.Diagnostics.Debug.WriteLine($"ERROR SaveLocation TX: {inner.Message}");
                            return new { Success = false, Message = inner.Message };
                        }
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR SaveLocation: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — Activar / Desactivar locación
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object ToggleLocationActive(int locationId, bool active) {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = "UPDATE MonthEnd_Locations SET Active = @Active WHERE Location_Id = @LocationId";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@Active", active);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                string msg = active ? "Locación activada" : "Locación desactivada";
                System.Diagnostics.Debug.WriteLine($"✓ ToggleLocationActive: LocationId={locationId} Active={active}");
                return new { Success = true, Message = msg };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR ToggleLocationActive: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }
    }
}