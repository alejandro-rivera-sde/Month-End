using Close_Portal.DataAccess;
using Microsoft.AspNet.SignalR;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Close_Portal.Hubs {

    /// <summary>
    /// Hub de SignalR para chat en tiempo real entre usuarios (cliente)
    /// y el agente de IT Support (Administrador o Owner en ITSupport.aspx).
    ///
    /// Modelo de datos:
    ///   Cada conversación es un "caso" (MonthEnd_Support_Cases) vinculado al
    ///   guard/cierre activo. Los mensajes se guardan en MonthEnd_Chat_Messages
    ///   con referencia al Case_Id. Los casos persisten durante todo el cierre.
    ///
    /// Seguridad: la identidad siempre se lee desde Session del servidor,
    /// nunca desde parámetros del cliente.
    ///
    /// Grupos SignalR:
    ///   "user-{userId}"  → recibe mensajes dirigidos a ese usuario
    ///   "it-agents"      → solo el agente activo en ITSupport.aspx
    /// </summary>
    public class ChatHub : Hub {

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        // ── Helpers: identidad desde Session (inmune a manipulación cliente) ──

        private int GetCurrentUserId() {
            var session = Context.Request.GetHttpContext()?.Session;
            return session?["UserId"] != null ? (int)session["UserId"] : -1;
        }

        private int GetCurrentRoleId() {
            var session = Context.Request.GetHttpContext()?.Session;
            return session?["RoleId"] != null ? (int)session["RoleId"] : -1;
        }

        private string GetCurrentFullName() {
            var session = Context.Request.GetHttpContext()?.Session;
            return session?["FullName"]?.ToString() ?? "Usuario";
        }

        // ── OnConnected: une al grupo personal ───────────────────────────────

        public override Task OnConnected() {
            JoinGroups();
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled) {
            return base.OnDisconnected(stopCalled);
        }

        // ── JoinGroups: grupo personal "user-{id}" (todos los usuarios) ──────
        // Llamado desde OnConnected() y explícitamente desde el cliente
        // porque Session puede no estar disponible en OnConnected() con OWIN.

        public void JoinGroups() {
            int userId = GetCurrentUserId();
            if (userId <= 0) return;
            Groups.Add(Context.ConnectionId, "user-" + userId);
        }

        // ── RegisterAsITAgent: grupo "it-agents" (solo ITSupport.aspx) ───────
        // Solo la persona en el panel de IT se suscribe a este grupo,
        // asegurando que únicamente el agente activo recibe las notificaciones.

        public void RegisterAsITAgent() {
            if (GetCurrentRoleId() < Core.RoleLevel.Administrador) return;
            Groups.Add(Context.ConnectionId, "it-agents");
        }

        // ── Paso 2a: Cliente → IT Support ────────────────────────────────────
        // Cualquier usuario autenticado puede enviar un mensaje.
        // Crea o reutiliza el caso del cierre activo para este cliente.

        public void EnviarMensajeAIT(string mensaje) {
            int emisorId = GetCurrentUserId();
            if (emisorId <= 0) return;
            if (string.IsNullOrWhiteSpace(mensaje)) return;
            if (mensaje.Length > 2000) mensaje = mensaje.Substring(0, 2000);

            string nombre    = GetCurrentFullName();
            var    timestamp = DateTime.Now;
            var    da        = new ChatDataAccess();

            int guardId = da.GetActiveGuardId();
            int caseId  = da.GetOrCreateCase(clientId: emisorId, guardId: guardId);
            if (caseId <= 0) return;

            int messageId = GuardarMensaje(caseId: caseId, senderId: emisorId,
                                           message: mensaje.Trim());

            Clients.Group("it-agents").recibirMensajeDeCliente(new {
                messageId,
                caseId,
                clientId   = emisorId,
                clientName = nombre,
                message    = mensaje.Trim(),
                sentAt     = timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                isClient   = true
            });
        }

        // ── Paso 2b: IT Agente → Cliente específico ───────────────────────────
        // Solo Administrador+. El servidor resuelve el caseId desde clientId
        // y el guard activo — el cliente no puede manipular este valor.

        public void EnviarMensajeACliente(int clientId, string mensaje) {
            int emisorId = GetCurrentUserId();
            if (emisorId <= 0 || GetCurrentRoleId() < Core.RoleLevel.Administrador) return;
            if (clientId <= 0 || string.IsNullOrWhiteSpace(mensaje)) return;
            if (mensaje.Length > 2000) mensaje = mensaje.Substring(0, 2000);

            string nombre    = GetCurrentFullName();
            var    timestamp = DateTime.Now;
            var    da        = new ChatDataAccess();

            int guardId = da.GetActiveGuardId();
            int caseId  = da.GetCaseIdForClient(clientId: clientId, guardId: guardId);
            if (caseId <= 0) return; // no hay caso activo para este cliente

            int messageId = GuardarMensaje(caseId: caseId, senderId: emisorId,
                                           message: mensaje.Trim());

            // Respuesta al cliente específico
            Clients.Group("user-" + clientId).recibirRespuestaIT(new {
                messageId,
                caseId,
                senderName = nombre,
                message    = mensaje.Trim(),
                sentAt     = timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                isClient   = false
            });

            // Sincronizar al propio panel IT (por si hay más de una pestaña abierta)
            Clients.Group("it-agents").mensajeEnviado(new {
                messageId,
                caseId,
                clientId,
                senderId   = emisorId,
                senderName = nombre,
                message    = mensaje.Trim(),
                sentAt     = timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                isClient   = false
            });
        }

        // ── Marcar caso como leído ────────────────────────────────────────────
        // Llamado desde ITSupport.aspx al abrir un caso.

        public void MarcarLeido(int caseId) {
            if (GetCurrentRoleId() < Core.RoleLevel.Administrador) return;
            if (caseId <= 0) return;
            ActualizarLeido(caseId);
        }

        // ── BD: insertar mensaje ──────────────────────────────────────────────

        private int GuardarMensaje(int caseId, int senderId, string message) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    INSERT INTO MonthEnd_Chat_Messages (Case_Id, Sender_Id, Message)
                    OUTPUT INSERTED.Message_Id
                    VALUES (@CaseId, @SenderId, @Message)", conn)) {
                    cmd.Parameters.AddWithValue("@CaseId",   caseId);
                    cmd.Parameters.AddWithValue("@SenderId", senderId);
                    cmd.Parameters.AddWithValue("@Message",  message);
                    conn.Open();
                    return (int)cmd.ExecuteScalar();
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ChatHub.GuardarMensaje] ERROR: {ex.Message}");
                return -1;
            }
        }

        // ── BD: marcar mensajes del cliente en el caso como leídos ───────────

        private void ActualizarLeido(int caseId) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    UPDATE m
                    SET    m.Is_Read = 1
                    FROM   MonthEnd_Chat_Messages m
                    INNER JOIN MonthEnd_Support_Cases c ON c.Case_Id = m.Case_Id
                    WHERE  m.Case_Id   = @CaseId
                      AND  m.Sender_Id = c.Client_Id
                      AND  m.Is_Read   = 0", conn)) {
                    cmd.Parameters.AddWithValue("@CaseId", caseId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ChatHub.ActualizarLeido] ERROR: {ex.Message}");
            }
        }
    }
}
