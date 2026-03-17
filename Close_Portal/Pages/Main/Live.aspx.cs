using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.Services;
using Close_Portal.Core;

namespace Close_Portal.Pages.Main {
    public partial class Dashboard : SecurePage {
        protected override int RequiredRoleId => RoleLevel.Regular;

        protected void Page_Load(object sender, EventArgs e) {
            // SecurePage.OnInit ya validó sesión y rol antes de llegar aquí
            System.Diagnostics.Debug.WriteLine("========================================");
            System.Diagnostics.Debug.WriteLine("===== Dashboard.aspx Page_Load =====");
            System.Diagnostics.Debug.WriteLine($"  UserId:   {Session["UserId"]}");
            System.Diagnostics.Debug.WriteLine($"  Email:    {Session["Email"]}");
            System.Diagnostics.Debug.WriteLine($"  Username: {Session["Username"]}");
            System.Diagnostics.Debug.WriteLine($"  RoleName: {Session["RoleName"]}");
            System.Diagnostics.Debug.WriteLine("========================================");
        }

        // ════════════════════════════════════════════════════════════════
        // DTOs INTERNOS
        // ════════════════════════════════════════════════════════════════

        private class LocationDto {
            public int LocationId { get; set; }
            public string LocationName { get; set; }
            public string LocationCode { get; set; }
            public string Status { get; set; } = "Active";
            public int? RequestId { get; set; }
            public string RequestedBy { get; set; } = "";
            public string RequestedAt { get; set; } = "";
            public string ReviewedBy { get; set; } = "";
            public string ReviewedAt { get; set; } = "";
            public string ReviewNotes { get; set; } = "";
        }

        private class GuardSpotDto {
            public string DeptCode { get; set; }
            public string DeptName { get; set; }
            public string Username { get; set; }
        }

        private class GuardDto {
            public bool IsActive { get; set; }
            public int GuardId { get; set; }
            public DateTime StartTime { get; set; }
            public string StartedBy { get; set; }
            public List<GuardSpotDto> Spots { get; set; } = new List<GuardSpotDto>();
        }

        // ════════════════════════════════════════════════════════════════
        // WEBMETHOD — GetDashboardData
        // ════════════════════════════════════════════════════════════════

        [WebMethod(EnableSession = true)]
        public static object GetDashboardData() {
            var session = HttpContext.Current.Session;
            if (session["UserId"] == null)
                return new { success = false, message = "Session expired" };

            SecurePage.CheckAccess(RoleLevel.Regular);

            int userId = Convert.ToInt32(session["UserId"]);
            int roleId = Convert.ToInt32(session["RoleId"]);

            string cs = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
            try {
                using (var conn = new SqlConnection(cs)) {
                    conn.Open();

                    // 1. Guardia activa
                    var guard = GetActiveGuard(conn);

                    // 2. Locaciones visibles para este usuario/rol
                    var locations = GetLocations(conn, userId, roleId);

                    // 3. Enriquecer con solicitudes desde el inicio de la guardia (o hoy)
                    DateTime since = (guard != null) ? guard.StartTime : DateTime.Today;

                    if (locations.Count > 0)
                        EnrichWithRequests(conn, locations, since);

                    // 4. Resumen
                    int total = locations.Count;
                    int active = locations.FindAll(l => l.Status == "Active").Count;
                    int pending = locations.FindAll(l => l.Status == "Pending").Count;
                    int rejected = locations.FindAll(l => l.Status == "Rejected").Count;
                    int approved = locations.FindAll(l => l.Status == "Approved").Count;

                    // 5. Serializar locaciones
                    var locData = new List<object>();
                    foreach (var l in locations) {
                        locData.Add(new {
                            locationId = l.LocationId,
                            locationName = l.LocationName,
                            locationCode = l.LocationCode,
                            status = l.Status,
                            requestId = l.RequestId,
                            requestedBy = l.RequestedBy,
                            requestedAt = l.RequestedAt,
                            reviewedBy = l.ReviewedBy,
                            reviewedAt = l.ReviewedAt,
                            reviewNotes = l.ReviewNotes
                        });
                    }

                    // 6. Serializar guardia
                    object guardData = null;
                    if (guard != null) {
                        var spotList = new List<object>();
                        foreach (var s in guard.Spots)
                            spotList.Add(new {
                                deptCode = s.DeptCode,
                                deptName = s.DeptName,
                                username = s.Username
                            });

                        guardData = new {
                            isActive = true,
                            guardId = guard.GuardId,
                            startTime = guard.StartTime.ToString("dd/MM/yyyy HH:mm"),
                            startedBy = guard.StartedBy,
                            spots = spotList
                        };
                    }

                    return new {
                        success = true,
                        guard = guardData,
                        locations = locData,
                        summary = new { total, active, pending, rejected, approved }
                    };
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("===== Dashboard WebMethod ERROR =====");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return new { success = false, message = ex.Message, detail = ex.ToString() };
            }
        }

