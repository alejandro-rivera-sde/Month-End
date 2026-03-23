using Close_Portal.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Services;

namespace Close_Portal.Pages.Admin {

    // ── ViewModel ──────────────────────────────────────────────────────────
    public class LocationViewModel {
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public bool Active { get; set; }
        public int UserCount { get; set; }
        public string OmsTagsHtml { get; set; }

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
                        FROM WMS_Location";

                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            if (r.Read()) {
                                litTotal.Text = r["Total"].ToString();
                                litActive.Text = r["Activas"].ToString();
                                litInactive.Text = r["Inactivas"].ToString();
                            }
                        }
                    }

                    string sqlUsers = "SELECT COUNT(DISTINCT User_Id) FROM Users_Location";
                    using (SqlCommand cmd = new SqlCommand(sqlUsers, conn)) {
                        litUsersAssigned.Text = cmd.ExecuteScalar().ToString();
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadStats: {ex.Message}");
            }
        }

        // ============================================================
        // LOAD LOCATIONS TABLE
        // LEFT JOIN a Location_OMS → OMS para construir OmsTagsHtml
        // ============================================================
        private void LoadLocations() {
            try {
                string sql = @"
                    SELECT
                        wl.Location_Id,
                        wl.Location_Name,
                        wl.Active,
                        o.OMS_Id,
                        o.OMS_Code,
                        (SELECT COUNT(*) FROM Users_Location ul
                         WHERE ul.Location_Id = wl.Location_Id) AS UserCount
                    FROM WMS_Location wl
                    LEFT JOIN Location_OMS  lo ON lo.Location_Id = wl.Location_Id
                    LEFT JOIN OMS           o  ON o.OMS_Id       = lo.OMS_Id
                    ORDER BY wl.Location_Name, o.OMS_Code";

                var viewModels = new Dictionary<int, LocationViewModel>();
                var omsTags = new Dictionary<int, List<string>>();

                using (SqlConnection conn = new SqlConnection(_connStr))
                using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            int locId = (int)r["Location_Id"];

                            if (!viewModels.ContainsKey(locId)) {
                                viewModels[locId] = new LocationViewModel {
                                    LocationId = locId,
                                    LocationName = r["Location_Name"].ToString(),
                                    Active = (bool)r["Active"],
                                    UserCount = (int)r["UserCount"]
                                };
                                omsTags[locId] = new List<string>();
                            }

                            if (r["OMS_Id"] != DBNull.Value) {
                                string code = r["OMS_Code"].ToString();
                                omsTags[locId].Add($"<span class='code-tag'>{code}</span>");
                            }
                        }
                    }
                }

                foreach (var kv in viewModels) {
                    kv.Value.OmsTagsHtml = omsTags[kv.Key].Count > 0
                        ? string.Join(" ", omsTags[kv.Key])
                        : "<span class='no-oms'>—</span>";
                }

                var list = viewModels.Values.ToList();

                pnlEmpty.Visible = list.Count == 0;
                rptLocations.DataSource = list;
                rptLocations.DataBind();

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadLocations: {ex.Message}");
            }
        }

        // ============================================================
        // WEBMETHOD — Lista de OMS para el checklist del modal
        // Agrupados por WMS para mejor legibilidad en el checklist
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetOmsList() {
            try {
                var list = new List<object>();
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT
                            o.OMS_Id,
                            o.OMS_Code,
                            o.OMS_Name,
                            w.WMS_Id,
                            w.WMS_Code,
                            w.WMS_Name
                        FROM OMS o
                        INNER JOIN WMS w ON w.WMS_Id = o.WMS_Id
                        WHERE o.Active = 1 AND w.Active = 1
                        ORDER BY w.WMS_Name, o.OMS_Name";

                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                list.Add(new {
                                    OmsId = (int)r["OMS_Id"],
                                    OmsCode = r["OMS_Code"].ToString(),
                                    OmsName = r["OMS_Name"].ToString(),
                                    WmsId = (int)r["WMS_Id"],
                                    WmsCode = r["WMS_Code"].ToString(),
                                    WmsName = r["WMS_Name"].ToString()
                                });
                            }
                        }
                    }
                }
                return new { Success = true, Data = list };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetOmsList: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — Detalle de locación para editar
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetLocationDetail(int locationId) {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    string locationName = null;
                    bool active = true;

                    string sqlLoc = @"
                        SELECT Location_Id, Location_Name, Active
                        FROM WMS_Location
                        WHERE Location_Id = @LocationId";

                    using (SqlCommand cmd = new SqlCommand(sqlLoc, conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            if (!r.Read())
                                return new { Success = false, Message = "Locación no encontrada" };
                            locationName = r["Location_Name"].ToString();
                            active = (bool)r["Active"];
                        }
                    }

                    var omsIds = new List<int>();
                    string sqlOms = "SELECT OMS_Id FROM Location_OMS WHERE Location_Id = @LocationId";
                    using (SqlCommand cmd = new SqlCommand(sqlOms, conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) omsIds.Add((int)r["OMS_Id"]);
                        }
                    }

                    return new {
                        Success = true,
                        LocationId = locationId,
                        LocationName = locationName,
                        Active = active,
                        OmsIds = omsIds
                    };
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
        public static object SaveLocation(int locationId, string locationName,
                                          bool active, int[] omsIds) {
            try {
                if (string.IsNullOrWhiteSpace(locationName))
                    return new { Success = false, Message = "El nombre de la locación es requerido" };

                locationName = locationName.Trim();
                if (omsIds == null) omsIds = new int[0];

                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction()) {
                        try {
                            if (locationId == 0) {
                                string sqlIns = @"
                                    INSERT INTO WMS_Location (Location_Name, Active)
                                    VALUES (@LocationName, @Active);
                                    SELECT SCOPE_IDENTITY();";

                                using (SqlCommand cmd = new SqlCommand(sqlIns, conn, tx)) {
                                    cmd.Parameters.AddWithValue("@LocationName", locationName);
                                    cmd.Parameters.AddWithValue("@Active", active);
                                    locationId = Convert.ToInt32(cmd.ExecuteScalar());
                                }
                            } else {
                                string sqlUpd = @"
                                    UPDATE WMS_Location
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

                            // ── Sincronizar Location_OMS ──────────────────
                            string sqlDel = "DELETE FROM Location_OMS WHERE Location_Id = @LocationId";
                            using (SqlCommand cmd = new SqlCommand(sqlDel, conn, tx)) {
                                cmd.Parameters.AddWithValue("@LocationId", locationId);
                                cmd.ExecuteNonQuery();
                            }

                            foreach (int oid in omsIds) {
                                string sqlOms = @"
                                    INSERT INTO Location_OMS (Location_Id, OMS_Id)
                                    VALUES (@LocationId, @OmsId)";
                                using (SqlCommand cmd = new SqlCommand(sqlOms, conn, tx)) {
                                    cmd.Parameters.AddWithValue("@LocationId", locationId);
                                    cmd.Parameters.AddWithValue("@OmsId", oid);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();
                            System.Diagnostics.Debug.WriteLine($"✓ SaveLocation LocationId={locationId}");
                            string msg = locationId == 0
                                ? "Locación creada correctamente"
                                : "Locación actualizada correctamente";
                            return new { Success = true, Message = msg };

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
                    string sql = "UPDATE WMS_Location SET Active = @Active WHERE Location_Id = @LocationId";
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