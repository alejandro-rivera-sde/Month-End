using Microsoft.AspNet.SignalR;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Close_Portal.Hubs {

    /// <summary>
    /// Hub de SignalR para chat en tiempo real entre usuarios (cliente)
    /// y agentes de IT Support (Administrador o Owner).
    ///
    /// Seguridad: la identidad siempre se lee desde Session del servidor,
    /// nunca desde parámetros del cliente.
    ///
    /// Grupos SignalR:
    ///   "user-{userId}"  → recibe mensajes dirigidos a ese usuario
    ///   "it-agents"      → todos los agentes IT conectados (RoleId >= 3)
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

        // ── OnConnected: suscribir a grupos según rol ─────────────────

        public override Task OnConnected() {
            int userId = GetCurrentUserId();
            if (userId > 0) {
                // Grupo personal: recibe mensajes directos de agentes IT
                Groups.Add(Context.ConnectionId, "user-" + userId);

                // Agentes IT (Administrador=3, Owner=4): también escuchan el grupo global
                if (GetCurrentRoleId() >= Core.RoleLevel.Administrador) {
                    Groups.Add(Context.ConnectionId, "it-agents");
                }
            }
            return base.OnConnected();
        }

        // SignalR elimina la conexión de todos los grupos al desconectar
        public override Task OnDisconnected(bool stopCalled) {
            return base.OnDisconnected(stopCalled);
        }

        // ── Paso 2a: Cliente → IT Support ────────────────────────────
        // Llamado desde Support.aspx — cualquier usuario autenticado

        public void EnviarMensajeAIT(string mensaje) {
            int emisorId = GetCurrentUserId();
            if (emisorId <= 0) return;
            if (string.IsNullOrWhiteSpace(mensaje)) return;

            // Límite de longitud para evitar abuso
            if (mensaje.Length > 2000) mensaje = mensaje.Substring(0, 2000);

            string nombre   = GetCurrentFullName();
            var    timestamp = DateTime.Now;

            int mensajeId = GuardarMensaje(clienteId: emisorId, emisorId: emisorId,
                                           mensaje:   mensaje.Trim());

            // Push a todos los agentes IT conectados
            Clients.Group("it-agents").recibirMensajeDeCliente(new {
                mensajeId,
                clienteId     = emisorId,
                clienteNombre = nombre,
                mensaje       = mensaje.Trim(),
                fechaHora     = timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                esCliente     = true
            });
        }

        // ── Paso 2b: IT Agente → Cliente específico ──────────────────
        // Llamado desde ITSupport.aspx — solo Administrador+

        public void EnviarMensajeACliente(int clienteId, string mensaje) {
            int emisorId = GetCurrentUserId();
            // Validar rol en el servidor — el cliente no puede elevar sus permisos
            if (emisorId <= 0 || GetCurrentRoleId() < Core.RoleLevel.Administrador) return;
            if (clienteId <= 0 || string.IsNullOrWhiteSpace(mensaje)) return;

            if (mensaje.Length > 2000) mensaje = mensaje.Substring(0, 2000);

            string nombre    = GetCurrentFullName();
            var    timestamp = DateTime.Now;

            int mensajeId = GuardarMensaje(clienteId: clienteId, emisorId: emisorId,
                                           mensaje:   mensaje.Trim());

            // Push al cliente específico (grupo personal)
            Clients.Group("user-" + clienteId).recibirRespuestaIT(new {
                mensajeId,
                agenteNombre = nombre,
                mensaje      = mensaje.Trim(),
                fechaHora    = timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                esCliente    = false
            });

            // Sincronizar a otros agentes IT para que vean la respuesta en tiempo real
            Clients.Group("it-agents").mensajeEnviado(new {
                mensajeId,
                clienteId,
                agenteId     = emisorId,
                agenteNombre = nombre,
                mensaje      = mensaje.Trim(),
                fechaHora    = timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                esCliente    = false
            });
        }

        // ── Marcar mensajes de un cliente como leídos ─────────────────
        // Llamado desde ITSupport.aspx al abrir una conversación

        public void MarcarLeido(int clienteId) {
            if (GetCurrentRoleId() < Core.RoleLevel.Administrador) return;
            if (clienteId <= 0) return;
            ActualizarLeido(clienteId);
        }

        // ── BD: insertar mensaje ───────────────────────────────────────

        private int GuardarMensaje(int clienteId, int emisorId, string mensaje) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    INSERT INTO ChatMensajes (ClienteId, EmisorId, Mensaje)
                    OUTPUT INSERTED.Id
                    VALUES (@ClienteId, @EmisorId, @Mensaje)", conn)) {
                    cmd.Parameters.AddWithValue("@ClienteId", clienteId);
                    cmd.Parameters.AddWithValue("@EmisorId",  emisorId);
                    cmd.Parameters.AddWithValue("@Mensaje",   mensaje);
                    conn.Open();
                    return (int)cmd.ExecuteScalar();
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ChatHub.GuardarMensaje] ERROR: {ex.Message}");
                return -1;
            }
        }

        // ── BD: marcar mensajes del cliente como leídos por el agente ─

        private void ActualizarLeido(int clienteId) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    UPDATE ChatMensajes
                    SET    Leido = 1
                    WHERE  ClienteId = @ClienteId
                      AND  EmisorId  = @ClienteId
                      AND  Leido     = 0", conn)) {
                    cmd.Parameters.AddWithValue("@ClienteId", clienteId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ChatHub.ActualizarLeido] ERROR: {ex.Message}");
            }
        }
    }
}
