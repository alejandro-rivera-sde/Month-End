using System;
using System.Web.UI;

namespace Close_Portal.Pages {
    /// <summary>
    /// Página de logout - Limpia la sesión y redirige al login
    /// </summary>
    public partial class Logout : Page {
        protected void Page_Load(object sender, EventArgs e) {
            // Limpiar toda la sesión
            Session.Clear();
            Session.Abandon();

            // Limpiar cookies de autenticación si existen
            Response.Cookies.Clear();

            // Redirigir al login
            Response.Redirect("~/login", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}