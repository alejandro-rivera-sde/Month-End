using Close_Portal.Core;
using Microsoft.AspNet.SignalR;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Close_Portal.Hubs {

    public class LocationHub : Hub {

        public const string LiveGroup = "live";
        public const string ValidateGroup = "validate";

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        // ── Suscripciones ────────────────────────────────────────────
        public Task JoinLive() => Groups.Add(Context.ConnectionId, LiveGroup);
        public Task JoinValidate() => Groups.Add(Context.ConnectionId, ValidateGroup);
        public Task JoinAsRequester(string userId) => Groups.Add(Context.ConnectionId, "requester-" + userId);

        // ── Nueva solicitud → notifica managers ──────────────────────
        public static void NotifyNewRequest(
                int managerId, int requestId,
                string locationName, string requesterName) {

            // Insertar notificación persistente para el manager
            string message = $"{requesterName} solicitó cierre de {locationName}";
            InsertNotification(managerId, "new_request", requestId, message);

            // Push SignalR al manager para actualizar badge en tiempo real
            var ctx = GlobalHost.ConnectionManager.GetHubContext<LocationHub>();
            ctx.Clients.Group(ValidateGroup).badgeUpdate();
            ctx.Clients.Group(ValidateGroup).newRequest(new {
                locationName,
                requesterName
            });
        }

        // ── Solicitud revisada → notifica al solicitante ─────────────
        public static void NotifyRequestReviewed(
                int requesterId, int requestId,
                string locationName, string newStatus, string reviewedBy) {

            string statusLabel;
            if (newStatus == "Approved") statusLabel = "aprobada";
            else if (newStatus == "Reverted") statusLabel = "revertida";
            else statusLabel = "rechazada";

            string message = $"Tu solicitud de cierre para {locationName} fue {statusLabel} por {reviewedBy}";
            InsertNotification(requesterId, "request_reviewed", requestId, message);

            var ctx = GlobalHost.ConnectionManager.GetHubContext<LocationHub>();
            ctx.Clients.Group("requester-" + requesterId).badgeUpdate();
            ctx.Clients.Group("requester-" + requesterId).requestReviewed(new {
                locationName,
                newStatus,
                reviewedBy
            });
        }

        // ── Guardia finalizada → notifica a todos los solicitantes ───
        public static void NotifyGuardClosed(int guardId) {
            try {
                var userIds = new System.Collections.Generic.List<int>();
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT DISTINCT cr.Requested_By
                    FROM MonthEnd_Closure_Requests cr
                    INNER JOIN MonthEnd_Guard_Locations gl
                           ON gl.Location_Id = cr.Location_Id AND gl.Guard_Id = @GuardId
                    INNER JOIN MonthEnd_Guard_Schedule gs
                           ON gs.Guard_Id = @GuardId
                    WHERE cr.Created_At >= gs.Created_At", conn)) {
                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) userIds.Add((int)r["Requested_By"]);
                }

                var ctx = GlobalHost.ConnectionManager.GetHubContext<LocationHub>();
                foreach (int uid in userIds) {
                    InsertNotification(uid, "guard_closed", guardId, "La guardia ha finalizado");
                    ctx.Clients.Group("requester-" + uid).badgeUpdate();
                }
            } catch (Exception ex) {
                AppLogger.Error("LocationHub.NotifyGuardClosed", ex);
            }
        }

        // ── Locación revertida → notifica a managers ─────────────────
        public static void NotifyManagersLocationReverted(
                int locationId, string locationName, string callerName) {
            try {
                var managerIds = new System.Collections.Generic.List<int>();
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT User_Id FROM MonthEnd_Users
                    WHERE Role_Id >= 3 AND Active = 1 AND Locked = 0", conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) managerIds.Add((int)r["User_Id"]);
                }

                string message = $"{callerName} revirtió el cierre de {locationName} — requiere nueva revisión";
                var ctx = GlobalHost.ConnectionManager.GetHubContext<LocationHub>();
                foreach (int uid in managerIds) {
                    InsertNotification(uid, "location_reverted", locationId, message);
                }
                ctx.Clients.Group(ValidateGroup).badgeUpdate();
            } catch (Exception ex) {
                AppLogger.Error("LocationHub.NotifyManagersLocationReverted", ex);
            }
        }

        // ── Spot de guardia cambia (asignación / liberación) ─────────────
        // Notifica a Live.aspx para que actualice el banner de guardia
        public static void NotifySpotChanged(int guardId) {
            var ctx = GlobalHost.ConnectionManager.GetHubContext<LocationHub>();
            ctx.Clients.Group(LiveGroup).spotChanged(new { guardId });
        }
        public static void NotifyLocationUpdated(
                int locationId, string locationName,
                string newStatus, string reviewedBy) {

            var ctx = GlobalHost.ConnectionManager.GetHubContext<LocationHub>();
            ctx.Clients.Group(LiveGroup).locationUpdated(new {
                locationId, locationName, newStatus, reviewedBy
            });
        }

        // ── Helper: insertar notificación en BD ──────────────────────
        private static void InsertNotification(
                int userId, string type, int? referenceId, string message) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    IF NOT EXISTS (
                        SELECT 1 FROM MonthEnd_Notifications
                        WHERE User_Id     = @UserId
                          AND Type        = @Type
                          AND (Reference_Id = @RefId OR (@RefId IS NULL AND Reference_Id IS NULL))
                          AND Created_At >= DATEADD(second, -30, GETDATE())
                    )
                    INSERT INTO MonthEnd_Notifications (User_Id, Type, Reference_Id, Message)
                    VALUES (@UserId, @Type, @RefId, @Message)", conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@Type", type);
                    cmd.Parameters.AddWithValue("@RefId", referenceId.HasValue ? (object)referenceId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Message", message);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                AppLogger.Error("LocationHub.InsertNotification", ex);
            }
        }
    }
}