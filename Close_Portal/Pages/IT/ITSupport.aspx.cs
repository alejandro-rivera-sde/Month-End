using Close_Portal.Core;
using Close_Portal.DataAccess;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Services;

namespace Close_Portal.Pages.IT {

    /// <summary>
    /// Panel de IT Support — agentes responden a los chats de los clientes.
    /// Requiere rol Administrador (3) o superior.
    ///
    /// Paso 2 (Servidor) / Paso 3 (Frontend):
    ///   - GetClientes(): lista de clientes con mensajes (la lógica de mensajes
    ///     en tiempo real está en ChatHub.cs, no aquí).
    ///   - GetHistorial(clienteId): historial de una conversación específica.
    ///
    /// SEGURIDAD: todos los WebMethods validan el rol en el servidor.
    /// </summary>
    public partial class ITSupportPage : SecurePage {

        // Solo Administrador (3) y Owner (4) acceden a esta página
        protected override int RequiredRoleId => RoleLevel.Administrador;

        protected void Page_Load(object sender, EventArgs e) {
            // Sin lógica en Page_Load; la UI la monta it_chat.js
        }

        // ── Helper de autorización para WebMethods ──────────────────

        private static bool TryGetAgent(out int userId) {
            var session = HttpContext.Current.Session;
            userId = 0;
            if (session["UserId"] == null) return false;
            userId = (int)session["UserId"];
            int roleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
            return roleId >= RoleLevel.Administrador;
        }

        // ── WebMethod: lista de clientes con conversaciones ─────────

        /// <summary>
        /// Retorna todos los clientes que han enviado mensajes, ordenados por
        /// mensajes no leídos (desc) y última actividad (desc).
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object GetClientes() {
            try {
                if (!TryGetAgent(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var da       = new ChatDataAccess();
                var clientes = da.GetClientesActivos();

                var dto = new List<object>();
                foreach (var c in clientes) {
                    dto.Add(new {
                        clientId     = c.ClientId,
                        clientName   = c.ClientName,
                        lastMessage  = c.LastMessage,
                        lastActivity = c.LastActivity.ToString("yyyy-MM-ddTHH:mm:ss"),
                        unreadCount  = c.UnreadCount
                    });
                }

                return new { success = true, clients = dto };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ITSupport.GetClientes] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ── WebMethod: historial de una conversación ────────────────

        /// <summary>
        /// Retorna los mensajes de la conversación entre IT y el cliente
        /// identificado por <paramref name="clienteId"/>.
        ///
        /// Solo accesible a agentes IT (Administrador+).
        /// </summary>
        [WebMethod(EnableSession = true)]
        public static object GetHistorial(int clientId) {
            try {
                if (!TryGetAgent(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                if (clientId <= 0)
                    return new { success = false, message = "clientId inválido." };

                var da       = new ChatDataAccess();
                var messages = da.GetHistorial(clientId);

                var dto = new List<object>();
                foreach (var m in messages) {
                    dto.Add(new {
                        messageId  = m.MessageId,
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
