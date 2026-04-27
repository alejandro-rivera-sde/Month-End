using Close_Portal.Core;
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
            try {
                var payload = await Task.Run(async () =>
                    await GoogleJsonWebSignature.ValidateAsync(request.IdToken)
                ).ConfigureAwait(false);

                string email = payload.Email?.Trim().ToLower();

                if (string.IsNullOrEmpty(email))
                    return new LoginResult { Success = false, Message = "No se pudo obtener el email de Google" };

                if (!email.EndsWith(ALLOWED_DOMAIN, StringComparison.OrdinalIgnoreCase))
                    return new LoginResult { Success = false, Message = "Revisar información. Si el problema persiste, contacte administrador" };

                return _userDataAccess.ValidateGoogleLogin(email);

            } catch (InvalidJwtException ex) {
                AppLogger.Warn("GoogleAuthService.ValidateGoogleToken", $"JWT inválido: {ex.Message}");
                return new LoginResult { Success = false, Message = "Token de Google inválido o expirado" };
            } catch (Exception ex) {
                AppLogger.Error("GoogleAuthService.ValidateGoogleToken", ex);
                return new LoginResult { Success = false, Message = "Error al procesar la solicitud de Google" };
            }
        }
    }
}