        // ════════════════════════════════════════════════════════════════
        // WEBMETHOD — TryCloseGuard
        // Verifica si todas las locaciones activas tienen su última
        // solicitud en Approved desde el inicio de la guardia.
        // Si se cumple, cierra la guardia y dispara notificación.
        // Llamado por live.js después de cada carga del dashboard.
        // ════════════════════════════════════════════════════════════════

        [WebMethod(EnableSession = true)]
        public static object TryCloseGuard() {
            SecurePage.CheckAccess(RoleLevel.Regular);

            string cs = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
            try {
                using (var conn = new SqlConnection(cs)) {
                    conn.Open();

                    // 1. Buscar guardia activa iniciada
                    const string sqlGuard = @"
                        SELECT TOP 1 Guard_Id, Start_Time
                        FROM Guard_Schedule
                        WHERE Start_Time IS NOT NULL
                          AND End_Time   IS NULL
                        ORDER BY Start_Time DESC";

                    int guardId = 0;
                    DateTime startTime = DateTime.MinValue;

                    using (var cmd = new SqlCommand(sqlGuard, conn))
                    using (var dr = cmd.ExecuteReader()) {
                        if (!dr.Read())
                            return new { success = true, closed = false, reason = "no_active_guard" };
                        guardId = dr.GetInt32(0);
                        startTime = dr.GetDateTime(1);
                    }

                    // 2. Contar locaciones activas totales
                    int totalLocations;
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM WMS_Location WHERE Active = 1", conn)) {
                        totalLocations = (int)cmd.ExecuteScalar();
                    }

                    if (totalLocations == 0)
                        return new { success = true, closed = false, reason = "no_locations" };

                    // 3. Contar locaciones cuya última solicitud (desde inicio guardia) es Approved
                    const string sqlApproved = @"
                        WITH LatestReq AS (
                            SELECT Location_Id, Status,
                                   ROW_NUMBER() OVER (
                                       PARTITION BY Location_Id ORDER BY Created_At DESC
                                   ) AS rn
                            FROM Closure_Requests
                            WHERE Created_At >= @Since
                        )
                        SELECT COUNT(DISTINCT wl.Location_Id)
                        FROM WMS_Location wl
                        INNER JOIN LatestReq lr
                               ON lr.Location_Id = wl.Location_Id AND lr.rn = 1
                        WHERE wl.Active = 1
                          AND lr.Status = 'Approved'";

                    int approvedCount;
                    using (var cmd = new SqlCommand(sqlApproved, conn)) {
                        cmd.Parameters.AddWithValue("@Since", startTime);
                        approvedCount = (int)cmd.ExecuteScalar();
                    }

                    if (approvedCount < totalLocations)
                        return new {
                            success = true,
                            closed = false,
                            reason = "pending",
                            approved = approvedCount,
                            total = totalLocations
                        };

                    // 4. Cerrar guardia
                    var da = new DataAccess.GuardDataAccess();
                    var result = da.CloseGuard(guardId);

                    if (!result.Success)
                        return new { success = false, message = result.Message };

                    System.Diagnostics.Debug.WriteLine(
                        $"[TryCloseGuard] Guard {guardId} cerrada automáticamente ✓");

                    // 5. Notificar (fire-and-forget)
                    string closedBy = HttpContext.Current.Session["Email"]?.ToString();
                    System.Threading.Tasks.Task.Run(() => {
                        Services.EmailService.NotifyGuardClosed(
                            closedAt: DateTime.Now,
                            triggeredByEmail: closedBy
                        );
                    });

                    return new { success = true, closed = true };
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[TryCloseGuard] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ════════════════════════════════════════════════════════════════
        // HELPERS PRIVADOS
        // ════════════════════════════════════════════════════════════════

        // ── Guardia activa (iniciada, sin End_Time) ──────────────────────
        private static GuardDto GetActiveGuard(SqlConnection conn) {
            const string sqlGuard = @"
                SELECT TOP 1
                    gs.Guard_Id,
                    gs.Start_Time,
                    cb.Username AS StartedBy
                FROM  Guard_Schedule gs
                LEFT  JOIN Users cb ON cb.User_Id = gs.Created_By
                WHERE gs.Start_Time IS NOT NULL
                  AND gs.Start_Time <= GETDATE()
                  AND gs.End_Time   IS NULL
                ORDER BY gs.Start_Time DESC";

            GuardDto guard = null;

            using (var cmd = new SqlCommand(sqlGuard, conn))
            using (var dr = cmd.ExecuteReader()) {
                if (!dr.Read()) return null;
                guard = new GuardDto {
                    IsActive = true,
                    GuardId = dr.GetInt32(0),
                    StartTime = dr.GetDateTime(1),
                    StartedBy = dr.IsDBNull(2) ? "" : dr.GetString(2)
                };
            }

            // Cargar spots del guard
            const string sqlSpots = @"
                SELECT d.Department_Code, d.Department_Name, u.Username
                FROM   Guard_Spots sp
                INNER JOIN Departments d ON d.Department_Id = sp.Department_Id
                LEFT  JOIN Users       u ON u.User_Id       = sp.User_Id
                WHERE  sp.Guard_Id = @GuardId
                ORDER BY d.Department_Code";

            using (var cmd = new SqlCommand(sqlSpots, conn)) {
                cmd.Parameters.AddWithValue("@GuardId", guard.GuardId);
                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        guard.Spots.Add(new GuardSpotDto {
                            DeptCode = dr.GetString(0),
                            DeptName = dr.GetString(1),
                            Username = dr.IsDBNull(2) ? null : dr.GetString(2)
                        });
                    }
                }
            }

