using Close_Portal.Core;
using Close_Portal.DataAccess;
using System;
using System.Web;
using System.Web.UI;

namespace Close_Portal.Controls {

    /// <summary>
    /// Widget flotante de Soporte IT — interfaz principal para todos los usuarios.
    ///
    /// Modos:
    ///   'widget'       — usuario regular: una sola conversación con IT
    ///   'agent-widget' — agente IT (role >= Administrador): lista de casos + respuesta inline
    ///
    /// El widget se suprime automáticamente en las páginas de chat dedicadas
    /// (Support.aspx e ITSupport.aspx) para evitar conflictos de IDs y handlers.
    /// </summary>
    public partial class ChatWidget : UserControl {

        protected bool   IsAgent         { get; private set; }
        protected string ChatModeJs      { get; private set; }
        protected string WebMethodBaseJs { get; private set; }
        protected string AgentNameJs     { get; private set; }

        protected void Page_Load(object sender, EventArgs e) {
            // Suprimir en las páginas de chat dedicadas — tienen su propia UI completa
            string path = Request.AppRelativeCurrentExecutionFilePath ?? "";
            if (path.IndexOf("Support.aspx",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("ITSupport.aspx", StringComparison.OrdinalIgnoreCase) >= 0) {
                this.Visible = false;
                return;
            }

            int roleId = Session["RoleId"] != null ? (int)Session["RoleId"] : 0;
            int userId = Session["UserId"] != null ? (int)Session["UserId"] : 0;

            bool isAdminOrAbove = roleId >= RoleLevel.Administrador;
            bool isItDept = false;
            if (isAdminOrAbove && userId > 0) {
                string deptCode = new GuardDataAccess().GetUserDepartmentCode(userId);
                isItDept = string.Equals(deptCode, "IT", StringComparison.OrdinalIgnoreCase);
            }
            IsAgent = isAdminOrAbove && isItDept;

            ChatModeJs      = IsAgent ? "agent-widget" : "widget";
            WebMethodBaseJs = IsAgent
                ? ResolveUrl("~/Pages/IT/ITSupport.aspx/")
                : ResolveUrl("~/Pages/Support/Support.aspx/");
            AgentNameJs     = IsAgent && Session["FullName"] != null
                ? HttpUtility.JavaScriptStringEncode(Session["FullName"].ToString())
                : "";
        }
    }
}
