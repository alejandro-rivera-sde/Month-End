using Close_Portal.Core;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;

namespace Close_Portal.Services {
    public class SecurityLogService {
        private readonly string _connectionString;

        private const int MAX_FAILED_ATTEMPTS = 5;
        private const int LOCKOUT_DURATION_MINUTES = 30;

        public SecurityLogService() {
            _connectionString = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
        }

        public void LogSuccessfulLogin(int? userId, string email, string loginMethod) {
            try {
                string ipAddress = GetClientIPAddress();
                string userAgent = GetUserAgent();
                string sessionId = GetSessionId();

                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_LogSecurityEvent", conn)) {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@UserId",        userId.HasValue ? (object)userId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email",         email);
                    cmd.Parameters.AddWithValue("@EventType",     "LoginSuccess");
                    cmd.Parameters.AddWithValue("@LoginMethod",   loginMethod ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsSuccess",     true);
                    cmd.Parameters.AddWithValue("@FailureReason", DBNull.Value);
                    cmd.Parameters.AddWithValue("@IPAddress",     ipAddress ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserAgent",     userAgent ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@SessionId",     sessionId ?? (object)DBNull.Value);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                AppLogger.Error("SecurityLogService.LogSuccessfulLogin", ex);
            }
        }

        public LoginAttemptResult LogFailedLogin(int? userId, string email, string loginMethod, string failureReason) {
            try {
                string ipAddress = GetClientIPAddress();
                string userAgent = GetUserAgent();
                string sessionId = GetSessionId();

                int  consecutiveFailures = 0;
                bool shouldLock          = false;

                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_LogSecurityEvent", conn)) {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@UserId",        userId.HasValue ? (object)userId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email",         email);
                    cmd.Parameters.AddWithValue("@EventType",     "LoginFailed");
                    cmd.Parameters.AddWithValue("@LoginMethod",   loginMethod ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsSuccess",     false);
                    cmd.Parameters.AddWithValue("@FailureReason", failureReason ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IPAddress",     ipAddress ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserAgent",     userAgent ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@SessionId",     sessionId ?? (object)DBNull.Value);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader()) {
                        if (reader.Read()) {
                            consecutiveFailures = reader["ConsecutiveFailures"] != DBNull.Value ? (int)reader["ConsecutiveFailures"] : 0;
                            shouldLock          = reader["ShouldLock"] != DBNull.Value && (bool)reader["ShouldLock"];
                        }
                    }
                }

                if (shouldLock && userId.HasValue)
                    LockUserAccount(email, $"Cuenta bloqueada automáticamente después de {consecutiveFailures} intentos fallidos");

                return new LoginAttemptResult {
                    ConsecutiveFailures = consecutiveFailures,
                    ShouldLock          = shouldLock,
                    RemainingAttempts   = Math.Max(0, MAX_FAILED_ATTEMPTS - consecutiveFailures)
                };
            } catch (Exception ex) {
                AppLogger.Error("SecurityLogService.LogFailedLogin", ex);
                return new LoginAttemptResult { ConsecutiveFailures = 0, ShouldLock = false, RemainingAttempts = MAX_FAILED_ATTEMPTS };
            }
        }

        public void LockUserAccount(string email, string reason) {
            try {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_LockUserAccount", conn)) {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Email",  email);
                    cmd.Parameters.AddWithValue("@Reason", reason);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                AppLogger.Error("SecurityLogService.LockUserAccount", ex);
                throw;
            }
        }

        public void UnlockUserAccount(string email, string unlockedBy = "System") {
            try {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_UnlockUserAccount", conn)) {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Email",       email);
                    cmd.Parameters.AddWithValue("@UnlockedBy",  unlockedBy);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                AppLogger.Error("SecurityLogService.UnlockUserAccount", ex);
                throw;
            }
        }

        public void LogEvent(int? userId, string email, string eventType, string detail = null) {
            try {
                string ipAddress = GetClientIPAddress();
                string userAgent = GetUserAgent();
                string sessionId = GetSessionId();

                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_LogSecurityEvent", conn)) {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@UserId",        userId.HasValue ? (object)userId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email",         email ?? "");
                    cmd.Parameters.AddWithValue("@EventType",     eventType);
                    cmd.Parameters.AddWithValue("@LoginMethod",   DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsSuccess",     true);
                    cmd.Parameters.AddWithValue("@FailureReason", (object)detail ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IPAddress",     (object)ipAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserAgent",     (object)userAgent ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SessionId",     (object)sessionId ?? DBNull.Value);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                AppLogger.Warn("SecurityLogService.LogEvent", ex.Message);
            }
        }

        private string GetClientIPAddress() {
            try {
                HttpContext context = HttpContext.Current;
                if (context == null) return null;
                string ip = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (string.IsNullOrEmpty(ip))
                    return context.Request.ServerVariables["REMOTE_ADDR"];
                string[] parts = ip.Split(',');
                return parts.Length > 0 ? parts[0].Trim() : ip;
            } catch { return null; }
        }

        private string GetUserAgent() {
            try {
                HttpContext context = HttpContext.Current;
                if (context == null) return null;
                string ua = context.Request.UserAgent;
                return !string.IsNullOrEmpty(ua) && ua.Length > 500 ? ua.Substring(0, 500) : ua;
            } catch { return null; }
        }

        private string GetSessionId() {
            try {
                HttpContext context = HttpContext.Current;
                if (context == null || context.Session == null) return null;
                return context.Session.SessionID;
            } catch { return null; }
        }
    }

    public class LoginAttemptResult {
        public int  ConsecutiveFailures { get; set; }
        public bool ShouldLock          { get; set; }
        public int  RemainingAttempts   { get; set; }
    }
}
