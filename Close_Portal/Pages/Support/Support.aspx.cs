using Close_Portal.Core;
using Close_Portal.DataAccess;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Services;

namespace Close_Portal.Pages.Support {

    /// <summary>
    /// Página de chat del cliente con IT Support.
    /// Accesible a cualquier usuario autenticado (RoleLevel.Regular).
    ///
    /// Paso 3 — Frontend:
    ///   - GetHistorial(): carga el historial al abrir el chat (llamado desde it_chat.js).
    ///   El envío y recepción de mensajes en tiempo real se gestiona desde ChatHub.cs.
    /// </summary>
    public partial class SupportPage : SecurePage {

        // Cualquier usuario autenticado puede abrir el chat con IT
        protected override int RequiredRoleId => RoleLevel.Regular;

        protected void Page_Load(object sender, EventArgs e) {
            // Sin lógica de servidor en Page_Load; toda la UI la monta it_chat.js
        }

        // ── WebMethod: historial de la conversación del usuario actual ──

        /// <summary>
        /// Retorna los últimos 100 mensajes de la conversación del usuario
        /// autenticado con IT Support.
        ///
        /// La identidad del cliente se lee siempre desde Session["UserId"];
        /// nunca se acepta un userId del frontend.
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object GetHistorial() {
            try {
                var session = HttpContext.Current.Session;
                if (session["UserId"] == null)
                    return new { success = false, message = "Sesión expirada." };

                int clientId = (int)session["UserId"];
                var da       = new ChatDataAccess();
                var messages = da.GetHistorial(clientId);

                var dto = new List<object>();
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
                System.Diagnostics.Debug.WriteLine($"[Support.GetHistorial] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }
    }
}
