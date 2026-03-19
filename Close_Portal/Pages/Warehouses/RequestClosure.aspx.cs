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
        // (via Users_Location) junto con el OmsLabel para el dropdown.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetMyLocations() {
            try {
                CheckAccess(RoleLevel.Regular);
                var session = System.Web.HttpContext.Current.Session;
                int userId = (int)session["UserId"];

                // Una fila por OMS asociado a la locación — agrupar en C#
                string sql = @"
                    SELECT
                        wl.Location_Id,
                        wl.Location_Name,
                        o.OMS_Code
                    FROM WMS_Location wl
                    INNER JOIN Users_Location ul ON ul.Location_Id = wl.Location_Id
                    LEFT  JOIN Location_OMS   lo ON lo.Location_Id = wl.Location_Id
                    LEFT  JOIN OMS             o  ON o.OMS_Id       = lo.OMS_Id
                    WHERE ul.User_Id = @UserId
                      AND wl.Active  = 1
                    ORDER BY wl.Location_Name, o.OMS_Code";

                var locMap = new Dictionary<int, (string Name, List<string> Codes)>();

                using (var conn = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                int locId = (int)r["Location_Id"];
                                if (!locMap.ContainsKey(locId))
                                    locMap[locId] = (r["Location_Name"].ToString(), new List<string>());
                                if (r["OMS_Code"] != DBNull.Value)
                                    locMap[locId].Codes.Add(r["OMS_Code"].ToString());
                            }
                        }
                    }
                }

                var list = new List<object>();
                foreach (var kv in locMap) {
                    list.Add(new {
                        locationId = kv.Key,
                        locationName = kv.Value.Name,
                        omsLabel = kv.Value.Codes.Count > 0
                                       ? string.Join(", ", kv.Value.Codes)
                                       : "—"
                    });
                }

                return new { success = true, data = list };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[RequestClosure.GetMyLocations] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET MANAGER FOR LOCATION
        // Busca el Manager (RoleId = 2) que tiene la locación asignada
        // directamente en Users_Location.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetManagerForLocation(int locationId) {
            try {
                CheckAccess(RoleLevel.Regular);

                string sql = @"
                    SELECT TOP 1
                        u.User_Id,
                        u.Username,
                        u.Email
                    FROM Users u
                    INNER JOIN Users_Location ul ON ul.User_Id     = u.User_Id
                    WHERE ul.Location_Id = @LocationId
                      AND u.Role_Id      = @ManagerRole
                      AND u.Active       = 1
                    ORDER BY u.Username";

                using (var conn = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@ManagerRole", RoleLevel.Manager);
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

        // ============================================================
        // SUBMIT REQUEST
        // Inserta la solicitud (Location_Id) y envía correo al Manager.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SubmitRequest(int locationId, string notes) {
            try {
                CheckAccess(RoleLevel.Regular);
                var session = System.Web.HttpContext.Current.Session;
                int requestedBy = (int)session["UserId"];
                string requesterName = session["Username"]?.ToString() ?? "";
                string requesterEmail = session["Email"]?.ToString() ?? "";

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // 1. Validar que la locación está asignada al solicitante
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM Users_Location
                        WHERE User_Id = @UserId AND Location_Id = @LocationId", conn)) {
                        cmd.Parameters.AddWithValue("@UserId", requestedBy);
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        if ((int)cmd.ExecuteScalar() == 0)
                            return new { success = false, message = "No tienes acceso a esa locación." };
                    }

                    // 2. Verificar que no haya solicitud Pending para esa locación del mismo usuario
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM Closure_Requests
                        WHERE Requested_By = @UserId
                          AND Location_Id  = @LocationId
                          AND Status       = 'Pending'", conn)) {
                        cmd.Parameters.AddWithValue("@UserId", requestedBy);
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        if ((int)cmd.ExecuteScalar() > 0)
                            return new { success = false, message = "Ya tienes una solicitud pendiente para esa locación." };
                    }

                    // 3. Obtener datos de la locación + OMS codes
                    string locationName = "";
                    string omsLabel = "";
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            wl.Location_Name,
                            STUFF((
                                SELECT ', ' + o.OMS_Code
                                FROM Location_OMS lo
                                INNER JOIN OMS o ON o.OMS_Id = lo.OMS_Id
                                WHERE lo.Location_Id = wl.Location_Id
                                FOR XML PATH('')
                            ), 1, 2, '') AS OmsLabel
                        FROM WMS_Location wl
                        WHERE wl.Location_Id = @LocationId", conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        using (var r = cmd.ExecuteReader()) {
                            if (r.Read()) {
                                locationName = r["Location_Name"].ToString();
                                omsLabel = r["OmsLabel"]?.ToString() ?? "—";
                            }
                        }
                    }

                    // 4. Buscar Manager de la locación via Users_Location
                    int managerId = 0;
                    string managerEmail = "";
                    string managerName = "";
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 1
                            u.User_Id, u.Email, u.Username
                        FROM Users u
                        INNER JOIN Users_Location ul ON ul.User_Id     = u.User_Id
                        WHERE ul.Location_Id = @LocationId
                          AND u.Role_Id      = @ManagerRole
                          AND u.Active       = 1
                        ORDER BY u.Username", conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@ManagerRole", RoleLevel.Manager);
                        using (var r = cmd.ExecuteReader()) {
                            if (r.Read()) {
                                managerId = (int)r["User_Id"];
                                managerEmail = r["Email"].ToString();
                                managerName = r["Username"]?.ToString() ?? "";
                            }
                        }
                    }

                    if (managerId == 0)
                        return new { success = false, message = "Esta locación no tiene un Manager asignado. Contacta a tu Administrador." };

                    // 5. Insertar solicitud
                    int requestId;
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO Closure_Requests (Location_Id, Requested_By, Notes, Status)
                        OUTPUT INSERTED.Request_Id
                        VALUES (@LocationId, @RequestedBy, @Notes, 'Pending')", conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@RequestedBy", requestedBy);
                        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes)
                                                               ? (object)DBNull.Value : notes.Trim());
                        requestId = (int)cmd.ExecuteScalar();
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[RequestClosure.SubmitRequest] RequestId={requestId} Location={locationName} by UserId={requestedBy}");

                    // Marcar como leídas notificaciones de respuestas anteriores
                    // de esta misma locación — el nuevo request implica que el usuario
                    // ya vio el resultado anterior.
                    using (var cmd = new SqlCommand(@"
                        UPDATE Notifications
                        SET Is_Read = 1
                        WHERE User_Id  = @UserId
                          AND Type     = 'request_reviewed'
                          AND Is_Read  = 0
                          AND Reference_Id IN (
                              SELECT Request_Id FROM Closure_Requests
                              WHERE Location_Id = @LocationId
                          )", conn)) {
                        cmd.Parameters.AddWithValue("@UserId", requestedBy);
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.ExecuteNonQuery();
                    }

                    // Notificar en tiempo real a managers en ValidateRequest
                    LocationHub.NotifyNewRequest(managerId, requestId, locationName, requesterName);

                    // 6. Notificar al Manager por correo
                    System.Threading.Tasks.Task.Run(() => {
                        EmailService.NotifyClosureRequest(
                            managerEmail: managerEmail,
                            managerName: managerName,
                            requesterName: requesterName,
                            requesterEmail: requesterEmail,
                            wmsCode: omsLabel,       // reutiliza el parámetro para OMS codes
                            wmsName: locationName,   // reutiliza el parámetro para nombre de locación
                            notes: notes,
                            requestId: requestId
                        );
                    });
                }

                return new { success = true, message = "Solicitud enviada. El Manager recibirá una notificación." };

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
                            STUFF((
                                SELECT ', ' + o.OMS_Code
                                FROM Location_OMS lo2
                                INNER JOIN OMS o ON o.OMS_Id = lo2.OMS_Id
                                WHERE lo2.Location_Id = cr.Location_Id
                                FOR XML PATH('')
                            ), 1, 2, '') AS OmsLabel,
                            rev.Username AS ReviewedByName
                        FROM Closure_Requests cr
                        INNER JOIN WMS_Location wl  ON wl.Location_Id = cr.Location_Id
                        LEFT  JOIN Users        rev ON rev.User_Id     = cr.Reviewed_By
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
                                    omsLabel = r["OmsLabel"]?.ToString() ?? "—",
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