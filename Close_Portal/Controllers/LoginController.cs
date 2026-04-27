using Close_Portal.Core;
using Close_Portal.Models;
using Close_Portal.Services;
using Close_Portal.DataAccess;
using System;
using System.Threading.Tasks;

namespace Close_Portal.Controllers {
    public static class LoginController {
        private static readonly GoogleAuthService _googleAuthService = new GoogleAuthService();
        private static readonly UserDataAccess _userDataAccess = new UserDataAccess();
        private static readonly SecurityLogService _securityLogService = new SecurityLogService();

        public static LoginResult ValidateGoogleLogin(GoogleLoginRequest request) {
            if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
                return new LoginResult { Success = false, Message = "Token de Google es requerido" };

            try {
                LoginResult result = Task.Run(async () =>
                    await _googleAuthService.ValidateGoogleToken(request)
                ).GetAwaiter().GetResult();

                if (result.Success)
                    _securityLogService.LogSuccessfulLogin(result.UserId, result.Email, "Google");
                else
                    _securityLogService.LogFailedLogin(result.UserId, result.Email, "Google", result.Message);

                return result;

            } catch (Exception ex) {
                AppLogger.Error("LoginController.ValidateGoogleLogin", ex);
                return new LoginResult { Success = false, Message = "Error al validar con Google" };
            }
        }

        public static LoginResult ValidateStandardLogin(StandardLoginRequest request) {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return new LoginResult { Success = false, Message = "Email y contraseña son requeridos" };

            try {
                LoginResult result = _userDataAccess.ValidateStandardLogin(request.Email, request.Password);

                if (result.Success) {
                    _securityLogService.LogSuccessfulLogin(result.UserId, result.Email, "Standard");
                } else {
                    LoginAttemptResult attemptResult = _securityLogService.LogFailedLogin(
                        result.UserId, request.Email, "Standard", result.Message);

                    if (attemptResult.ShouldLock) {
                        result.Message = "Cuenta bloqueada por múltiples intentos fallidos. Contacte al administrador.";
                    } else if (attemptResult.ConsecutiveFailures > 0) {
                        result.Message = $"{result.Message}. Intentos restantes: {attemptResult.RemainingAttempts}";
                    }
                }

                return result;

            } catch (Exception ex) {
                AppLogger.Error("LoginController.ValidateStandardLogin", ex);
                return new LoginResult { Success = false, Message = "Error al validar credenciales" };
            }
        }
    }
}
