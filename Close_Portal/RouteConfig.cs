using System.Web.Routing;

namespace Close_Portal {
    public static class RouteConfig {
        public static void RegisterRoutes(RouteCollection routes) {

            // ── Auth ─────────────────────────────────────────────────
            routes.MapPageRoute(
                routeName: "login",
                routeUrl: "login",
                physicalFile: "~/Pages/Home/Login.aspx");

            routes.MapPageRoute(
                routeName: "logout",
                routeUrl: "logout",
                physicalFile: "~/Pages/Home/Logout.aspx");

            // ── Navigation ───────────────────────────────────────────
            routes.MapPageRoute(
                routeName: "live",
                routeUrl: "live",
                physicalFile: "~/Pages/Main/Live.aspx");

            // ── Admin ────────────────────────────────────────────────
            routes.MapPageRoute(
                routeName: "users",
                routeUrl: "users",
                physicalFile: "~/Pages/Admin/UserManagement.aspx");

            routes.MapPageRoute(
                routeName: "locations",
                routeUrl: "locations",
                physicalFile: "~/Pages/Admin/WarehouseManagement.aspx");

            // ── Guard ────────────────────────────────────────────────
            routes.MapPageRoute(
                routeName: "guard",
                routeUrl: "guard",
                physicalFile: "~/Pages/Admin/Guard.aspx");

            // ── Validation ───────────────────────────────────────────
            routes.MapPageRoute(
                routeName: "validate",
                routeUrl: "validate",
                physicalFile: "~/Pages/Validation/ValidateRequest.aspx");

            // ── Warehouses ───────────────────────────────────────────
            routes.MapPageRoute(
                routeName: "request-closure",
                routeUrl: "closure",
                physicalFile: "~/Pages/Warehouses/RequestClosure.aspx");

            // ── IT ───────────────────────────────────────────────────
            routes.MapPageRoute(
                routeName: "email-service",
                routeUrl: "email",
                physicalFile: "~/Pages/IT/EmailService.aspx");

            routes.MapPageRoute(
                routeName: "processes",
                routeUrl: "processes",
                physicalFile: "~/Pages/IT/Processes.aspx");

            routes.MapPageRoute(
                routeName: "it-support",
                routeUrl: "it-support",
                physicalFile: "~/Pages/IT/ITSupport.aspx");

            // ── Support (todos los usuarios) ─────────────────────────
            routes.MapPageRoute(
                routeName: "support",
                routeUrl: "support",
                physicalFile: "~/Pages/Support/Support.aspx");
        }
    }
}