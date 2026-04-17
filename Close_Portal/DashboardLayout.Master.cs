using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Close_Portal {
    public partial class DashboardLayout : System.Web.UI.MasterPage {
        protected void Page_Load(object sender, EventArgs e) {
            if (Session["UserId"] == null) {
                Response.Redirect("~/login");
                return;
            }
            if (!IsPostBack) {
                LoadUserInfo();
                ApplyRolePermissions();
            }
        }
        private void ApplyRolePermissions() {
            int roleId = Convert.ToInt32(Session["RoleId"]);
            System.Diagnostics.Debug.WriteLine($"=== ApplyRolePermissions - RoleId: {roleId} ===");
            sectionAdmin.Visible      = (roleId >= 3);
            sectionGuard.Visible      = (roleId >= 3);
            sectionValidation.Visible = (roleId >= 2);
            sectionWarehouses.Visible = true;
            sectionIT.Visible         = (roleId == 4);
            // IT Support Chat: visible para agentes (Administrador+) como panel de gestión
            sectionITSupport.Visible  = (roleId >= 3);
            // Soporte: visible para usuarios Regular y Manager (que no son agentes IT)
            sectionSupport.Visible    = (roleId < 3);
            string roleKey = roleId == 4 ? "owner"
                           : roleId == 3 ? "admin"
                           : roleId == 2 ? "manager"
                           : "regular";
            dashboardHeader.Attributes["class"] = $"dashboard-header header-role-{roleKey}";
            System.Diagnostics.Debug.WriteLine($"=== Role key: {roleKey} ===");
        }
        private void LoadUserInfo() {
            string userName = Session["FullName"]?.ToString();
            string email = Session["Email"]?.ToString();
            string roleName = Session["RoleName"]?.ToString();
            int userId = Session["UserId"] != null ? (int)Session["UserId"] : -1;

            if (string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(email))
                userName = email.Split('@')[0];
            if (string.IsNullOrEmpty(userName)) userName = "Usuario";
            if (string.IsNullOrEmpty(roleName)) roleName = "User";

            litUserName.Text = userName;
            litUserRole.Text = roleName;

            litWmsName.Text = GetUserDepartmentCode(userId);

            string initials = userName.Length >= 2
                ? userName.Substring(0, 2).ToUpper()
                : userName.Substring(0, 1).ToUpper();
            litUserInitials.Text = initials;
        }
        private string GetUserDepartmentCode(int userId) {
            if (userId <= 0) return "";
            try {
                string cs = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
                using (var conn = new SqlConnection(cs))
                using (var cmd = new SqlCommand(@"
                    SELECT d.Department_Code
                    FROM   MonthEnd_Users u
                    INNER JOIN MonthEnd_Departments d ON d.Department_Id = u.Department_Id
                    WHERE  u.User_Id = @UserId", conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();
                    object result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "";
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetUserDepartmentCode] ERROR: {ex.Message}");
                return "";
            }
        }
        public string PageTitleText {
            get { return ((ContentPlaceHolder)Master.FindControl("TitleContent")).Page.Title; }
            set { ((ContentPlaceHolder)Master.FindControl("TitleContent")).Page.Title = value; }
        }
    }
}