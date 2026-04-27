using Close_Portal.Core;
using Close_Portal.DataAccess;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Services;

namespace Close_Portal.Pages.Support {

    /// <summary>
    /// Página de chat del cliente con IT Support.
    /// Accesible a cualquier usuario autenticado.
    ///
    /// WebMethod:
    ///   GetHistorial() — carga el historial del caso activo del usuario
    ///                    (caso del cierre activo, si existe).
    ///
    /// El envío y recepción en tiempo real se gestiona en ChatHub.cs.
    /// </summary>
    public partial class SupportPage : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Regular;

        protected void Page_Load(object sender, EventArgs e) { }

        // ── WebMethod: historial del caso activo del usuario ─────────────────

        /// <summary>
        /// Returns the message history for the current user's open support case
        /// in the active guard. Returns empty list if no case exists yet
        /// (the case is created lazily on first message send via ChatHub).
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object GetHistorial() {
            try {
                var session = HttpContext.Current.Session;
                if (session["UserId"] == null)
                    return new { success = false, message = "Sesión expirada." };

                int clientId = (int)session["UserId"];
                var da       = new ChatDataAccess();
                int guardId  = da.GetActiveGuardId();
                int caseId   = da.GetCaseIdForClient(clientId, guardId);

                // No case yet — return empty history (case is created on first send)
                if (caseId <= 0)
                    return new { success = true, messages = new object[0] };

                var messages = da.GetHistorial(caseId);
                var dto      = new List<object>();
                foreach (var m in messages) {
                    dto.Add(new {
                        messageId  = m.MessageId,
                        senderName = m.SenderName,
                        message    = m.Message,
                        sentAt     = m.SentAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                        isRead     = m.IsRead,
                        isClient   = m.IsClient
                    });
                }

                return new { success = true, messages = dto };

            } catch (Exception ex) {
                AppLogger.Error("Support.GetHistorial", ex);
                return new { success = false, message = ex.Message };
            }
        }
    }
}
