using Close_Portal.Core;
using Close_Portal.Hubs;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Services;

namespace Close_Portal.Pages {
    public partial class ValidateRequest : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Administrador;

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e) {
            // UI cargada 100% por JS vía WebMethods
        }

        // ============================================================
        // GET REQUESTS
        // Administrador / Owner → solicitudes de la guardia activa actual
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetRequests() {
            try {
                CheckAccess(RoleLevel.Administrador);

                // Resolver guardia activa
                int guardId = GetActiveGuardId();
                if (guardId == 0)
                    return new { success = true, data = new List<object>(), guardActive = false };

                string sql = @"
                    SELECT
                        cr.Request_Id,
                        cr.Status,
                        cr.Notes,
                        cr.Created_At,
                        cr.Review_Notes,
                        cr.Reviewed_At,
                        wl.Location_Id,
                        wl.Location_Name,
                        RTRIM(ISNULL(req.First_Name,'') + ' ' + ISNULL(req.Last_Name,'')) AS RequesterName,
                        req.Email     AS RequesterEmail,
                        RTRIM(ISNULL(rev.First_Name,'') + ' ' + ISNULL(rev.Last_Name,'')) AS ReviewedByName
                    FROM MonthEnd_Closure_Requests cr
                    INNER JOIN MonthEnd_Locations wl ON wl.Location_Id = cr.Location_Id
                    INNER JOIN MonthEnd_Users req        ON req.User_Id    = cr.Requested_By
                    LEFT  JOIN MonthEnd_Users rev        ON rev.User_Id    = cr.Reviewed_By
                    WHERE cr.Guard_Id = @GuardId
                    ORDER BY
                        CASE cr.Status WHEN 'Pending' THEN 0 ELSE 1 END,
                        cr.Created_At DESC";

                var list = new List<object>();
                using (var conn = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                list.Add(new {
                                    requestId = (int)r["Request_Id"],
                                    status = r["Status"].ToString(),
                                    notes = r["Notes"]?.ToString() ?? "",
                                    createdAt = ((DateTime)r["Created_At"]).ToString("dd/MM/yyyy HH:mm"),
                                    reviewNotes = r["Review_Notes"]?.ToString() ?? "",
                                    reviewedAt = r["Reviewed_At"] != System.DBNull.Value
                                                     ? ((DateTime)r["Reviewed_At"]).ToString("dd/MM/yyyy HH:mm")
                                                     : null,
                                    locationId = (int)r["Location_Id"],
                                    locationName = r["Location_Name"].ToString(),
                                    requesterName = r["RequesterName"]?.ToString() ?? "",
                                    requesterEmail = r["RequesterEmail"].ToString(),
                                    reviewedByName = r["ReviewedByName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return new { success = true, data = list, guardActive = true };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ValidateRequest.GetRequests] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // REVIEW REQUEST
        // Aprueba o rechaza una solicitud Pending.
        // Solo Administrador / Owner.
        // action: "Approved" | "Rejected"
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object ReviewRequest(int requestId, string action, string reviewNotes) {
            try {
                CheckAccess(RoleLevel.Administrador);
                var session = System.Web.HttpContext.Current.Session;
                int reviewerId = (int)session["UserId"];

                if (action != "Approved" && action != "Rejected")
                    return new { success = false, message = "Acción no válida." };

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // 1. Verificar que la solicitud existe, está Pending y pertenece a la guardia activa
                    int locationId;
                    using (var cmd = new SqlCommand(@"
                        SELECT cr.Location_Id, cr.Status
                        FROM MonthEnd_Closure_Requests cr
                        WHERE cr.Request_Id = @RequestId", conn)) {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        using (var r = cmd.ExecuteReader()) {
                            if (!r.Read())
                                return new { success = false, message = "Solicitud no encontrada." };
                            if (r["Status"].ToString() != "Pending")
                                return new { success = false, message = "La solicitud ya fue procesada." };
                            locationId = (int)r["Location_Id"];
                        }
                    }

                    // 2. Actualizar la solicitud
                    using (var cmd = new SqlCommand(@"
                        UPDATE MonthEnd_Closure_Requests SET
                            Status       = @Status,
                            Reviewed_By  = @ReviewedBy,
                            Reviewed_At  = GETDATE(),
                            Review_Notes = @ReviewNotes
                        WHERE Request_Id = @RequestId", conn)) {
                        cmd.Parameters.AddWithValue("@Status", action);
                        cmd.Parameters.AddWithValue("@ReviewedBy", reviewerId);
                        cmd.Parameters.AddWithValue("@ReviewNotes", string.IsNullOrWhiteSpace(reviewNotes)
                            ? (object)System.DBNull.Value
                            : reviewNotes.Trim());
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        cmd.ExecuteNonQuery();
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[ValidateRequest.ReviewRequest] RequestId={requestId} → {action} by UserId={reviewerId}");

                    string reviewerName = session["FullName"]?.ToString() ?? session["Email"]?.ToString() ?? "";
                    string locName = "";
                    using (var cmd = new SqlCommand(
                        "SELECT Location_Name FROM MonthEnd_Locations WHERE Location_Id = @LocationId", conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        locName = cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                    LocationHub.NotifyLocationUpdated(locationId, locName, action, reviewerName);

                    int requestedByUserId;
                    string requesterEmail = "";
                    string requesterName = "";
                    using (var cmd = new SqlCommand(@"
                        SELECT cr.Requested_By, u.Email,
                               RTRIM(ISNULL(u.First_Name,'') + ' ' + ISNULL(u.Last_Name,'')) AS Username
                        FROM MonthEnd_Closure_Requests cr
                        INNER JOIN MonthEnd_Users u ON u.User_Id = cr.Requested_By
                        WHERE cr.Request_Id = @RequestId", conn)) {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        using (var r = cmd.ExecuteReader()) {
                            r.Read();
                            requestedByUserId = (int)r["Requested_By"];
                            requesterEmail = r["Email"]?.ToString() ?? "";
                            requesterName = r["Username"]?.ToString() ?? requesterEmail;
                        }
                    }
                    LocationHub.NotifyRequestReviewed(requestedByUserId, requestId, locName, action, reviewerName);

                    // Notificar por correo al solicitante
                    string capturedRequesterEmail = requesterEmail;
                    string capturedRequesterName = requesterName;
                    string capturedLocName = locName;
                    string capturedAction = action;
                    string capturedReviewNotes = reviewNotes;
                    string capturedReviewerName = reviewerName;
                    System.Threading.Tasks.Task.Run(() =>
                        Services.EmailService.NotifyClosureResponse(
                            capturedRequesterEmail, capturedRequesterName,
                            capturedLocName, capturedAction,
                            capturedReviewNotes, capturedReviewerName));
                }

                string label = action == "Approved" ? "aprobada" : "rechazada";
                return new { success = true, message = $"Solicitud #{requestId} {label} correctamente." };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ValidateRequest.ReviewRequest] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }
        // ============================================================
        // GET CLOSED REQUESTS
        // Solicitudes Approved de la guardia activa actual.
        // Solo Administrador / Owner.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetClosedRequests() {
            try {
                CheckAccess(RoleLevel.Administrador);

                int guardId = GetActiveGuardId();
                if (guardId == 0)
                    return new { success = true, data = new List<object>(), guardActive = false };

                string sql = @"
                    SELECT
                        cr.Request_Id, cr.Notes, cr.Created_At,
                        cr.Review_Notes, cr.Reviewed_At,
                        wl.Location_Id, wl.Location_Name,
                        RTRIM(ISNULL(req.First_Name,'') + ' ' + ISNULL(req.Last_Name,'')) AS RequesterName,
                        req.Email     AS RequesterEmail,
                        RTRIM(ISNULL(rev.First_Name,'') + ' ' + ISNULL(rev.Last_Name,'')) AS ReviewedByName
                    FROM MonthEnd_Closure_Requests cr
                    INNER JOIN MonthEnd_Locations wl ON wl.Location_Id = cr.Location_Id
                    INNER JOIN MonthEnd_Users req        ON req.User_Id    = cr.Requested_By
                    LEFT  JOIN MonthEnd_Users rev        ON rev.User_Id    = cr.Reviewed_By
                    WHERE cr.Status   = 'Approved'
                      AND cr.Guard_Id = @GuardId
                    ORDER BY cr.Reviewed_At DESC";

                var list = new List<object>();
                using (var conn = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                list.Add(new {
                                    requestId = (int)r["Request_Id"],
                                    locationId = (int)r["Location_Id"],
                                    locationName = r["Location_Name"].ToString(),
                                    notes = r["Notes"]?.ToString() ?? "",
                                    reviewNotes = r["Review_Notes"]?.ToString() ?? "",
                                    createdAt = ((DateTime)r["Created_At"]).ToString("dd/MM/yyyy HH:mm"),
                                    reviewedAt = r["Reviewed_At"] != System.DBNull.Value
                                                         ? ((DateTime)r["Reviewed_At"]).ToString("dd/MM/yyyy HH:mm")
                                                         : null,
                                    requesterName = r["RequesterName"]?.ToString() ?? "",
                                    requesterEmail = r["RequesterEmail"].ToString(),
                                    reviewedByName = r["ReviewedByName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return new { success = true, data = list, guardActive = true };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ValidateRequest.GetClosedRequests] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // REVERT LOCATION
        // Elimina la solicitud Approved → locación vuelve a Active.
        // Solo Administrador / Owner.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object RevertLocation(int requestId, string reason) {
            try {
                CheckAccess(RoleLevel.Administrador);
                var session = System.Web.HttpContext.Current.Session;
                int callerId = (int)session["UserId"];

                if (string.IsNullOrWhiteSpace(reason))
                    return new { success = false, message = "Debes indicar el motivo de la reversión." };

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    int locationId;
                    string locationName;
                    int requestedBy;
                    using (var cmd = new SqlCommand(@"
                        SELECT cr.Location_Id, cr.Requested_By, wl.Location_Name
                        FROM MonthEnd_Closure_Requests cr
                        INNER JOIN MonthEnd_Locations wl ON wl.Location_Id = cr.Location_Id
                        WHERE cr.Request_Id = @RequestId AND cr.Status = 'Approved'", conn)) {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        using (var r = cmd.ExecuteReader()) {
                            if (!r.Read())
                                return new { success = false, message = "Solicitud no encontrada o no está aprobada." };
                            locationId = (int)r["Location_Id"];
                            locationName = r["Location_Name"].ToString();
                            requestedBy = (int)r["Requested_By"];
                        }
                    }

                    using (var cmd = new SqlCommand(
                        "DELETE FROM MonthEnd_Closure_Requests WHERE Request_Id = @RequestId", conn)) {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        cmd.ExecuteNonQuery();
                    }

                    string callerName = session["FullName"]?.ToString() ?? session["Email"]?.ToString() ?? "";
                    LocationHub.NotifyLocationUpdated(locationId, locationName, "Active", callerName);
                    LocationHub.NotifyRequestReviewed(requestedBy, requestId, locationName, "Reverted", callerName);
                }

                return new { success = true, message = "Locación revertida a operación correctamente." };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ValidateRequest.RevertLocation] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }
        // ============================================================
        // HELPER — Guard activa confirmada (0 si no hay ninguna)
        // ============================================================
        private static int GetActiveGuardId() {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 Guard_Id FROM MonthEnd_Guard_Schedule
                    WHERE End_Time IS NULL AND Is_Confirmed = 1
                    ORDER BY Created_At DESC", conn)) {
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    return val != null && val != System.DBNull.Value ? (int)val : 0;
                }
            } catch {
                return 0;
            }
        }
    }
}