using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace Close_Portal.DataAccess {

    // ── View models ──────────────────────────────────────────────────────────

    public class ChatMessageViewModel {
        public int      MessageId   { get; set; }
        public int      ClientId    { get; set; }
        public int      SenderId    { get; set; }
        public string   SenderName  { get; set; }
        public string   Message     { get; set; }
        public DateTime SentAt      { get; set; }
        public bool     IsRead      { get; set; }
        /// <summary>True if the sender is the client (for visual differentiation).</summary>
        public bool     IsClient    { get; set; }
    }

    public class ChatClientViewModel {
        public int      ClientId      { get; set; }
        public string   ClientName    { get; set; }
        public string   LastMessage   { get; set; }
        public DateTime LastActivity  { get; set; }
        public int      UnreadCount   { get; set; }
    }

    // ── Data access ──────────────────────────────────────────────────────────

    public class ChatDataAccess {

        private readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        /// <summary>
        /// Returns the conversation history for a given client, ordered chronologically.
        /// Limited to the most recent <paramref name="pageSize"/> messages.
        /// </summary>
        public List<ChatMessageViewModel> GetHistorial(int clientId, int pageSize = 100) {
            var result = new List<ChatMessageViewModel>();
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT TOP (@PageSize)
                        m.Message_Id,
                        m.Client_Id,
                        m.Sender_Id,
                        RTRIM(ISNULL(u.First_Name,'') + ' ' + ISNULL(u.Last_Name,'')) AS SenderName,
                        m.Message,
                        m.Sent_At,
                        m.Is_Read,
                        CASE WHEN m.Sender_Id = m.Client_Id THEN 1 ELSE 0 END AS IsClient
                    FROM  MonthEnd_Chat_Messages m
                    INNER JOIN MonthEnd_Users u ON u.User_Id = m.Sender_Id
                    WHERE m.Client_Id = @ClientId
                    ORDER BY m.Sent_At DESC", conn)) {

                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);
                    conn.Open();

                    using (var dr = cmd.ExecuteReader()) {
                        while (dr.Read()) {
                            result.Add(new ChatMessageViewModel {
                                MessageId  = dr.GetInt32(0),
                                ClientId   = dr.GetInt32(1),
                                SenderId   = dr.GetInt32(2),
                                SenderName = dr.GetString(3),
                                Message    = dr.GetString(4),
                                SentAt     = dr.GetDateTime(5),
                                IsRead     = dr.GetBoolean(6),
                                IsClient   = dr.GetInt32(7) == 1
                            });
                        }
                    }
                }
                // Query returns DESC (newest first); reverse to get chronological order
                result.Reverse();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ChatDataAccess.GetHistorial] ERROR: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Returns all clients who have sent at least one message,
        /// ordered by unread count (desc) then last activity (desc).
        /// Used by the IT Support agent panel to populate the conversation list.
        /// </summary>
        public List<ChatClientViewModel> GetClientesActivos() {
            var result = new List<ChatClientViewModel>();
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT
                        m.Client_Id,
                        RTRIM(ISNULL(u.First_Name,'') + ' ' + ISNULL(u.Last_Name,'')) AS ClientName,
                        MAX(m.Sent_At) AS LastActivity,
                        (
                            SELECT TOP 1 sub.Message
                            FROM   MonthEnd_Chat_Messages sub
                            WHERE  sub.Client_Id = m.Client_Id
                            ORDER  BY sub.Sent_At DESC
                        ) AS LastMessage,
                        SUM(CASE WHEN m.Is_Read = 0 AND m.Sender_Id = m.Client_Id THEN 1 ELSE 0 END)
                            AS UnreadCount
                    FROM  MonthEnd_Chat_Messages m
                    INNER JOIN MonthEnd_Users u ON u.User_Id = m.Client_Id
                    GROUP BY m.Client_Id, u.First_Name, u.Last_Name
                    ORDER BY UnreadCount DESC, LastActivity DESC", conn)) {

                    conn.Open();
                    using (var dr = cmd.ExecuteReader()) {
                        while (dr.Read()) {
                            result.Add(new ChatClientViewModel {
                                ClientId     = dr.GetInt32(0),
                                ClientName   = dr.GetString(1)?.Trim() ?? "Usuario",
                                LastActivity = dr.GetDateTime(2),
                                LastMessage  = dr.IsDBNull(3) ? "" : dr.GetString(3),
                                UnreadCount  = dr.GetInt32(4)
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
