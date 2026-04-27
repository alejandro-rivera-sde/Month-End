using Close_Portal.Services;
using System;
using System.Web.UI;

namespace Close_Portal.Pages {
    public partial class Logout : Page {
        protected void Page_Load(object sender, EventArgs e) {
            int? userId = Session["UserId"] != null ? (int?)Session["UserId"] : null;
            string email = Session["Email"]?.ToString() ?? "";
            try {
                new SecurityLogService().LogEvent(userId, email, "Logout");
            } catch { }

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