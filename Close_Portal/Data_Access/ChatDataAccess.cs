using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace Close_Portal.DataAccess {

    // ── View models ──────────────────────────────────────────────────────────

    public class ChatMensajeViewModel {
        public int      Id          { get; set; }
        public int      ClienteId   { get; set; }
        public int      EmisorId    { get; set; }
        public string   EmisorNombre { get; set; }
        public string   Mensaje     { get; set; }
        public DateTime FechaHora   { get; set; }
        public bool     Leido       { get; set; }
        /// <summary>True si el emisor es el propio cliente (diferencia visual).</summary>
        public bool     EsCliente   { get; set; }
    }

    public class ChatClienteViewModel {
        public int      ClienteId         { get; set; }
        public string   ClienteNombre     { get; set; }
        public string   UltimoMensaje     { get; set; }
        public DateTime UltimaActividad   { get; set; }
        public int      MensajesNoLeidos  { get; set; }
    }

    // ── Data access ──────────────────────────────────────────────────────────

    public class ChatDataAccess {

        private readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        /// <summary>
        /// Retorna el historial de una conversación (identificada por clienteId),
        /// ordenado cronológicamente. Limita a los últimos <paramref name="pageSize"/>
        /// mensajes para no saturar la UI.
        /// </summary>
        public List<ChatMensajeViewModel> GetHistorial(int clienteId, int pageSize = 100) {
            var result = new List<ChatMensajeViewModel>();
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT TOP (@PageSize)
                        m.Id,
                        m.ClienteId,
                        m.EmisorId,
                        RTRIM(ISNULL(u.First_Name,'') + ' ' + ISNULL(u.Last_Name,'')) AS EmisorNombre,
                        m.Mensaje,
                        m.FechaHora,
                        m.Leido,
                        CASE WHEN m.EmisorId = m.ClienteId THEN 1 ELSE 0 END AS EsCliente
                    FROM  ChatMensajes m
                    INNER JOIN MonthEnd_Users u ON u.User_Id = m.EmisorId
                    WHERE m.ClienteId = @ClienteId
                    ORDER BY m.FechaHora DESC", conn)) {

                    cmd.Parameters.AddWithValue("@ClienteId", clienteId);
                    cmd.Parameters.AddWithValue("@PageSize",  pageSize);
                    conn.Open();

                    using (var dr = cmd.ExecuteReader()) {
                        while (dr.Read()) {
                            result.Add(new ChatMensajeViewModel {
                                Id           = dr.GetInt32(0),
                                ClienteId    = dr.GetInt32(1),
                                EmisorId     = dr.GetInt32(2),
                                EmisorNombre = dr.GetString(3),
                                Mensaje      = dr.GetString(4),
                                FechaHora    = dr.GetDateTime(5),
                                Leido        = dr.GetBoolean(6),
                                EsCliente    = dr.GetInt32(7) == 1
                            });
                        }
                    }
                }
                // Revertir: la query trae DESC (más recientes primero), necesitamos ASC (cronológico)
                result.Reverse();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ChatDataAccess.GetHistorial] ERROR: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Retorna todos los clientes que han enviado al menos un mensaje,
        /// ordenados por mensajes no leídos (desc) y última actividad (desc).
        /// Usado en el panel de IT Support para la lista lateral de conversaciones.
        /// </summary>
        public List<ChatClienteViewModel> GetClientesActivos() {
            var result = new List<ChatClienteViewModel>();
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT
                        m.ClienteId,
                        RTRIM(ISNULL(u.First_Name,'') + ' ' + ISNULL(u.Last_Name,'')) AS ClienteNombre,
                        MAX(m.FechaHora) AS UltimaActividad,
                        (
                            SELECT TOP 1 Mensaje
                            FROM   ChatMensajes sub
                            WHERE  sub.ClienteId = m.ClienteId
                            ORDER  BY sub.FechaHora DESC
                        ) AS UltimoMensaje,
                        SUM(CASE WHEN m.Leido = 0 AND m.EmisorId = m.ClienteId THEN 1 ELSE 0 END)
                            AS MensajesNoLeidos
                    FROM  ChatMensajes m
                    INNER JOIN MonthEnd_Users u ON u.User_Id = m.ClienteId
                    GROUP BY m.ClienteId, u.First_Name, u.Last_Name
                    ORDER BY MensajesNoLeidos DESC, UltimaActividad DESC", conn)) {

                    conn.Open();
                    using (var dr = cmd.ExecuteReader()) {
                        while (dr.Read()) {
                            result.Add(new ChatClienteViewModel {
                                ClienteId        = dr.GetInt32(0),
                                ClienteNombre    = dr.GetString(1)?.Trim() ?? "Usuario",
                                UltimaActividad  = dr.GetDateTime(2),
                                UltimoMensaje    = dr.IsDBNull(3) ? "" : dr.GetString(3),
                                MensajesNoLeidos = dr.GetInt32(4)
                            });
                        }
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ChatDataAccess.GetClientesActivos] ERROR: {ex.Message}");
            }
            return result;
        }
    }
}
