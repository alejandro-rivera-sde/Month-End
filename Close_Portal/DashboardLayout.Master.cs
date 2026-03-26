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
                Response.Redirect("~/Pages/Home/Login.aspx");
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
            sectionAdmin.Visible = (roleId == 4 || roleId == 3);
            sectionGuard.Visible = (roleId == 4 || roleId == 3);
            sectionValidation.Visible = (roleId == 4 || roleId == 3 || roleId == 2);
            sectionWarehouses.Visible = true;
            string roleKey = roleId == 4 ? "owner"
                           : roleId == 3 ? "admin"
                           : roleId == 2 ? "manager"
                           : "regular";
            dashboardHeader.Attributes["class"] = $"dashboard-header header-role-{roleKey}";
            System.Diagnostics.Debug.WriteLine($"=== Role key: {roleKey} ===");
        }
        private void LoadUserInfo() {
            string userName = Session["Username"]?.ToString();
            string email = Session["Email"]?.ToString();
            string roleName = Session["RoleName"]?.ToString();
            string wmsCode = Session["WmsCode"]?.ToString() ?? "";
            int userId = Session["UserId"] != null ? (int)Session["UserId"] : -1;

            if (string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(email))
                userName = email.Split('@')[0];
            if (string.IsNullOrEmpty(userName)) userName = "Usuario";
            if (string.IsNullOrEmpty(roleName)) roleName = "User";

            litUserName.Text = userName;
            litUserRole.Text = roleName;

            // Consultar departamento del usuario
            string deptCode = GetUserDepartmentCode(userId);
            string wmsName = GetWmsFullName(wmsCode);

            // Header: "DEPT · WMS" — si no tiene departamento, solo WMS
            litWmsName.Text = !string.IsNullOrEmpty(deptCode)
                ? $"{deptCode} · {wmsName}"
                : wmsName;

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
                    SELECT d.Department_Name
                    FROM   Users u
                    INNER JOIN Departments d ON d.Department_Id = u.Department_Id
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
        private string GetWmsFullName(string wmsCode) {
            var wmsMap = new Dictionary<string, string> {
                { "NVMCLX", "Calexico"     },
                { "NVMMES", "Dallas"       },
                { "NVMMSQ", "Mesquite"     },
                { "NVMMXL", "Mexicali"     },
                { "NVMLRD", "Nuevo Laredo" }
            };
            if (string.IsNullOrEmpty(wmsCode)) return "";
            return wmsMap.ContainsKey(wmsCode.ToUpper()) ? wmsMap[wmsCode.ToUpper()] : wmsCode;
        }
        public string PageTitleText {
            get { return ((ContentPlaceHolder)Master.FindControl("TitleContent")).Page.Title; }
            set { ((ContentPlaceHolder)Master.FindControl("TitleContent")).Page.Title = value; }
        }
    }
}