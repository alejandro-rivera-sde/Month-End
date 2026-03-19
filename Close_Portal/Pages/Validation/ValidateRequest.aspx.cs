using Close_Portal.Core;
using Close_Portal.Hubs;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Services;

namespace Close_Portal.Pages {
    public partial class ValidateRequest : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Manager;

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e) {
            // UI cargada 100% por JS vía WebMethods
        }

        // ============================================================
        // GET REQUESTS
        // Manager (2) → solo solicitudes de locaciones cuyo OMS
        //               está en sus Users_OMS
        // Admin (3) / Owner (4) → todas
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetRequests() {
            try {
                CheckAccess(RoleLevel.Manager);
                var session = System.Web.HttpContext.Current.Session;
                int userId = (int)session["UserId"];
                int roleId = (int)session["RoleId"];

                bool isManagerOnly = roleId == RoleLevel.Manager;

                string sql = isManagerOnly
                    ? @"
                        SELECT
                            cr.Request_Id,
                            cr.Status,
                            cr.Notes,
                            cr.Created_At,
                            cr.Review_Notes,
                            cr.Reviewed_At,
                            wl.Location_Id,
                            wl.Location_Name,
                            STUFF((
                                SELECT ', ' + o.OMS_Code
                                FROM Location_OMS lo2
                                INNER JOIN OMS o ON o.OMS_Id = lo2.OMS_Id
                                WHERE lo2.Location_Id = cr.Location_Id
                                FOR XML PATH('')
                            ), 1, 2, '') AS OmsLabel,
                            req.Username  AS RequesterName,
                            req.Email     AS RequesterEmail,
                            rev.Username  AS ReviewedByName
                        FROM Closure_Requests cr
                        INNER JOIN WMS_Location wl ON wl.Location_Id = cr.Location_Id
                        INNER JOIN Users req        ON req.User_Id    = cr.Requested_By
                        LEFT  JOIN Users rev        ON rev.User_Id    = cr.Reviewed_By
                        WHERE EXISTS (
                            SELECT 1
                            FROM Users_Location ul
                            WHERE ul.Location_Id = cr.Location_Id
                              AND ul.User_Id     = @UserId
                        )
                        ORDER BY
                            CASE cr.Status WHEN 'Pending' THEN 0 ELSE 1 END,
                            cr.Created_At DESC"
                    : @"
                        SELECT
                            cr.Request_Id,
                            cr.Status,
                            cr.Notes,
                            cr.Created_At,
                            cr.Review_Notes,
                            cr.Reviewed_At,
                            wl.Location_Id,
                            wl.Location_Name,
                            STUFF((
                                SELECT ', ' + o.OMS_Code
                                FROM Location_OMS lo2
                                INNER JOIN OMS o ON o.OMS_Id = lo2.OMS_Id
                                WHERE lo2.Location_Id = cr.Location_Id
                                FOR XML PATH('')
                            ), 1, 2, '') AS OmsLabel,
                            req.Username  AS RequesterName,
                            req.Email     AS RequesterEmail,
                            rev.Username  AS ReviewedByName
                        FROM Closure_Requests cr
                        INNER JOIN WMS_Location wl ON wl.Location_Id = cr.Location_Id
                        INNER JOIN Users req        ON req.User_Id    = cr.Requested_By
                        LEFT  JOIN Users rev        ON rev.User_Id    = cr.Reviewed_By
                        ORDER BY
                            CASE cr.Status WHEN 'Pending' THEN 0 ELSE 1 END,
                            cr.Created_At DESC";

                var list = new List<object>();
                using (var conn = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(sql, conn)) {
                        if (isManagerOnly)
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
                                    reviewedAt = r["Reviewed_At"] != System.DBNull.Value
                                                     ? ((DateTime)r["Reviewed_At"]).ToString("dd/MM/yyyy HH:mm")
                                                     : null,
                                    locationId = (int)r["Location_Id"],
                                    locationName = r["Location_Name"].ToString(),
                                    omsLabel = r["OmsLabel"]?.ToString() ?? "—",
                                    requesterName = r["RequesterName"]?.ToString() ?? "",
                                    requesterEmail = r["RequesterEmail"].ToString(),
                                    reviewedByName = r["ReviewedByName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return new { success = true, data = list };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ValidateRequest.GetRequests] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // REVIEW REQUEST
        // Aprueba o rechaza una solicitud Pending.
        // Manager valida que comparte OMS con la locación.
        // action: "Approved" | "Rejected"
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object ReviewRequest(int requestId, string action, string reviewNotes) {
            try {
                CheckAccess(RoleLevel.Manager);
                var session = System.Web.HttpContext.Current.Session;
                int reviewerId = (int)session["UserId"];
                int roleId = (int)session["RoleId"];

                if (action != "Approved" && action != "Rejected")
                    return new { success = false, message = "Acción no válida." };

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // 1. Verificar que la solicitud existe y está Pending
                    int locationId;
                    using (var cmd = new SqlCommand(@"
                        SELECT Location_Id, Status FROM Closure_Requests
                        WHERE Request_Id = @RequestId", conn)) {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        using (var r = cmd.ExecuteReader()) {
                            if (!r.Read())
                                return new { success = false, message = "Solicitud no encontrada." };
                            if (r["Status"].ToString() != "Pending")
                                return new { success = false, message = "La solicitud ya fue procesada." };
                            locationId = (int)r["Location_Id"];
                        }
                    }

                    // 2. Manager: verificar que tiene la locación asignada en Users_Location
                    if (roleId == RoleLevel.Manager) {
                        using (var cmd = new SqlCommand(@"
                            SELECT COUNT(*)
                            FROM Users_Location
                            WHERE Location_Id = @LocationId
                              AND User_Id     = @UserId", conn)) {
                            cmd.Parameters.AddWithValue("@LocationId", locationId);
                            cmd.Parameters.AddWithValue("@UserId", reviewerId);
                            if ((int)cmd.ExecuteScalar() == 0)
                                return new { success = false, message = "No tienes permiso para revisar esta solicitud." };
                        }
                    }

                    // 3. Actualizar la solicitud
                    using (var cmd = new SqlCommand(@"
                        UPDATE Closure_Requests SET
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

                    // Notificar en tiempo real a todos los clientes en Live.aspx
                    string reviewerName = session["Username"]?.ToString() ?? session["Email"]?.ToString() ?? "";
                    string locName = "";
                    using (var cmd = new SqlCommand(
                        "SELECT Location_Name FROM WMS_Location WHERE Location_Id = @LocationId", conn)) {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        locName = cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                    LocationHub.NotifyLocationUpdated(locationId, locName, action, reviewerName);

                    // Notificar al solicitante específico
                    int requestedByUserId;
                    using (var cmd = new SqlCommand(
                        "SELECT Requested_By FROM Closure_Requests WHERE Request_Id = @RequestId", conn)) {
                        cmd.Parameters.AddWithValue("@RequestId", requestId);
                        requestedByUserId = (int)cmd.ExecuteScalar();
                    }
                    LocationHub.NotifyRequestReviewed(requestedByUserId, requestId, locName, action, reviewerName);
                }

                string label = action == "Approved" ? "aprobada" : "rechazada";
                return new { success = true, message = $"Solicitud #{requestId} {label} correctamente." };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ValidateRequest.ReviewRequest] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }
    }
}