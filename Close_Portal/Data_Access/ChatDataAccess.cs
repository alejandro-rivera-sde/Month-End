using Close_Portal.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace Close_Portal.DataAccess {

    // ── View models ──────────────────────────────────────────────────────────

    public class ChatMessageViewModel {
        public int      MessageId  { get; set; }
        public int      CaseId     { get; set; }
        public int      SenderId   { get; set; }
        public string   SenderName { get; set; }
        public string   Message    { get; set; }
        public DateTime SentAt     { get; set; }
        public bool     IsRead     { get; set; }
        /// <summary>True if the sender is the client (for visual differentiation).</summary>
        public bool     IsClient   { get; set; }
    }

    public class SupportCaseViewModel {
        public int      CaseId       { get; set; }
        public int      ClientId     { get; set; }
        public string   ClientName   { get; set; }
        public string   Status       { get; set; }
        public string   LastMessage  { get; set; }
        public DateTime LastActivity { get; set; }
        public int      UnreadCount  { get; set; }
    }

    // ── Data access ──────────────────────────────────────────────────────────

    public class ChatDataAccess {

        private readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        // ── Guard activo ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns the Guard_Id of the currently active guard (End_Time IS NULL).
        /// Returns 0 if no guard is active — cases are still created with Guard_Id = 0
        /// so chat works even outside a formal guard cycle.
        /// </summary>
        public int GetActiveGuardId() {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 Guard_Id
                    FROM   MonthEnd_Guard_Schedule
                    WHERE  End_Time IS NULL
                    ORDER  BY Created_At DESC", conn)) {
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    return result != null ? (int)result : 0;
                }
            } catch (Exception ex) {
                AppLogger.Error("ChatDataAccess.GetActiveGuardId", ex);
                return 0;
            }
        }

        /// <summary>
        /// Returns the User_Id of the IT agent assigned to a spot in the given guard.
        /// "IT agent" = the spot user whose Role_Id >= 3 (Administrador or Owner),
        /// matching the same criteria used by RegisterAsITAgent() on the client.
        /// Returns 0 if the guard has no such spot filled (not yet assigned, etc.).
        /// Falls back to 0 when guardId is 0 (no active guard).
        /// </summary>
        public int GetActiveITAgentId(int guardId) {
            if (guardId <= 0) return 0;
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 gs.User_Id
                    FROM   MonthEnd_Guard_Spots gs
                    INNER JOIN MonthEnd_Users u ON u.User_Id = gs.User_Id
                    WHERE  gs.Guard_Id  = @GuardId
                      AND  gs.User_Id   IS NOT NULL
                      AND  u.Role_Id   >= 3
                      AND  u.Active     = 1
                      AND  u.Locked     = 0", conn)) {
                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    return result != null ? (int)result : 0;
                }
            } catch (Exception ex) {
                AppLogger.Error("ChatDataAccess.GetActiveITAgentId", ex);
                return 0;
            }
        }

        // ── Casos de soporte ─────────────────────────────────────────────────

        /// <summary>
        /// Returns the Case_Id for the given client + guard, creating a new case if
        /// none exists. Also updates Updated_At on the case when a new message arrives.
        /// Returns 0 on error.
        /// </summary>
        public int GetOrCreateCase(int clientId, int guardId) {
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // Try to get existing case first
                    using (var sel = new SqlCommand(@"
                        SELECT Case_Id FROM MonthEnd_Support_Cases
                        WHERE  Client_Id = @ClientId AND Guard_Id = @GuardId", conn)) {
                        sel.Parameters.AddWithValue("@ClientId", clientId);
                        sel.Parameters.AddWithValue("@GuardId",  guardId);
                        var existing = sel.ExecuteScalar();
                        if (existing != null) {
                            int existingId = (int)existing;
                            // Update timestamp so IT panel sorts correctly
                            UpdateCaseTimestamp(existingId, conn);
                            return existingId;
                        }
                    }

                    // Create new case
                    using (var ins = new SqlCommand(@"
                        INSERT INTO MonthEnd_Support_Cases (Client_Id, Guard_Id, Status)
                        OUTPUT INSERTED.Case_Id
                        VALUES (@ClientId, @GuardId, 'Open')", conn)) {
                        ins.Parameters.AddWithValue("@ClientId", clientId);
                        ins.Parameters.AddWithValue("@GuardId",  guardId);
                        return (int)ins.ExecuteScalar();
                    }
                }
            } catch (Exception ex) {
                AppLogger.Error("ChatDataAccess.GetOrCreateCase", ex);
                return 0;
            }
        }

        /// <summary>
        /// Returns the Case_Id for the given client + guard WITHOUT creating a new one.
        /// Returns 0 if no case exists.
        /// </summary>
        public int GetCaseIdForClient(int clientId, int guardId) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT Case_Id FROM MonthEnd_Support_Cases
                    WHERE  Client_Id = @ClientId AND Guard_Id = @GuardId", conn)) {
                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    cmd.Parameters.AddWithValue("@GuardId",  guardId);
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    return result != null ? (int)result : 0;
                }
            } catch (Exception ex) {
                AppLogger.Error("ChatDataAccess.GetCaseIdForClient", ex);
                return 0;
            }
        }

        private void UpdateCaseTimestamp(int caseId, SqlConnection conn) {
            using (var cmd = new SqlCommand(@"
                UPDATE MonthEnd_Support_Cases SET Updated_At = GETDATE()
                WHERE  Case_Id = @CaseId", conn)) {
                cmd.Parameters.AddWithValue("@CaseId", caseId);
                cmd.ExecuteNonQuery();
            }
        }

        // ── Mensajes ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the conversation history for a case, ordered chronologically.
        /// </summary>
        public List<ChatMessageViewModel> GetHistorial(int caseId, int pageSize = 100) {
            var result = new List<ChatMessageViewModel>();
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT TOP (@PageSize)
                        m.Message_Id,
                        m.Case_Id,
                        m.Sender_Id,
                        RTRIM(ISNULL(u.First_Name,'') + ' ' + ISNULL(u.Last_Name,'')) AS SenderName,
                        m.Message,
                        m.Sent_At,
                        m.Is_Read,
                        CASE WHEN m.Sender_Id = c.Client_Id THEN 1 ELSE 0 END AS IsClient
                    FROM  MonthEnd_Chat_Messages m
                    INNER JOIN MonthEnd_Support_Cases c ON c.Case_Id = m.Case_Id
                    INNER JOIN MonthEnd_Users         u ON u.User_Id = m.Sender_Id
                    WHERE m.Case_Id = @CaseId
                    ORDER BY m.Sent_At DESC", conn)) {

                    cmd.Parameters.AddWithValue("@CaseId",   caseId);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);
                    conn.Open();

                    using (var dr = cmd.ExecuteReader()) {
                        while (dr.Read()) {
                            result.Add(new ChatMessageViewModel {
                                MessageId  = dr.GetInt32(0),
                                CaseId     = dr.GetInt32(1),
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
                AppLogger.Error("ChatDataAccess.GetHistorial", ex);
            }
            return result;
        }

        // ── Lista de casos activos (panel IT) ────────────────────────────────

        /// <summary>
        /// Returns all open support cases for the current active guard,
        /// ordered by unread count (desc) then last activity (desc).
        /// Used by the IT Support agent panel to populate the conversation list.
        /// </summary>
        public List<SupportCaseViewModel> GetActiveCases() {
            var result = new List<SupportCaseViewModel>();
            try {
                int guardId = GetActiveGuardId();

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT
                        sc.Case_Id,
                        sc.Client_Id,
                        RTRIM(ISNULL(u.First_Name,'') + ' ' + ISNULL(u.Last_Name,'')) AS ClientName,
                        sc.Status,
                        sc.Updated_At AS LastActivity,
                        (
                            SELECT TOP 1 m2.Message
                            FROM   MonthEnd_Chat_Messages m2
                            WHERE  m2.Case_Id = sc.Case_Id
                            ORDER  BY m2.Sent_At DESC
                        ) AS LastMessage,
                        (
                            SELECT COUNT(*)
                            FROM   MonthEnd_Chat_Messages m3
                            WHERE  m3.Case_Id   = sc.Case_Id
                              AND  m3.Sender_Id = sc.Client_Id
                              AND  m3.Is_Read   = 0
                        ) AS UnreadCount
                    FROM  MonthEnd_Support_Cases sc
                    INNER JOIN MonthEnd_Users u ON u.User_Id = sc.Client_Id
                    WHERE sc.Guard_Id = @GuardId
                      AND sc.Status  = 'Open'
                    ORDER BY UnreadCount DESC, sc.Updated_At DESC", conn)) {

                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                    conn.Open();

                    using (var dr = cmd.ExecuteReader()) {
                        while (dr.Read()) {
                            result.Add(new SupportCaseViewModel {
                                CaseId       = dr.GetInt32(0),
                                ClientId     = dr.GetInt32(1),
                                ClientName   = dr.GetString(2)?.Trim() ?? "Usuario",
                                Status       = dr.GetString(3),
                                LastActivity = dr.GetDateTime(4),
                                LastMessage  = dr.IsDBNull(5) ? "" : dr.GetString(5),
                                UnreadCount  = dr.GetInt32(6)
                            });
                        }
                    }
                }
            } catch (Exception ex) {
                AppLogger.Error("ChatDataAccess.GetActiveCases", ex);
            }
            return result;
        }
    }
}
