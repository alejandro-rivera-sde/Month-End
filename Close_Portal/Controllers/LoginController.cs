using System;
using System.Threading.Tasks;
using Close_Portal.Models;
using Close_Portal.Services;
using Close_Portal.DataAccess;

namespace Close_Portal.Controllers {
    public static class LoginController {
        private static readonly GoogleAuthService _googleAuthService = new GoogleAuthService();
        private static readonly UserDataAccess _userDataAccess = new UserDataAccess();
        private static readonly SecurityLogService _securityLogService = new SecurityLogService();

        public static LoginResult ValidateGoogleLogin(GoogleLoginRequest request) {
            System.Diagnostics.Debug.WriteLine("===== CONTROLLER GOOGLE INICIO =====");

            if (request == null || string.IsNullOrWhiteSpace(request.IdToken)) {
                return new LoginResult { Success = false, Message = "Token de Google es requerido" };
            }

            try {
                // Ejecutar en Task.Run para evitar deadlock de ASP.NET sync context.
                // request.InvitationToken ya fue leído de Session en el WebMethod
                // y viaja aquí como propiedad del request — sin necesidad de HttpContext.
                LoginResult result = Task.Run(async () => {
                    return await _googleAuthService.ValidateGoogleToken(request);
                }).GetAwaiter().GetResult();

                if (result.Success) {
                    _securityLogService.LogSuccessfulLogin(result.UserId, result.Email, "Google");
                    System.Diagnostics.Debug.WriteLine("✓ Login Google exitoso registrado");
                } else {
                    _securityLogService.LogFailedLogin(result.UserId, result.Email, "Google", result.Message);
                    System.Diagnostics.Debug.WriteLine($"✗ Login Google fallido: {result.Message}");
                }

                return result;

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR en Google login: {ex.Message}");
                return new LoginResult { Success = false, Message = "Error al validar con Google" };
            }
        }

        public static LoginResult ValidateStandardLogin(StandardLoginRequest request) {
            System.Diagnostics.Debug.WriteLine("===== CONTROLLER ESTÁNDAR INICIO =====");

            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)) {
                return new LoginResult { Success = false, Message = "Email y contraseña son requeridos" };
            }

            try {
                LoginResult result = _userDataAccess.ValidateStandardLogin(request.Email, request.Password);

                if (result.Success) {
                    _securityLogService.LogSuccessfulLogin(result.UserId, result.Email, "Standard");
                    System.Diagnostics.Debug.WriteLine("✓ Login estándar exitoso registrado");
                } else {
                    LoginAttemptResult attemptResult = _securityLogService.LogFailedLogin(
                        result.UserId, request.Email, "Standard", result.Message);

                    System.Diagnostics.Debug.WriteLine($"✗ Login fallido - Intentos: {attemptResult.ConsecutiveFailures}");

                    if (attemptResult.ShouldLock) {
                        result.Message = "Cuenta bloqueada por múltiples intentos fallidos. Contacte al administrador.";
                    } else if (attemptResult.ConsecutiveFailures > 0) {
                        result.Message = $"{result.Message}. Intentos restantes: {attemptResult.RemainingAttempts}";
                    }
                }

                return result;

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR en login estándar: {ex.Message}");
                return new LoginResult { Success = false, Message = "Error al validar credenciales" };
            }
        }
    }
}