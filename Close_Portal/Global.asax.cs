using System;
using System.Web;
using System.Web.Routing;

namespace Close_Portal {
    public class Global : HttpApplication {
        protected void Application_Start(object sender, EventArgs e) {
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_BeginRequest(object sender, EventArgs e) {
            // Permissions-Policy: deshabilita features del browser que esta app no usa.
            // Se establece aquí para cubrir todas las páginas (login, error, dashboard).
            HttpContext.Current.Response.Headers.Set(
                "Permissions-Policy",
                "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()");
        }
        protected void Application_EndRequest(object sender, EventArgs e) {
            var response = HttpContext.Current?.Response;
            if (response == null) return;
            foreach (string key in response.Cookies.AllKeys) {
                var cookie = response.Cookies[key];
                if (cookie != null)
                    cookie.SameSite = SameSiteMode.Strict;
            }
        }
    }
}
