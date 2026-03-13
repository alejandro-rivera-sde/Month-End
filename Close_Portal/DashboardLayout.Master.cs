using System;
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

            // 4 = Owner     → Todo
            // 3 = Admin     → Users + Validation + Warehouses
            // 2 = Manager   → Validation + Warehouses
            // 1 = Regular   → Solo Warehouses
            sectionAdmin.Visible = (roleId == 4);
            sectionUsers.Visible = (roleId == 4 || roleId == 3);
            sectionValidation.Visible = (roleId == 4 || roleId == 3 || roleId == 2);
            sectionWarehouses.Visible = true;

            // Clase por rol
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

            if (string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(email))
                userName = email.Split('@')[0];

            if (string.IsNullOrEmpty(userName)) userName = "Usuario";
            if (string.IsNullOrEmpty(roleName)) roleName = "User";

            litUserName.Text = userName;
            litUserRole.Text = roleName;
            litWmsName.Text = GetWmsFullName(wmsCode);

            string initials = userName.Length >= 2
                ? userName.Substring(0, 2).ToUpper()
                : userName.Substring(0, 1).ToUpper();
            litUserInitials.Text = initials;
        }

        /// <summary>
        /// Consulta WMS_Name directamente desde la BD usando el WMS_Code en sesión.
        /// Si no encuentra el código o hay error, devuelve el código tal cual como fallback.
        /// </summary>
        private string GetWmsFullName(string wmsCode) {
            if (string.IsNullOrEmpty(wmsCode)) return "";

            try {
                string connStr = ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;
                using (var conn = new SqlConnection(connStr)) {
                    using (var cmd = new SqlCommand(
                        "SELECT WMS_Name FROM WMS WHERE WMS_Code = @WmsCode AND Active = 1", conn)) {
                        cmd.Parameters.AddWithValue("@WmsCode", wmsCode.ToUpper());
                        conn.Open();
                        object result = cmd.ExecuteScalar();
                        return result?.ToString() ?? wmsCode;
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetWmsFullName] ERROR: {ex.Message}");
                return wmsCode;   // fallback: mostrar el código si falla la BD
            }
        }

        public string PageTitleText {
            get { return ((ContentPlaceHolder)Master.FindControl("TitleContent")).Page.Title; }
            set { ((ContentPlaceHolder)Master.FindControl("TitleContent")).Page.Title = value; }
        }
    }
}