            return guard;
        }

        // ── Locaciones según rol ─────────────────────────────────────────
        private static List<LocationDto> GetLocations(SqlConnection conn, int userId, int roleId) {
            string sql;

            if (roleId >= RoleLevel.Owner) {
                sql = @"
                    SELECT Location_Id, Location_Name
                    FROM   WMS_Location
                    WHERE  Active = 1
                    ORDER BY Location_Name";
            } else if (roleId == RoleLevel.Administrador || roleId == RoleLevel.Manager) {
                sql = @"
                    SELECT DISTINCT
                        wl.Location_Id,
                        wl.Location_Name
                    FROM  WMS_Location  wl
                    INNER JOIN Location_OMS lo ON wl.Location_Id = lo.Location_Id
                    INNER JOIN Users_OMS    uo ON lo.OMS_Id      = uo.OMS_Id
                    WHERE wl.Active  = 1
                      AND uo.User_Id = @userId
                    ORDER BY wl.Location_Name";
            } else {
                sql = @"
                    SELECT
                        wl.Location_Id,
                        wl.Location_Name
                    FROM  WMS_Location   wl
                    INNER JOIN Users_Location ul ON wl.Location_Id = ul.Location_Id
                    WHERE wl.Active  = 1
                      AND ul.User_Id = @userId
                    ORDER BY wl.Location_Name";
            }

            var list = new List<LocationDto>();
            using (var cmd = new SqlCommand(sql, conn)) {
                if (roleId < RoleLevel.Owner)
                    cmd.Parameters.AddWithValue("@userId", userId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        list.Add(new LocationDto {
                            LocationId = dr.GetInt32(0),
                            LocationName = dr.GetString(1),
                            LocationCode = ""   // columna no existe aún en WMS_Location
                        });
                    }
                }
            }
            return list;
        }

        // ── Enriquecer con solicitudes (última por locación) ─────────────
        private static void EnrichWithRequests(
            SqlConnection conn,
            List<LocationDto> locations,
            DateTime since) {

            var ids = new List<string>();
            foreach (var l in locations) ids.Add(l.LocationId.ToString());
            string inClause = string.Join(",", ids);

            string sql = string.Format(@"
                WITH LatestReq AS (
                    SELECT
                        cr.Location_Id,
                        cr.Request_Id,
                        cr.Status,
                        cr.Created_At,
                        cr.Review_Notes,
                        cr.Reviewed_At,
                        u.Username  AS RequestedByName,
                        ru.Username AS ReviewedByName,
                        ROW_NUMBER() OVER (
                            PARTITION BY cr.Location_Id
                            ORDER BY cr.Created_At DESC
                        ) AS rn
                    FROM  Closure_Requests cr
                    INNER JOIN Users u  ON cr.Requested_By = u.User_Id
                    LEFT  JOIN Users ru ON cr.Reviewed_By  = ru.User_Id
                    WHERE cr.Location_Id IN ({0})
                      AND cr.Created_At  >= @since
                )
                SELECT
                    Location_Id, Request_Id, Status,
                    Created_At, RequestedByName,
                    ReviewedByName, Reviewed_At, Review_Notes
                FROM LatestReq
                WHERE rn = 1", inClause);

            using (var cmd = new SqlCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@since", since);

                var dt = new DataTable();
                using (var da = new SqlDataAdapter(cmd))
                    da.Fill(dt);

                foreach (DataRow row in dt.Rows) {
                    int locId = Convert.ToInt32(row["Location_Id"]);
                    var loc = locations.Find(l => l.LocationId == locId);
                    if (loc == null) continue;

                    loc.Status = row["Status"].ToString();
                    loc.RequestId = Convert.ToInt32(row["Request_Id"]);
                    loc.RequestedBy = row["RequestedByName"].ToString();
                    loc.RequestedAt = Convert.ToDateTime(row["Created_At"]).ToString("dd/MM HH:mm");
                    loc.ReviewedBy = row["ReviewedByName"] == DBNull.Value ? ""
                                        : row["ReviewedByName"].ToString();
                    loc.ReviewedAt = row["Reviewed_At"] == DBNull.Value ? ""
                                        : Convert.ToDateTime(row["Reviewed_At"]).ToString("dd/MM HH:mm");
                    loc.ReviewNotes = row["Review_Notes"] == DBNull.Value ? ""
                                        : row["Review_Notes"].ToString();
                }
            }
        }
    }
}