using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;

namespace Close_Portal.Services {
    /// <summary>
    /// Servicio para registrar eventos de seguridad
    /// </summary>
    public class SecurityLogService {
        private readonly string _connectionString;

        // Configuración de bloqueo
        private const int MAX_FAILED_ATTEMPTS = 5;  // Máximo de intentos fallidos antes de bloquear
        private const int LOCKOUT_DURATION_MINUTES = 30;  // Duración del bloqueo en minutos

        public SecurityLogService() {
            _connectionString = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
        }

        /// <summary>
        /// Registra un intento de login exitoso
        /// </summary>
        public void LogSuccessfulLogin(int? userId, string email, string loginMethod) {
            try {
                System.Diagnostics.Debug.WriteLine($"=== LogSuccessfulLogin: {email} ===");

                string ipAddress = GetClientIPAddress();
                string userAgent = GetUserAgent();
                string sessionId = GetSessionId();

                using (SqlConnection conn = new SqlConnection(_connectionString)) {
                    using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_LogSecurityEvent", conn)) {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@UserId", userId.HasValue ? (object)userId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.Parameters.AddWithValue("@EventType", "LoginSuccess");
                        cmd.Parameters.AddWithValue("@LoginMethod", loginMethod ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsSuccess", true);
                        cmd.Parameters.AddWithValue("@FailureReason", DBNull.Value);
                        cmd.Parameters.AddWithValue("@IPAddress", ipAddress ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserAgent", userAgent ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@SessionId", sessionId ?? (object)DBNull.Value);

                        conn.Open();
                        cmd.ExecuteNonQuery();

                        System.Diagnostics.Debug.WriteLine("✓ Login exitoso registrado");
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error logging successful login: {ex.Message}");
                // No lanzar excepción, solo log
            }
        }

        /// <summary>
        /// Registra un intento de login fallido y verifica si debe bloquear la cuenta
        /// </summary>
        public LoginAttemptResult LogFailedLogin(int? userId, string email, string loginMethod, string failureReason) {
            try {
                System.Diagnostics.Debug.WriteLine($"=== LogFailedLogin: {email}, Reason: {failureReason} ===");

                string ipAddress = GetClientIPAddress();
                string userAgent = GetUserAgent();
                string sessionId = GetSessionId();

                int consecutiveFailures = 0;
                bool shouldLock = false;

                using (SqlConnection conn = new SqlConnection(_connectionString)) {
                    using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_LogSecurityEvent", conn)) {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@UserId", userId.HasValue ? (object)userId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.Parameters.AddWithValue("@EventType", "LoginFailed");
                        cmd.Parameters.AddWithValue("@LoginMethod", loginMethod ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsSuccess", false);
                        cmd.Parameters.AddWithValue("@FailureReason", failureReason ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IPAddress", ipAddress ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserAgent", userAgent ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@SessionId", sessionId ?? (object)DBNull.Value);

                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader()) {
                            if (reader.Read()) {
                                consecutiveFailures = reader["ConsecutiveFailures"] != DBNull.Value
                                    ? (int)reader["ConsecutiveFailures"]
                                    : 0;

                                shouldLock = reader["ShouldLock"] != DBNull.Value
                                    ? (bool)reader["ShouldLock"]
                                    : false;
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Consecutive failures: {consecutiveFailures}");
                System.Diagnostics.Debug.WriteLine($"Should lock: {shouldLock}");

                // Si debe bloquear, hacerlo
                if (shouldLock && userId.HasValue) {
                    LockUserAccount(email, $"Cuenta bloqueada automáticamente después de {consecutiveFailures} intentos fallidos");
                }

                return new LoginAttemptResult {
                    ConsecutiveFailures = consecutiveFailures,
                    ShouldLock = shouldLock,
                    RemainingAttempts = Math.Max(0, MAX_FAILED_ATTEMPTS - consecutiveFailures)
                };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error logging failed login: {ex.Message}");
                return new LoginAttemptResult {
                    ConsecutiveFailures = 0,
                    ShouldLock = false,
                    RemainingAttempts = MAX_FAILED_ATTEMPTS
                };
            }
        }

        /// <summary>
        /// Bloquea una cuenta de usuario
        /// </summary>
        public void LockUserAccount(string email, string reason) {
            try {
                System.Diagnostics.Debug.WriteLine($"=== Bloqueando cuenta: {email} ===");
                System.Diagnostics.Debug.WriteLine($"Razón: {reason}");

                using (SqlConnection conn = new SqlConnection(_connectionString)) {
                    using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_LockUserAccount", conn)) {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.Parameters.AddWithValue("@Reason", reason);

                        conn.Open();
                        cmd.ExecuteNonQuery();

                        System.Diagnostics.Debug.WriteLine("✓ Cuenta bloqueada");
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error locking account: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Desbloquea una cuenta de usuario
        /// </summary>
        public void UnlockUserAccount(string email, string unlockedBy = "System") {
            try {
                System.Diagnostics.Debug.WriteLine($"=== Desbloqueando cuenta: {email} ===");

                using (SqlConnection conn = new SqlConnection(_connectionString)) {
                    using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_UnlockUserAccount", conn)) {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.Parameters.AddWithValue("@UnlockedBy", unlockedBy);

                        conn.Open();
                        cmd.ExecuteNonQuery();

                        System.Diagnostics.Debug.WriteLine("✓ Cuenta desbloqueada");
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error unlocking account: {ex.Message}");
                throw;
            }
        }

        #region Helper Methods

        private string GetClientIPAddress() {
            try {
                HttpContext context = HttpContext.Current;
                if (context == null) return null;

                string ipAddress = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

                if (string.IsNullOrEmpty(ipAddress)) {
                    ipAddress = context.Request.ServerVariables["REMOTE_ADDR"];
                } else {
                    // Si hay múltiples IPs (proxy), tomar la primera
                    string[] addresses = ipAddress.Split(',');
                    if (addresses.Length > 0) {
                        ipAddress = addresses[0].Trim();
                    }
                }

                return ipAddress;
            } catch {
                return null;
            }
        }

        private string GetUserAgent() {
            try {
                HttpContext context = HttpContext.Current;
                if (context == null) return null;

                string userAgent = context.Request.UserAgent;

                // Truncar si es muy largo
                if (!string.IsNullOrEmpty(userAgent) && userAgent.Length > 500) {
                    userAgent = userAgent.Substring(0, 500);
                }

                return userAgent;
            } catch {
                return null;
            }
        }

        private string GetSessionId() {
            try {
                HttpContext context = HttpContext.Current;
                if (context == null || context.Session == null) return null;

                return context.Session.SessionID;
            } catch {
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Resultado de un intento de login
    /// </summary>
    public class LoginAttemptResult {
        public int ConsecutiveFailures { get; set; }
        public bool ShouldLock { get; set; }
        public int RemainingAttempts { get; set; }
    }
}