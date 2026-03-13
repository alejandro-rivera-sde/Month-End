using Close_Portal.DataAccess;
using Close_Portal.Models;
using Google.Apis.Auth;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Close_Portal.Services {
    public class GoogleAuthService {
        private readonly UserDataAccess _userDataAccess;
        private const string ALLOWED_DOMAIN = "@novamex.com";

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        public GoogleAuthService() {
            _userDataAccess = new UserDataAccess();
        }

        /// <summary>
        /// Valida token de Google. Si request.InvitationToken tiene valor,
        /// crea el usuario ANTES de llamar al SP de login para evitar el bucle.
        /// InvitationToken se lee en el WebMethod donde HttpContext está disponible
        /// y se pasa como parámetro — nunca se lee desde Task.Run.
        /// </summary>
        public async Task<LoginResult> ValidateGoogleToken(GoogleLoginRequest request) {
            System.Diagnostics.Debug.WriteLine("===== INICIO ValidateGoogleToken =====");

            try {
                // JWT validation dentro de Task.Run (fix de deadlock ya existente)
                var payload = await Task.Run(async () =>
                    await GoogleJsonWebSignature.ValidateAsync(request.IdToken)
                ).ConfigureAwait(false);

                string email = payload.Email?.Trim().ToLower();
                System.Diagnostics.Debug.WriteLine($"===== Token validado. Email: {email} =====");

                if (string.IsNullOrEmpty(email))
                    return new LoginResult { Success = false, Message = "No se pudo obtener el email de Google" };

                if (!email.EndsWith(ALLOWED_DOMAIN, StringComparison.OrdinalIgnoreCase))
                    return new LoginResult { Success = false, Message = "Revisar información. Si el problema persiste, contacte administrador" };

                // ── Procesar invitación si viene con token ──────────────────────
                // InvitationToken ya fue leído de Session en el WebMethod (donde
                // HttpContext sí está disponible) y pasado como parámetro.
                if (!string.IsNullOrEmpty(request.InvitationToken)) {
                    System.Diagnostics.Debug.WriteLine($"[GoogleAuthService] Procesando invitación: {request.InvitationToken}");
                    try {
                        ProcessPendingInvitation(email, request.InvitationToken);
                    } catch (Exception invEx) {
                        // Falla silenciosamente — no bloquea el login
                        System.Diagnostics.Debug.WriteLine($"[GoogleAuthService] WARNING invitación: {invEx.Message}");
                    }
                }

                // ── Login normal — si vino de invitación el usuario ya existe ──
                System.Diagnostics.Debug.WriteLine("===== Validando en BD =====");
                LoginResult result = _userDataAccess.ValidateGoogleLogin(email);

                System.Diagnostics.Debug.WriteLine($"===== BD Result: Success={result.Success} Msg={result.Message} =====");
                return result;

            } catch (InvalidJwtException ex) {
                System.Diagnostics.Debug.WriteLine($"===== Token inválido: {ex.Message} =====");
                return new LoginResult { Success = false, Message = "Token de Google inválido o expirado" };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"===== ERROR TIPO: {ex.GetType().FullName} =====");
                System.Diagnostics.Debug.WriteLine($"===== ERROR MSG: {ex.Message} =====");
                System.Diagnostics.Debug.WriteLine($"===== INNER: {ex.InnerException?.Message} =====");
                System.Diagnostics.Debug.WriteLine($"===== STACK: {ex.StackTrace} =====");
                return new LoginResult { Success = false, Message = "Error al procesar la solicitud de Google" };
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PROCESS PENDING INVITATION
        // Crea el usuario en BD si no existe y acepta la invitación.
        // Solo ejecuta si googleEmail === invitedEmail (validación de seguridad).
        // ════════════════════════════════════════════════════════════════════
        private static void ProcessPendingInvitation(string googleEmail, string invitationToken) {
            System.Diagnostics.Debug.WriteLine($"[ProcessPendingInvitation] email={googleEmail}");

            using (var conn = new SqlConnection(_connStr)) {
                conn.Open();

                // 1. Cargar invitación activa
                int invitationId;
                int roleId;
                string invitedEmail;

                using (var cmd = new SqlCommand(@"
                    SELECT Invitation_Id, Email, Role_Id
                    FROM User_Invitations
                    WHERE Token = @Token AND Is_Active = 1 AND Accepted_At IS NULL", conn)) {
                    cmd.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = Guid.Parse(invitationToken);
                    using (var r = cmd.ExecuteReader()) {
                        if (!r.Read()) {
                            System.Diagnostics.Debug.WriteLine("[ProcessPendingInvitation] Invitación no encontrada o ya procesada.");
                            return;
                        }
                        invitationId = (int)r["Invitation_Id"];
                        roleId = (int)r["Role_Id"];
                        invitedEmail = r["Email"].ToString().Trim().ToLower();
                    }
                }

                // 2. Seguridad: el email de Google debe coincidir exactamente
                if (googleEmail != invitedEmail) {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ProcessPendingInvitation] Mismatch: google={googleEmail} inv={invitedEmail}. Ignorado.");
                    return;
                }

                // 3. Transacción: crear/actualizar usuario + WMS + aceptar
                using (var tx = conn.BeginTransaction()) {
                    try {
                        // Crear usuario si no existe
                        int userId = 0;
                        using (var cmd = new SqlCommand(
                            "SELECT User_Id FROM Users WHERE Email = @Email", conn, tx)) {
                            cmd.Parameters.AddWithValue("@Email", invitedEmail);
                            var found = cmd.ExecuteScalar();
                            if (found != null) userId = (int)found;
                        }

                        if (userId > 0) {
                            using (var cmd = new SqlCommand(
                                "UPDATE Users SET Role_Id = @RoleId, Active = 1 WHERE User_Id = @UserId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@RoleId", roleId);
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.ExecuteNonQuery();
                            }
                            System.Diagnostics.Debug.WriteLine($"[ProcessPendingInvitation] Usuario existente actualizado. UserId={userId}");
                        } else {
                            string username = invitedEmail.Split('@')[0];
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO Users (Email, Username, Role_Id, Login_Type, Active, Locked, Created_At)
                                OUTPUT INSERTED.User_Id
                                VALUES (@Email, @Username, @RoleId, 'Google', 1, 0, GETDATE())", conn, tx)) {
                                cmd.Parameters.AddWithValue("@Email", invitedEmail);
                                cmd.Parameters.AddWithValue("@Username", username);
                                cmd.Parameters.AddWithValue("@RoleId", roleId);
                                userId = (int)cmd.ExecuteScalar();
                            }
                            System.Diagnostics.Debug.WriteLine($"[ProcessPendingInvitation] Usuario creado. UserId={userId}");
                        }

                        // Actualizar WMS
                        using (var cmd = new SqlCommand(
                            "DELETE FROM Users_WMS WHERE User_Id = @UserId", conn, tx)) {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        var wmsIds = new List<int>();
                        using (var cmd = new SqlCommand(
                            "SELECT WMS_Id FROM User_Invitation_WMS WHERE Invitation_Id = @InvId", conn, tx)) {
                            cmd.Parameters.AddWithValue("@InvId", invitationId);
                            using (var r = cmd.ExecuteReader())
                                while (r.Read()) wmsIds.Add((int)r["WMS_Id"]);
                        }

                        foreach (int wmsId in wmsIds) {
                            using (var cmd = new SqlCommand(
                                "INSERT INTO Users_WMS (User_Id, WMS_Id) VALUES (@UserId, @WmsId)", conn, tx)) {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.Parameters.AddWithValue("@WmsId", wmsId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Marcar invitación como aceptada
                        using (var cmd = new SqlCommand(@"
                            UPDATE User_Invitations
                            SET Accepted_At = GETDATE(), Is_Active = 0
                            WHERE Invitation_Id = @InvId", conn, tx)) {
                            cmd.Parameters.AddWithValue("@InvId", invitationId);
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                        System.Diagnostics.Debug.WriteLine($"[ProcessPendingInvitation] ✓ Completado. UserId={userId}");

                    } catch {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}