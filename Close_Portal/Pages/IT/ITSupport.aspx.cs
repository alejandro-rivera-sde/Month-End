using Close_Portal.Core;
using Close_Portal.DataAccess;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Services;

namespace Close_Portal.Pages.IT {

    /// <summary>
    /// Panel de IT Support — el agente ve y responde los casos del cierre activo.
    /// Requiere rol Administrador (3) o superior.
    ///
    /// WebMethods:
    ///   GetCases()               — lista de casos abiertos en el cierre activo
    ///   GetHistorial(clientId)   — mensajes del caso de ese cliente en el cierre activo
    ///
    /// El envío y recepción en tiempo real se gestiona en ChatHub.cs.
    /// </summary>
    public partial class ITSupportPage : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Administrador;

        protected void Page_Load(object sender, EventArgs e) { }

        // ── Helper de autorización para WebMethods ───────────────────────────

        private static bool TryGetAgent(out int userId) {
            var session = HttpContext.Current.Session;
            userId = 0;
            if (session["UserId"] == null) return false;
            userId = (int)session["UserId"];
            int roleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
            return roleId >= RoleLevel.Administrador;
        }

        // ── WebMethod: casos abiertos del cierre activo ──────────────────────

        /// <summary>
        /// Returns all open support cases for the current active guard,
        /// ordered by unread count then last activity.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object GetCases() {
            try {
                if (!TryGetAgent(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var da    = new ChatDataAccess();
                var cases = da.GetActiveCases();

                var dto = new List<object>();
                foreach (var c in cases) {
                    dto.Add(new {
                        caseId       = c.CaseId,
                        clientId     = c.ClientId,
                        clientName   = c.ClientName,
                        status       = c.Status,
                        lastMessage  = c.LastMessage,
                        lastActivity = c.LastActivity.ToString("yyyy-MM-ddTHH:mm:ss"),
                        unreadCount  = c.UnreadCount
                    });
                }

                return new { success = true, cases = dto };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ITSupport.GetCases] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ── WebMethod: historial de un caso ─────────────────────────────────

        /// <summary>
        /// Returns the message history for the support case belonging to
        /// <paramref name="clientId"/> in the current active guard.
        /// The server resolves the Case_Id — the client never passes it directly.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object GetHistorial(int clientId) {
            try {
                if (!TryGetAgent(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                if (clientId <= 0)
                    return new { success = false, message = "clientId inválido." };

                var da      = new ChatDataAccess();
                int guardId = da.GetActiveGuardId();
                int caseId  = da.GetCaseIdForClient(clientId, guardId);

                if (caseId <= 0)
                    return new { success = true, messages = new object[0] };

                var messages = da.GetHistorial(caseId);
                var dto      = new List<object>();
                foreach (var m in messages) {
                    dto.Add(new {
                        messageId  = m.MessageId,
                        caseId     = m.CaseId,
                        senderId   = m.SenderId,
                        senderName = m.SenderName,
                        message    = m.Message,
                        sentAt     = m.SentAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                        isRead     = m.IsRead,
                        isClient   = m.IsClient
                    });
                }

                return new { success = true, messages = dto };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ITSupport.GetHistorial] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }
    }
}
