using Close_Portal.Core;
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

        // ── Helpers: identidad del usuario ───────────────────────────────────
        //
        // Intenta Session primero. Si no está disponible (habitual en OWIN cuando
        // el pipeline no inicializa Session para conexiones SignalR), usa el
        // query string "chatUserId" que it_chat.js establece antes de hub.start()
        // con el valor server-rendered de window.CurrentUserId.
        //
        // El rol nunca se toma del cliente: siempre se valida en BD.

        private int GetCurrentUserId() {
            var session = Context.Request.GetHttpContext()?.Session;
            if (session?["UserId"] != null) return (int)session["UserId"];

            // Fallback: QS establecido por it_chat.js desde window.CurrentUserId (server-rendered).
            // Se valida contra BD para evitar suplantación de identidad: solo se acepta
            // si el usuario existe, está activo y no está bloqueado.
            if (int.TryParse(Context.QueryString["chatUserId"], out int qsId) && qsId > 0
                && IsActiveUser(qsId))
                return qsId;
            return -1;
        }

        private bool IsActiveUser(int userId) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(1) FROM MonthEnd_Users WHERE User_Id=@Id AND Active=1 AND Locked=0", conn)) {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    conn.Open();
                    return (int)cmd.ExecuteScalar() == 1;
                }
            } catch (Exception ex) { AppLogger.Error("ChatHub.IsActiveUser", ex); return false; }
        }

        private int GetCurrentRoleId() {
            var session = Context.Request.GetHttpContext()?.Session;
            if (session?["RoleId"] != null) return (int)session["RoleId"];

            // Fallback: consultar BD — el rol nunca se acepta del cliente
            return GetRoleFromDb(GetCurrentUserId());
        }

        private string GetCurrentFullName() {
            var session = Context.Request.GetHttpContext()?.Session;
            if (session?["FullName"] != null) return session["FullName"].ToString();

            return GetFullNameFromDb(GetCurrentUserId());
        }

        private int GetRoleFromDb(int userId) {
            if (userId <= 0) return -1;
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(
                    "SELECT Role_Id FROM MonthEnd_Users WHERE User_Id = @Id AND Active = 1", conn)) {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    conn.Open();
                    var r = cmd.ExecuteScalar();
                    return r != null ? (int)r : -1;
                }
            } catch (Exception ex) { AppLogger.Error("ChatHub.GetRoleFromDb", ex); return -1; }
        }

        private string GetFullNameFromDb(int userId) {
            if (userId <= 0) return "Usuario";
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(
                    @"SELECT RTRIM(ISNULL(First_Name,'') + ' ' + ISNULL(Last_Name,''))
                      FROM   MonthEnd_Users WHERE User_Id = @Id", conn)) {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    conn.Open();
                    var r = cmd.ExecuteScalar();
                    return r?.ToString()?.Trim() is string s && s.Length > 0 ? s : "Usuario";
                }
            } catch (Exception ex) { AppLogger.Error("ChatHub.GetFullNameFromDb", ex); return "Usuario"; }
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
            int userId = GetCurrentUserId();
            if (userId <= 0 || GetCurrentRoleId() < Core.RoleLevel.Administrador) return;
            if (!IsItDept(userId)) return;
            Groups.Add(Context.ConnectionId, "it-agents");
        }

        private bool IsItDept(int userId) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT d.Department_Code
                    FROM   MonthEnd_Users u
                    INNER JOIN MonthEnd_Departments d ON d.Department_Id = u.Department_Id
                    WHERE  u.User_Id = @Id AND u.Active = 1 AND u.Locked = 0", conn)) {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    conn.Open();
                    var r = cmd.ExecuteScalar();
                    return r != null && string.Equals(r.ToString(), "IT", StringComparison.OrdinalIgnoreCase);
                }
            } catch (Exception ex) { AppLogger.Error("ChatHub.IsItDept", ex); return false; }
        }

        // ── Paso 2a: Cliente → IT Support ────────────────────────────────────
        // Cualquier usuario autenticado puede enviar un mensaje.
        // Crea o reutiliza el caso del cierre activo para este cliente.
        //
        // ENRUTAMIENTO: el mensaje se envía al grupo personal del agente IT
        // asignado al spot de la guardia activa ("user-{itAgentId}").
        // Solo esa persona recibe la notificación, no cualquier admin que esté
        // conectado. Si el spot aún no tiene agente asignado, se usa "it-agents"
        // como fallback para no perder el mensaje.

        public void EnviarMensajeAIT(string mensaje) {
            int emisorId = GetCurrentUserId();
            if (emisorId <= 0) return;
            if (string.IsNullOrWhiteSpace(mensaje)) return;
            if (mensaje.Length > 2000) mensaje = mensaje.Substring(0, 2000);

            string nombre    = GetCurrentFullName();
            var    timestamp = DateTime.Now;
            var    da        = new ChatDataAccess();

            int guardId   = da.GetActiveGuardId();
            int caseId    = da.GetOrCreateCase(clientId: emisorId, guardId: guardId);
            if (caseId <= 0) return;

            int messageId = GuardarMensaje(caseId: caseId, senderId: emisorId,
                                           message: mensaje.Trim());

            var payload = new {
                messageId,
                caseId,
                clientId   = emisorId,
                clientName = nombre,
                message    = mensaje.Trim(),
                sentAt     = timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                isClient   = true
            };

            // Enrutar al agente IT del spot de la guardia activa
            int itAgentId = da.GetActiveITAgentId(guardId);
            if (itAgentId > 0) {
                Clients.Group("user-" + itAgentId).recibirMensajeDeCliente(payload);
            } else {
                // Fallback: spot vacío — notificar a cualquier admin registrado
                Clients.Group("it-agents").recibirMensajeDeCliente(payload);
            }
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

            // Sincronizar al propio agente (otras pestañas abiertas del mismo usuario)
            // Se usa el grupo personal "user-{emisorId}" para no notificar a otros admins.
            Clients.Group("user-" + emisorId).mensajeEnviado(new {
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
                AppLogger.Error("ChatHub.GuardarMensaje", ex);
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
                AppLogger.Error("ChatHub.ActualizarLeido", ex);
            }
        }
    }
}
