using Close_Portal.Core;
using Close_Portal.Models;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Close_Portal.DataAccess {
    public class UserDataAccess {
        private readonly string _connectionString;

        public UserDataAccess() {
            _connectionString = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
        }

        public LoginResult ValidateGoogleLogin(string email) {
            try {
                using (SqlConnection conn = new SqlConnection(_connectionString)) {
                    using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_ValidateGoogleLogin", conn)) {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Email", email);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader()) {
                            if (reader.Read()) {
                                var result = new LoginResult {
                                    Success = reader["Success"] != DBNull.Value && (bool)reader["Success"],
                                    Message = reader["Message"]?.ToString(),
                                    UserId = reader["UserId"] != DBNull.Value ? (int?)reader["UserId"] : null,
                                    RoleId = reader["RoleId"] != DBNull.Value ? (int?)reader["RoleId"] : null,
                                    Email = reader["Email"]?.ToString(),
                                    RoleName = reader["RoleName"]?.ToString()
                                };
                                if (result.Success && result.UserId.HasValue)
                                    result.FullName = GetFullName(result.UserId.Value);
                                return result;
                            }
                        }
                    }
                }

                return new LoginResult {
                    Success = false,
                    Message = "Error al procesar la solicitud"
                };
            } catch (Exception ex) {
                AppLogger.Error("UserDataAccess.ValidateGoogleLogin", ex);
                throw;
            }
        }

        public LoginResult ValidateStandardLogin(string email, string password) {
            try {
                try {
                    BCrypt.Net.BCrypt.HashPassword("test", 11);
                } catch (Exception bcryptEx) {
                    AppLogger.Error("UserDataAccess.ValidateStandardLogin bcrypt", bcryptEx);
                    return new LoginResult { Success = false, Message = "Error de configuración del sistema (bcrypt)" };
                }

                string storedHash = null;
                int? userId = null;
                string roleName = null;
                int? roleId = null;
                string returnedEmail = null;

                using (SqlConnection conn = new SqlConnection(_connectionString)) {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand("MonthEnd_sp_ValidateStandardLogin", conn)) {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.Parameters.AddWithValue("@PasswordHash", DBNull.Value);

                        using (SqlDataReader reader = cmd.ExecuteReader()) {
                            if (reader.Read()) {
                                bool success = reader["Success"] != DBNull.Value && (bool)reader["Success"];
                                string message = reader["Message"]?.ToString();

                                if (!success)
                                    return new LoginResult { Success = false, Message = message ?? "Usuario no encontrado" };

                                if (reader["UserId"] != DBNull.Value) userId = (int)reader["UserId"];
                                if (reader["RoleName"] != DBNull.Value) roleName = reader["RoleName"].ToString();
                                if (reader["RoleId"] != DBNull.Value) roleId = (int)reader["RoleId"];
                                if (reader["Email"] != DBNull.Value) returnedEmail = reader["Email"].ToString();
                                if (reader["PasswordHash"] != DBNull.Value)
                                    storedHash = reader["PasswordHash"].ToString();
                            } else {
                                return new LoginResult { Success = false, Message = "Error al consultar base de datos" };
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(storedHash))
                    return new LoginResult { Success = false, Message = "Usuario no configurado para login estándar" };

                bool isPasswordValid = false;
                try {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(password, storedHash);
                } catch (Exception bcryptEx) {
                    AppLogger.Error("UserDataAccess.ValidateStandardLogin bcrypt.Verify", bcryptEx);
                    return new LoginResult { Success = false, Message = "Error al validar contraseña (formato de hash inválido)" };
                }

                if (isPasswordValid) {
                    try {
                        using (SqlConnection conn = new SqlConnection(_connectionString)) {
                            conn.Open();
                            using (SqlCommand cmd = new SqlCommand(
                                "UPDATE MonthEnd_Users SET Last_Login_Date = GETDATE() WHERE User_Id = @UserId", conn)) {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    } catch (Exception updateEx) {
                        AppLogger.Warn("UserDataAccess.ValidateStandardLogin Last_Login_Date", updateEx.Message);
                    }

                    return new LoginResult {
                        Success = true,
                        Message = "Login exitoso",
                        UserId = userId,
                        Email = returnedEmail ?? email,
                        FullName = userId.HasValue ? GetFullName(userId.Value) : null,
                        RoleName = roleName,
                        RoleId = roleId
                    };
                }

                return new LoginResult { Success = false, Message = "Credenciales incorrectas" };

            } catch (SqlException sqlEx) {
                AppLogger.Error("UserDataAccess.ValidateStandardLogin", sqlEx);
                throw new Exception($"Error de BD: {sqlEx.Message}", sqlEx);
            } catch (Exception ex) {
                AppLogger.Error("UserDataAccess.ValidateStandardLogin", ex);
                throw;
            }
        }

        private string GetFullName(int userId) {
            try {
                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(
                    "SELECT RTRIM(ISNULL(First_Name,'') + ' ' + ISNULL(Last_Name,'')) FROM MonthEnd_Users WHERE User_Id = @UserId", conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();
                    var val = cmd.ExecuteScalar()?.ToString()?.Trim();
                    return string.IsNullOrEmpty(val) ? null : val;
                }
            } catch (Exception ex) { AppLogger.Error("UserDataAccess.GetFullName", ex); return null; }
        }

        public void SavePreferredLang(int userId, string lang) {
            try {
                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(
                    "UPDATE MonthEnd_Users SET Preferred_Lang = @Lang WHERE User_Id = @UserId", conn)) {
                    cmd.Parameters.AddWithValue("@Lang", lang);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                AppLogger.Error("UserDataAccess.SavePreferredLang", ex);
            }
        }

        /// <summary>
        /// Obtiene el WMS_Code del primer MonthEnd_WMS activo asignado al usuario,
        /// derivado via MonthEnd_Users_WMS → MonthEnd_OMS → MonthEnd_WMS.
        /// Se almacena en Session["WmsCode"] al hacer login.
        /// </summary>
        public string GetUserWmsCode(int userId) {
            try {
                using (SqlConnection conn = new SqlConnection(_connectionString)) {
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT TOP 1 w.WMS_Code
                        FROM MonthEnd_Users_WMS uw
                        INNER JOIN MonthEnd_WMS w ON w.WMS_Id = uw.WMS_Id
                        WHERE uw.User_Id = @UserId
                          AND w.Active   = 1
                        ORDER BY w.WMS_Code", conn)) {

                        cmd.Parameters.AddWithValue("@UserId", userId);
                        conn.Open();
                        return cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                }
            } catch (Exception ex) {
                AppLogger.Error("UserDataAccess.GetUserWmsCode", ex);
                return "";
            }
        }
    }
}
