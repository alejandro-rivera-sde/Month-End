using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
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

                    // 2. Locaciones — solo las involucradas en la guardia activa
                    int guardId = guard?.GuardId ?? 0;
                    var locations = GetLocations(conn, userId, roleId, guardId);

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
                        FROM MonthEnd_Guard_Schedule
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

                    // 2. Contar locaciones involucradas en esta guardia (MonthEnd_Guard_Locations)
                    int totalLocations;
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM MonthEnd_Guard_Locations WHERE Guard_Id = @GuardId", conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        totalLocations = (int)cmd.ExecuteScalar();
                    }

                    // Si no hay locaciones definidas, no cerrar
                    if (totalLocations == 0)
                        return new { success = true, closed = false, reason = "no_locations" };

                    // 3. Contar locaciones involucradas cuya última solicitud es Approved
                    const string sqlApproved = @"
                        WITH LatestReq AS (
                            SELECT cr.Location_Id, cr.Status,
                                   ROW_NUMBER() OVER (
                                       PARTITION BY cr.Location_Id ORDER BY cr.Created_At DESC
                                   ) AS rn
                            FROM MonthEnd_Closure_Requests cr
                            INNER JOIN MonthEnd_Guard_Locations gl
                                   ON gl.Location_Id = cr.Location_Id
                                  AND gl.Guard_Id    = @GuardId
                            WHERE cr.Created_At >= @Since
                        )
                        SELECT COUNT(DISTINCT Location_Id)
                        FROM LatestReq
                        WHERE rn = 1
                          AND Status = 'Approved'";

                    int approvedCount;
                    using (var cmd = new SqlCommand(sqlApproved, conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
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
        // WEBMETHOD — GetGuardSpots
        // Devuelve solo los spots de la guardia activa para actualizar
        // el banner en tiempo real sin recargar todo el dashboard.
        // ════════════════════════════════════════════════════════════════
        [WebMethod(EnableSession = true)]
        public static object GetGuardSpots() {
            try {
                var session = HttpContext.Current.Session;
                if (session["UserId"] == null) return new { success = false };

                string cs = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
                using (var conn = new SqlConnection(cs)) {
                    conn.Open();
                    var guard = GetActiveGuard(conn);
                    if (guard == null)
                        return new { success = true, isActive = false, spots = new object[0] };

                    var spots = guard.Spots.Select(s => new {
                        deptCode = s.DeptCode,
                        deptName = s.DeptName,
                        username = s.Username
                    }).ToArray();

                    return new {
                        success = true,
                        isActive = true,
                        startTime = guard.StartTime.ToString("dd/MM/yyyy HH:mm"),
                        spots
                    };
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetGuardSpots] ERROR: {ex.Message}");
                return new { success = false };
            }
        }

        // ════════════════════════════════════════════════════════════════
        // WEBMETHOD — GetUnreadCount
        // ════════════════════════════════════════════════════════════════
        [WebMethod(EnableSession = true)]
        public static object GetUnreadCount() {
            try {
                var session = HttpContext.Current.Session;
                if (session["UserId"] == null) return new { success = false, count = 0 };
                int userId = Convert.ToInt32(session["UserId"]);
                string cs = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
                int count = 0;
                using (var conn = new SqlConnection(cs))
                using (var cmd = new SqlCommand(@"
                    DECLARE @GuardStart DATETIME = (
                        SELECT TOP 1 Created_At FROM MonthEnd_Guard_Schedule
                        WHERE End_Time IS NULL ORDER BY Created_At DESC
                    )
                    SELECT COUNT(*) FROM MonthEnd_Notifications
                    WHERE User_Id  = @UserId
                      AND Is_Read  = 0
                      AND Created_At >= ISNULL(@GuardStart, Created_At)", conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();
                    count = (int)cmd.ExecuteScalar();
                }
                return new { success = true, count };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetUnreadCount] ERROR: {ex.Message}");
                return new { success = false, count = 0 };
            }
        }

        // ════════════════════════════════════════════════════════════════
        // WEBMETHOD — GetNotifications
        // Devuelve las últimas 20 notificaciones del usuario (leídas y no)
        // ════════════════════════════════════════════════════════════════
        [WebMethod(EnableSession = true)]
        public static object GetNotifications() {
            try {
                var session = HttpContext.Current.Session;
                if (session["UserId"] == null) return new { success = false };
                int userId = Convert.ToInt32(session["UserId"]);
                int roleId = session["RoleId"] != null ? Convert.ToInt32(session["RoleId"]) : 1;
                string cs = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

                var list = new System.Collections.Generic.List<object>();
                using (var conn = new SqlConnection(cs))
                using (var cmd = new SqlCommand(@"
                    DECLARE @GuardStart DATETIME = (
                        SELECT TOP 1 Created_At FROM MonthEnd_Guard_Schedule
                        WHERE End_Time IS NULL ORDER BY Created_At DESC
                    )
                    SELECT TOP 20
                        Notification_Id, Type, Reference_Id, Message, Is_Read, Created_At
                    FROM MonthEnd_Notifications
                    WHERE User_Id   = @UserId
                      AND Created_At >= ISNULL(@GuardStart, Created_At)
                    ORDER BY Created_At DESC", conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            string type = r["Type"].ToString();
                            int? refId = r["Reference_Id"] as int?;

                            // URL de destino según tipo y rol
                            string url = ResolveNotifUrl(type, roleId);

                            list.Add(new {
                                notificationId = (int)r["Notification_Id"],
                                type,
                                referenceId = refId,
                                message = r["Message"].ToString(),
                                isRead = (bool)r["Is_Read"],
                                createdAt = ((DateTime)r["Created_At"]).ToString("dd/MM HH:mm"),
                                url
                            });
                        }
                    }
                }
                return new { success = true, data = list };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetNotifications] ERROR: {ex.Message}");
                return new { success = false };
            }
        }

        // Resuelve la URL de destino según el tipo de notificación y el rol del usuario
        private static string ResolveNotifUrl(string type, int roleId) {
            switch (type) {
                case "new_request":
                    // Administrador/Owner → ValidateRequest
                    return "Pages/Admin/ValidateRequest.aspx";
                case "request_reviewed":
                    // Regular → RequestClosure (ve el historial de sus solicitudes)
                    return "Pages/Warehouses/RequestClosure.aspx";
                default:
                    return null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        // WEBMETHOD — MarkAsRead
        // Marca como leídas las notificaciones de un tipo+referencia específica
        // ════════════════════════════════════════════════════════════════
        [WebMethod(EnableSession = true)]
        public static object MarkAsRead(int referenceId, string type) {
            try {
                var session = HttpContext.Current.Session;
                if (session["UserId"] == null) return new { success = false };
                int userId = Convert.ToInt32(session["UserId"]);
                string cs = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
                using (var conn = new SqlConnection(cs))
                using (var cmd = new SqlCommand(@"
                    UPDATE MonthEnd_Notifications SET Is_Read = 1
                    WHERE User_Id     = @UserId
                      AND Reference_Id = @RefId
                      AND Type         = @Type
                      AND Is_Read      = 0", conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@RefId", referenceId);
                    cmd.Parameters.AddWithValue("@Type", type);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new { success = true };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[MarkAsRead] ERROR: {ex.Message}");
                return new { success = false };
            }
        }

        // ════════════════════════════════════════════════════════════════
        // WEBMETHOD — MarkAllRead
        // ════════════════════════════════════════════════════════════════
        [WebMethod(EnableSession = true)]
        public static object MarkAllRead() {
            try {
                var session = HttpContext.Current.Session;
                if (session["UserId"] == null) return new { success = false };
                int userId = Convert.ToInt32(session["UserId"]);
                string cs = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
                using (var conn = new SqlConnection(cs))
                using (var cmd = new SqlCommand(
                    "UPDATE MonthEnd_Notifications SET Is_Read = 1 WHERE User_Id = @UserId AND Is_Read = 0", conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new { success = true };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[MarkAllRead] ERROR: {ex.Message}");
                return new { success = false };
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
                FROM  MonthEnd_Guard_Schedule gs
                LEFT  JOIN MonthEnd_Users cb ON cb.User_Id = gs.Created_By
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
                FROM   MonthEnd_Guard_Spots sp
                INNER JOIN MonthEnd_Departments d ON d.Department_Id = sp.Department_Id
                LEFT  JOIN MonthEnd_Users       u ON u.User_Id       = sp.User_Id
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
        // ── Locaciones — solo las involucradas en la guardia activa ────────
        private static List<LocationDto> GetLocations(SqlConnection conn, int userId, int roleId, int guardId) {

            // Sin guardia activa → nada que mostrar
            if (guardId == 0)
                return new List<LocationDto>();

            string sql;

            if (roleId >= RoleLevel.Owner) {
                // Owner ve todas las locaciones de la guardia
                sql = @"
                    SELECT wl.Location_Id, wl.Location_Name
                    FROM   MonthEnd_Locations wl
                    INNER JOIN MonthEnd_Guard_Locations gl ON gl.Location_Id = wl.Location_Id
                    WHERE  gl.Guard_Id = @GuardId
                      AND  wl.Active  = 1
                    ORDER BY wl.Location_Name";
            } else {
                // Todos los demás — sus locaciones asignadas que estén en la guardia
                sql = @"
                    SELECT wl.Location_Id, wl.Location_Name
                    FROM   MonthEnd_Locations wl
                    INNER JOIN MonthEnd_Guard_Locations gl ON gl.Location_Id = wl.Location_Id
                    INNER JOIN MonthEnd_Users_Location  ul ON ul.Location_Id = wl.Location_Id
                    WHERE  gl.Guard_Id = @GuardId
                      AND  ul.User_Id  = @UserId
                      AND  wl.Active   = 1
                    ORDER BY wl.Location_Name";
            }

            var list = new List<LocationDto>();
            using (var cmd = new SqlCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@GuardId", guardId);
                if (roleId < RoleLevel.Owner)
                    cmd.Parameters.AddWithValue("@UserId", userId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        list.Add(new LocationDto {
                            LocationId = dr.GetInt32(0),
                            LocationName = dr.GetString(1),
                            LocationCode = ""
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
                    FROM  MonthEnd_Closure_Requests cr
                    INNER JOIN MonthEnd_Users u  ON cr.Requested_By = u.User_Id
                    LEFT  JOIN MonthEnd_Users ru ON cr.Reviewed_By  = ru.User_Id
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