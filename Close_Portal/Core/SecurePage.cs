using System;
using System.Web;
using System.Web.UI;

namespace Close_Portal.Core {
    /// <summary>
    /// Clase base para todas las páginas protegidas del dashboard.
    /// Hereda de esta clase en lugar de System.Web.UI.Page.
    /// 
    /// Jerarquía de roles:
    ///   4 = Owner        (acceso total)
    ///   3 = Administrador
    ///   2 = Manager
    ///   1 = Regular
    /// </summary>
    public abstract class SecurePage : Page {

        /// <summary>
        /// Define el rol mínimo requerido para acceder a esta página.
        /// Sobreescribe en cada página según necesidad.
        /// Por defecto: Regular (cualquier usuario autenticado).
        /// </summary>
        protected virtual int RequiredRoleId => RoleLevel.Regular;

        /// <summary>Rol del usuario en sesión. -1 si no hay sesión.</summary>
        protected int CurrentRoleId {
            get { return Session["RoleId"] != null ? (int)Session["RoleId"] : -1; }
        }

        protected int CurrentUserId {
            get { return Session["UserId"] != null ? (int)Session["UserId"] : -1; }
        }

        protected string CurrentRoleName {
            get { return Session["RoleName"]?.ToString() ?? ""; }
        }

        /// <summary>
        /// Se ejecuta antes de Page_Load en cada página hija.
        /// Valida sesión y rol automáticamente.
        /// </summary>
        protected override void OnInit(EventArgs e) {
            base.OnInit(e);

            // ── Headers anti-cache: páginas autenticadas nunca deben
            // ser servidas desde cache (previene que datos de un usuario
            // aparezcan en la sesión de otro).
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            Response.Cache.AppendCacheExtension("must-revalidate, no-cache, no-store, private");

            // 1. Sin sesión → Login directamente (sin pasar por Error.aspx)
            //    Cubre logout normal, sesión expirada y cookies limpias.
            if (Session["UserId"] == null) {
                Response.Redirect("~/login");
                return;
            }

            // 2. Rol insuficiente → Error (limpia sesión y redirige a Login)
            //    Solo este caso amerita mostrar el mensaje de acceso denegado.
            if (CurrentRoleId < RequiredRoleId) {
                Response.Redirect("~/Pages/Home/Error.aspx?reason=unauthorized");
                return;
            }
        }

        /// <summary>
        /// Verifica acceso desde WebMethods (llamadas AJAX).
        /// Lanza excepción si no tiene el rol requerido.
        /// </summary>
        public static void CheckAccess(int requiredRoleId) {
            var session = System.Web.HttpContext.Current.Session;
            if (session["UserId"] == null)
                throw new UnauthorizedAccessException("Sesión expirada");

            int roleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
            if (roleId < requiredRoleId)
                throw new UnauthorizedAccessException("Acceso no autorizado");
        }
    }

    /// <summary>
    /// Constantes de roles para uso legible en el código
    /// </summary>
    public static class RoleLevel {
        public const int Regular = 1;
        public const int Manager = 2;
        public const int Administrador = 3;
        public const int Owner = 4;
    }
}