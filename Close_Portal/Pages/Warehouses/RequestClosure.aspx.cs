using Close_Portal.Core;
using Close_Portal.Hubs;
using Close_Portal.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Services;

namespace Close_Portal.Pages {
    public partial class RequestClosure : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Regular;

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e) {
            // UI cargada 100% por JS vía WebMethods
        }

        // ============================================================
        // GET MY LOCATIONS
        // Devuelve las locaciones asignadas al usuario en sesión
        // (via MonthEnd_Users_Location) disponibles para solicitar cierre.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetMyLocations() {
            try {
                CheckAccess(RoleLevel.Regular);
                var session = System.Web.HttpContext.Current.Session;
                int userId = (int)session["UserId"];

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // Resolver guardia activa confirmada
                    int guardId = 0;
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 1 Guard_Id FROM MonthEnd_Guard_Schedule
                        WHERE End_Time IS NULL AND Is_Confirmed = 1
                        ORDER BY Created_At DESC", conn)) {
                        var val = cmd.ExecuteScalar();
                        if (val == null || val == System.DBNull.Value)
                            return new { success = true, data = new List<object>(), guardActive = false };
                        guardId = (int)val;
                    }

                    // Locaciones asignadas al usuario que pertenecen a la guardia activa
                    // y que aún no tienen solicitud Pending o Approved en esta guardia
                    string sql = @"
                        SELECT
                            wl.Location_Id,
                            wl.Location_Name
                        FROM MonthEnd_Locations wl
                        INNER JOIN MonthEnd_Users_Location ul ON ul.Location_Id = wl.Location_Id
                        INNER JOIN MonthEnd_Guard_Locations gl ON gl.Location_Id = wl.Location_Id
                                                               AND gl.Guard_Id   = @GuardId
                        WHERE ul.User_Id = @UserId
                          AND wl.Active  = 1
                          AND NOT EXISTS (
                              SELECT 1 FROM MonthEnd_Closure_Requests cr
                              WHERE cr.Location_Id  = wl.Location_Id
                                AND cr.Requested_By = @UserId
                                AND cr.Guard_Id     = @GuardId
                                AND cr.Status       IN ('Pending', 'Approved')
                          )
                        ORDER BY wl.Location_Name";

                    var list = new List<object>();
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                list.Add(new {
                                    locationId = (int)r["Location_Id"],
                                    locationName = r["Location_Name"].ToString()
                                });
                            }
                        }
                    }

                    return new { success = true, data = list, guardActive = true };
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[RequestClosure.GetMyLocations] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET MANAGER FOR LOCATION
        // Busca el Administrador (RoleId = 3) que tiene la locación asignada
        // directamente en MonthEnd_Users_Location.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetManagerForLocation(int locationId) {
            try {
                CheckAccess(RoleLevel.Regular);

                string sql = @"
                    SELECT TOP 1
                        u.User_Id,
                        RTRIM(ISNULL(u.First_Name,'') + ' ' + ISNULL(u.Last_Name,'')) AS Username,
                        u.Email
                    FROM MonthEnd_Users u
                    INNER JOIN MonthEnd_Users_Location ul ON ul.User_Id     = u.User_Id
                    WHERE ul.Location_Id = @LocationId
                      AND u.Role_Id      = @ManagerRole
                      AND u.Active       = 1
                    ORDER BY u.First_Name, u.Last_Name";

                using (var conn = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@ManagerRole", RoleLevel.Administrador);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            if (!r.Read())
                                return new { success = true, data = (object)null };

                            return new {
                                success = true,
                                data = new {
                                    managerId = (int)r["User_Id"],
                                    managerName = r["Username"]?.ToString() ?? "",
                                    managerEmail = r["Email"].ToString()
                                }
                            };
                        }
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[RequestClosure.GetManagerForLocation] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        [WebMethod(EnableSession = true)]
        public static object SubmitRequest(int locationId, string notes) {
            try {
                CheckAccess(RoleLevel.Regular);
                var session = System.Web.HttpContext.Current.Session;
                int requestedBy = (int)session["UserId"];
                string requesterName = session["FullName"]?.ToString() ?? "";
                string requesterEmail = session["Email"]?.ToString() ?? "";

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // 1. Resolver guardia activa confirmada — bloquear si no hay
                    int guardId = 0;
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 1 Guard_Id FROM MonthEnd_Guard_Schedule
                        WHERE End_Time IS NULL AND Is_Confirmed = 1
                        ORDER BY Created_At DESC", conn)) {
                        var val = cmd.ExecuteScalar();
                        if (val == null || val == System.DBNull.Value)
                            return new { success = false, message = "No hay una guardia activa en este momento. No se puede enviar una solicitud de cierre." };
                        guardId = (int)val;
                    }

                    // 2. Validar que la locación está asignada al solicitante
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM MonthEnd_Users_Location
                        WHERE User_Id = @UserId AND Location_Id = @LocationId", conn)) {
                        cmd.Parameters.AddWithValue("@UserId", requestedBy);
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        if ((int)cmd.ExecuteScalar() == 0)
                            return new { success = false, message = "No tienes acceso a esa locación." };
                    }

                    // 3. Validar que la locación pertenece a la guardia activa
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM MonthEnd_Guard_Locations
                        WHERE Guard_Id = @GuardId AND Location_Id = @LocationId", conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        if ((int)cmd.ExecuteScalar() == 0)
                            return new { success = false, message = "Esa locación no forma parte de la guardia activa." };
                    }

                    // 4. Verificar que no haya solicitud Pending/Approved para esta guardia
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM MonthEnd_Closure_Requests
                        WHERE Requested_By = @UserId
                          AND Location_Id  = @LocationId
                          AND Guard_Id     = @GuardId
                          AND Status       IN ('Pending', 'Approved')", conn)) {
                        cmd.Parameters.AddWithValue("@UserId", requestedBy);
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        if ((int)cmd.ExecuteScalar() > 0)
                            return new { success = false, message = "Ya tienes una solicitud pendiente o aprobada para esa locación en esta guardia." };
                    }

                    // 5. Obtener nombre de la locación
                    string locationName = "";
                    using (var cmd = new SqlCommand(
                        "SELECT Location_Name FROM MonthEnd_Locations WHERE Location_Id = @LocationId", conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        locationName = cmd.ExecuteScalar()?.ToString() ?? "";
                    }

                    // 6. Buscar Administrador de la locación
                    int managerId = 0;
                    string managerEmail = "";
                    string managerName = "";
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 1 u.User_Id, u.Email,
                               RTRIM(ISNULL(u.First_Name,'') + ' ' + ISNULL(u.Last_Name,'')) AS Username
                        FROM MonthEnd_Users u
                        INNER JOIN MonthEnd_Users_Location ul ON ul.User_Id = u.User_Id
                        WHERE ul.Location_Id = @LocationId
                          AND u.Role_Id      = @ManagerRole
                          AND u.Active       = 1
                        ORDER BY u.First_Name, u.Last_Name", conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@ManagerRole", RoleLevel.Administrador);
                        using (var r = cmd.ExecuteReader()) {
                            if (r.Read()) {
                                managerId = (int)r["User_Id"];
                                managerEmail = r["Email"].ToString();
                                managerName = r["Username"]?.ToString() ?? "";
                            }
                        }
                    }

                    if (managerId == 0)
                        return new { success = false, message = "Esta locación no tiene un Administrador asignado. Contacta con tu Owner." };

                    // 7. Insertar solicitud con Guard_Id
                    int requestId;
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO MonthEnd_Closure_Requests (Location_Id, Requested_By, Notes, Status, Guard_Id)
                        OUTPUT INSERTED.Request_Id
                        VALUES (@LocationId, @RequestedBy, @Notes, 'Pending', @GuardId)", conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@RequestedBy", requestedBy);
                        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes)
                                                               ? (object)DBNull.Value : notes.Trim());
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        requestId = (int)cmd.ExecuteScalar();
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[RequestClosure.SubmitRequest] RequestId={requestId} Location={locationName} GuardId={guardId} by UserId={requestedBy}");

                    // Marcar notificaciones anteriores de esta locación como leídas
                    using (var cmd = new SqlCommand(@"
                        UPDATE MonthEnd_Notifications
                        SET Is_Read = 1
                        WHERE User_Id  = @UserId
                          AND Type     = 'request_reviewed'
                          AND Is_Read  = 0
                          AND Reference_Id IN (
                              SELECT Request_Id FROM MonthEnd_Closure_Requests
                              WHERE Location_Id = @LocationId
                          )", conn)) {
                        cmd.Parameters.AddWithValue("@UserId", requestedBy);
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.ExecuteNonQuery();
                    }

                    LocationHub.NotifyNewRequest(managerId, requestId, locationName, requesterName);

                    System.Threading.Tasks.Task.Run(() => {
                        EmailService.NotifyClosureRequest(
                            managerEmail: managerEmail,
                            managerName: managerName,
                            requesterName: requesterName,
                            requesterEmail: requesterEmail,
                            wmsCode: locationName,
                            wmsName: locationName,
                            notes: notes,
                            requestId: requestId
                        );
                    });
                }

                return new { success = true, message = "Solicitud enviada. El Administrador recibirá una notificación." };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[RequestClosure.SubmitRequest] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET MY HISTORY
        // Historial de solicitudes del usuario en sesión.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetMyHistory() {
            try {
                CheckAccess(RoleLevel.Regular);
                var session = System.Web.HttpContext.Current.Session;
                int userId = (int)session["UserId"];

                var list = new List<object>();
                using (var conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT
                            cr.Request_Id,
                            cr.Status,
                            cr.Notes,
                            cr.Created_At,
                            cr.Review_Notes,
                            cr.Reviewed_At,
                            wl.Location_Name,
                            RTRIM(ISNULL(rev.First_Name,'') + ' ' + ISNULL(rev.Last_Name,'')) AS ReviewedByName
                        FROM MonthEnd_Closure_Requests cr
                        INNER JOIN MonthEnd_Locations wl  ON wl.Location_Id = cr.Location_Id
                        LEFT  JOIN MonthEnd_Users        rev ON rev.User_Id     = cr.Reviewed_By
                        WHERE cr.Requested_By = @UserId
                        ORDER BY cr.Created_At DESC";

                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                list.Add(new {
                                    requestId = (int)r["Request_Id"],
                                    status = r["Status"].ToString(),
                                    notes = r["Notes"]?.ToString() ?? "",
                                    createdAt = ((DateTime)r["Created_At"]).ToString("dd/MM/yyyy HH:mm"),
                                    reviewNotes = r["Review_Notes"]?.ToString() ?? "",
                                    reviewedAt = r["Reviewed_At"] != DBNull.Value
                                                         ? ((DateTime)r["Reviewed_At"]).ToString("dd/MM/yyyy HH:mm")
                                                         : null,
                                    locationName = r["Location_Name"].ToString(),
                                    reviewedByName = r["ReviewedByName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return new { success = true, data = list };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[RequestClosure.GetMyHistory] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }
    }
}