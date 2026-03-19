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

            string statusLabel = newStatus == "Approved" ? "aprobada" : "rechazada";
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

        // ── Locación cambia estado → notifica Live ───────────────────
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
                    INSERT INTO Notifications (User_Id, Type, Reference_Id, Message)
                    VALUES (@UserId, @Type, @RefId, @Message)", conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@Type", type);
                    cmd.Parameters.AddWithValue("@RefId", referenceId.HasValue ? (object)referenceId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Message", message);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[LocationHub.InsertNotification] ERROR: {ex.Message}");
            }
        }
    }
}