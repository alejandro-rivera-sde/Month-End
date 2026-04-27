using Close_Portal.Controllers;
using Close_Portal.Core;
using Close_Portal.Models;
using System;
using System.Web;
using System.Web.Services;
using System.Web.UI;

namespace Close_Portal.Pages {
    public partial class Login : Page {

        protected void Page_Load(object sender, EventArgs e) {
            if (Session["UserId"] != null && !IsPostBack) {
                Response.Redirect("~/live");
                return;
            }
        }

        [WebMethod(EnableSession = true)]
        public static LoginResult ValidarLoginGoogle(GoogleLoginRequest request) {
            try {
                LoginResult result = LoginController.ValidateGoogleLogin(request);
                if (result.Success)
                    CreateUserSession(result);
                return result;
            } catch (Exception ex) {
                AppLogger.Error("Login.ValidarLoginGoogle", ex);
                return new LoginResult { Success = false, Message = "Error al procesar la solicitud." };
            }
        }

        [WebMethod(EnableSession = true)]
        public static LoginResult ValidarLoginEstandar(StandardLoginRequest request) {
            try {
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                    return new LoginResult { Success = false, Message = "Email y contraseña son requeridos" };

                if (!request.Email.EndsWith("@novamex.com", StringComparison.OrdinalIgnoreCase))
                    return new LoginResult { Success = false, Message = "Por favor usa tu email corporativo @novamex.com" };

                LoginResult result = LoginController.ValidateStandardLogin(request);
                if (result.Success)
                    CreateUserSession(result);
                return result;
            } catch (Exception ex) {
                AppLogger.Error("Login.ValidarLoginEstandar", ex);
                return new LoginResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private static void CreateUserSession(LoginResult loginResult) {
            try {
                HttpContext context = HttpContext.Current;
                if (context?.Session == null) return;

                // Discard any pre-auth session data before writing authenticated identity
                context.Session.Clear();

                context.Session["UserId"]   = loginResult.UserId;
                context.Session["Email"]    = loginResult.Email;
                context.Session["FullName"] = loginResult.FullName ?? loginResult.Email?.Split('@')[0];
                context.Session["RoleName"] = loginResult.RoleName;
                context.Session["RoleId"]   = loginResult.RoleId;

                if (loginResult.UserId.HasValue) {
                    DataAccess.UserDataAccess uda = new DataAccess.UserDataAccess();
                    context.Session["WmsCode"] = uda.GetUserWmsCode(loginResult.UserId.Value);
                }
            } catch (Exception ex) {
                AppLogger.Error("Login.CreateUserSession", ex);
            }
        }
    }
}
