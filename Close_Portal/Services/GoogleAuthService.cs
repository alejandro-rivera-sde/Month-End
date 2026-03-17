using Close_Portal.DataAccess;
using Close_Portal.Models;
using Google.Apis.Auth;
using System;
using System.Configuration;
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

                // ── Login normal ────────────────────────────────────────────────
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

    }
}