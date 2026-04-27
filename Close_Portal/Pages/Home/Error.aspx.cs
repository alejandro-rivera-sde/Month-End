using System;
using System.Web.UI;

namespace Close_Portal.Pages {
    // Hereda de Page directamente — no de SecurePage.
    // La sesión puede estar inválida cuando el usuario llega aquí.
    public partial class Error : Page {

        protected string ErrorTitle = "";
        protected string ErrorMessage = "";
        protected string ErrorIcon = "block";

        protected void Page_Load(object sender, EventArgs e) {
            string reason = Request.QueryString["reason"] ?? "unknown";

            switch (reason) {
                case "unauthorized":
                    ErrorTitle = "Acceso no autorizado";
                    ErrorMessage = "No tienes permisos para acceder a esa sección. Tu sesión será cerrada por seguridad.";
                    ErrorIcon = "lock";
                    break;

                case "notfound":
                    ErrorTitle = "Página no encontrada";
                    ErrorMessage = "La sección que intentas acceder no existe. Tu sesión será cerrada por seguridad.";
                    ErrorIcon = "search_off";
                    break;

                default:
                    ErrorTitle = "Error inesperado";
                    ErrorMessage = "Ocurrió un error inesperado. Tu sesión será cerrada por seguridad.";
                    ErrorIcon = "error_outline";
                    break;
            }
        }
    }
}