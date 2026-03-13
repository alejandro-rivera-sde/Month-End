using Close_Portal.Controllers;
using Close_Portal.Models;
using System;
using System.Web;
using System.Web.Services;
using System.Web.UI;

namespace Close_Portal.Pages {
    public partial class Login : Page {

        // Leído de ?inv= en Page_Load, renderizado en el hidden field #pendingInvToken.
        // login.js lo lee del DOM — no depende de window.location.search
        // que Google OAuth puede alterar durante el flujo de autenticación.
        protected string PendingInvToken { get; private set; } = "";

        protected void Page_Load(object sender, EventArgs e) {
            if (Session["UserId"] != null && !IsPostBack) {
                System.Diagnostics.Debug.WriteLine("Ya hay sesión activa - Redirigiendo a Dashboard");
                Response.Redirect("~/Pages/Main/Live.aspx");
                return;
            }

            // Leer ?inv=TOKEN y exponerlo al DOM vía hidden field
            string inv = Request.QueryString["inv"];
            if (!string.IsNullOrWhiteSpace(inv)) {
                PendingInvToken = inv;
                System.Diagnostics.Debug.WriteLine($"[Login.Page_Load] PendingInvToken: {inv}");
            }
        }

        [WebMethod(EnableSession = true)]
        public static LoginResult ValidarLoginGoogle(GoogleLoginRequest request) {
            try {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("===== WEBMETHOD GOOGLE INICIO =====");

                if (!string.IsNullOrEmpty(request?.InvitationToken))
                    System.Diagnostics.Debug.WriteLine($"[ValidarLoginGoogle] InvitationToken: {request.InvitationToken}");

                LoginResult result = LoginController.ValidateGoogleLogin(request);

                if (result.Success) {
                    System.Diagnostics.Debug.WriteLine("✓ Login Google exitoso - Creando sesión...");
                    CreateUserSession(result);
                }

                System.Diagnostics.Debug.WriteLine($"Success: {result.Success}");
                System.Diagnostics.Debug.WriteLine("========================================");
                return result;

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GOOGLE: {ex.Message}");
                return new LoginResult { Success = false, Message = "Error al procesar la solicitud." };
            }
        }

        [WebMethod(EnableSession = true)]
        public static LoginResult ValidarLoginEstandar(StandardLoginRequest request) {
            try {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("===== WEBMETHOD ESTÁNDAR INICIO =====");

                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                    return new LoginResult { Success = false, Message = "Email y contraseña son requeridos" };

                if (!request.Email.EndsWith("@novamex.com", StringComparison.OrdinalIgnoreCase))
                    return new LoginResult { Success = false, Message = "Por favor usa tu email corporativo @novamex.com" };

                LoginResult result = LoginController.ValidateStandardLogin(request);

                if (result.Success) {
                    System.Diagnostics.Debug.WriteLine("✓ Login estándar exitoso - Creando sesión...");
                    CreateUserSession(result);
                }

                System.Diagnostics.Debug.WriteLine($"Success: {result.Success}");
                System.Diagnostics.Debug.WriteLine($"Message: {result.Message}");
                System.Diagnostics.Debug.WriteLine("========================================");
                return result;

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR ESTÁNDAR: {ex.Message}");
                return new LoginResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private static void CreateUserSession(LoginResult loginResult) {
            try {
                System.Diagnostics.Debug.WriteLine("=== CreateUserSession ===");
                HttpContext context = HttpContext.Current;
                if (context?.Session == null) { System.Diagnostics.Debug.WriteLine("ERROR: Session null"); return; }

                context.Session["UserId"] = loginResult.UserId;
                context.Session["Email"] = loginResult.Email;
                context.Session["Username"] = loginResult.Username ?? loginResult.Email;
                context.Session["RoleName"] = loginResult.RoleName;
                context.Session["RoleId"] = loginResult.RoleId;

                System.Diagnostics.Debug.WriteLine($"✓ Sesión: UserId={loginResult.UserId} Email={loginResult.Email} Role={loginResult.RoleName}");

                if (loginResult.UserId.HasValue) {
                    DataAccess.UserDataAccess uda = new DataAccess.UserDataAccess();
                    string wmsCode = uda.GetUserWmsCode(loginResult.UserId.Value);
                    context.Session["WmsCode"] = wmsCode;
                    System.Diagnostics.Debug.WriteLine($"  WmsCode: {wmsCode}");
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR creando sesión: {ex.Message}");
            }
        }
    }
}