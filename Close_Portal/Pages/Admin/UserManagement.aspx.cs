using Close_Portal.Core;
using Close_Portal.Models;
using Close_Portal.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web.Services;
using System.Web.UI;

namespace Close_Portal.Pages.Admin {
    public partial class UserManagement : SecurePage {
        protected override int RequiredRoleId => RoleLevel.Administrador;

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e) {
            if (!IsPostBack) {
                LoadStats();
                LoadLocationsFilter();
                LoadRoles();
                LoadDepartments();
                LoadUsers();
            }
        }

        // ============================================================
        // LOAD STATS
        // ============================================================
        private void LoadStats() {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT
                            COUNT(*)                                                    AS Total,
                            SUM(CASE WHEN Active = 1 AND Locked = 0 THEN 1 ELSE 0 END) AS Activos,
                            SUM(CASE WHEN Locked = 1 THEN 1 ELSE 0 END)                AS Bloqueados
                        FROM MonthEnd_Users";

                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            if (r.Read()) {
                                litTotalUsers.Text = r["Total"].ToString();
                                litActiveUsers.Text = r["Activos"].ToString();
                                litLockedUsers.Text = r["Bloqueados"].ToString();
                            }
                        }
                    }


                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadStats: {ex.Message}");
            }
        }

        // ============================================================
        // LOAD LOCATIONS FILTER (toolbar)
        // ============================================================
        private void LoadLocationsFilter() {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = "SELECT Location_Id, Location_Name FROM MonthEnd_Locations WHERE Active = 1 ORDER BY Location_Name";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);
                        rptLocationsFilter.DataSource = dt;
                        rptLocationsFilter.DataBind();
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadLocationsFilter: {ex.Message}");
            }
        }

        // ============================================================
        // LOAD ROLES
        // ============================================================
        private void LoadRoles() {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = "SELECT Role_Id, Role_Name FROM MonthEnd_Users_Roles ORDER BY Role_Id DESC";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        // Ni edit ni new permiten asignar Owner (Role_Id = 4)
                        dt.DefaultView.RowFilter = "Role_Id < 4";
                        DataTable filtered = dt.DefaultView.ToTable();

                        rptRoles.DataSource = filtered;
                        rptRoles.DataBind();
                        rptRolesNew.DataSource = filtered;
                        rptRolesNew.DataBind();

                        dt.DefaultView.RowFilter = "";
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadRoles: {ex.Message}");
            }
        }

        // ============================================================
        // LOAD DEPARTMENTS
        // ============================================================
        private void LoadDepartments() {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = "SELECT Department_Id, Department_Code, Department_Name FROM MonthEnd_Departments WHERE Active = 1 ORDER BY Department_Code";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);
                        rptDepartments.DataSource = dt;
                        rptDepartments.DataBind();
                        rptDepartmentsFilter.DataSource = dt;
                        rptDepartmentsFilter.DataBind();
                        rptDepartmentsNew.DataSource = dt;
                        rptDepartmentsNew.DataBind();
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadDepartments: {ex.Message}");
            }
        }

        // ============================================================
        // LOAD USERS
        // ============================================================
        private void LoadUsers() {
            try {
                List<UserManagementModel> users = new List<UserManagementModel>();

                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT
                            u.User_Id, u.Email, u.Username,
                            u.Active, u.Locked, u.Login_Type,
                            r.Role_Id, r.Role_Name,
                            d.Department_Id, d.Department_Code, d.Department_Name
                        FROM MonthEnd_Users u
                        INNER JOIN MonthEnd_Users_Roles r ON u.Role_Id = r.Role_Id
                        LEFT  JOIN MonthEnd_Departments d ON d.Department_Id = u.Department_Id
                        ORDER BY u.Role_Id DESC, u.Username";

                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader()) {
                            while (reader.Read()) {
                                users.Add(new UserManagementModel {
                                    UserId = (int)reader["User_Id"],
                                    Email = reader["Email"].ToString(),
                                    Username = reader["Username"]?.ToString(),
                                    Active = (bool)reader["Active"],
                                    Locked = (bool)reader["Locked"],
                                    LoginType = reader["Login_Type"].ToString(),
                                    RoleId = (int)reader["Role_Id"],
                                    RoleName = reader["Role_Name"].ToString(),
                                    DepartmentId = reader["Department_Id"] != DBNull.Value ? (int?)reader["Department_Id"] : null,
                                    DepartmentCode = reader["Department_Code"]?.ToString(),
                                    DepartmentName = reader["Department_Name"]?.ToString()
                                });
                            }
                        }
                    }

                    foreach (var user in users) {
                        using (SqlConnection c2 = new SqlConnection(_connStr)) {
                            c2.Open();
                            user.WmsCodes = GetUserWmsCodes(c2, user.UserId);
                            user.WmsTagsHtml = BuildLocationTagsHtml(c2, user.UserId);
                            user.LocationNames = GetUserLocationNames(c2, user.UserId);
                        }
                    }
                }

                if (users.Count == 0) pnlEmpty.Visible = true;
                rptUsers.DataSource = users;
                rptUsers.DataBind();

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadUsers: {ex.Message}");
            }
        }

        // ── WmsCodes para data-wms del <tr> (filtro toolbar)
        //    Deriva WMS_Code directamente desde MonthEnd_Users_WMS
        private static string GetUserWmsCodes(SqlConnection conn, int userId) {
            string sql = @"
                SELECT DISTINCT w.WMS_Code
                FROM MonthEnd_Users_WMS uw
                INNER JOIN MonthEnd_WMS w ON w.WMS_Id = uw.WMS_Id
                WHERE uw.User_Id = @UserId AND w.Active = 1
                ORDER BY w.WMS_Code";

            using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@UserId", userId);
                var codes = new List<string>();
                using (SqlDataReader r = cmd.ExecuteReader()) {
                    while (r.Read()) codes.Add(r["WMS_Code"].ToString());
                }
                return string.Join(",", codes);
            }
        }

        // ── LocationNames para data-location del <tr> (filtro toolbar)
        private static string GetUserLocationNames(SqlConnection conn, int userId) {
            string sql = @"
                SELECT wl.Location_Name
                FROM MonthEnd_Users_Location ul
                INNER JOIN MonthEnd_Locations wl ON wl.Location_Id = ul.Location_Id
                WHERE ul.User_Id = @UserId AND wl.Active = 1
                ORDER BY wl.Location_Name";

            using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@UserId", userId);
                var names = new List<string>();
                using (SqlDataReader r = cmd.ExecuteReader()) {
                    while (r.Read()) names.Add(r["Location_Name"].ToString());
                }
                return string.Join(",", names);
            }
        }

        // ── Tags HTML de locaciones operativas para la columna de la tabla
        private static string BuildLocationTagsHtml(SqlConnection conn, int userId) {
            string sql = @"
                SELECT wl.Location_Name
                FROM MonthEnd_Users_Location ul
                INNER JOIN MonthEnd_Locations wl ON wl.Location_Id = ul.Location_Id
                WHERE ul.User_Id = @UserId AND wl.Active = 1
                ORDER BY wl.Location_Name";

            var sb = new StringBuilder();
            using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@UserId", userId);
                using (SqlDataReader r = cmd.ExecuteReader()) {
                    while (r.Read())
                        sb.Append($"<span class='wms-tag'>{r["Location_Name"]}</span>");
                }
            }
            return sb.Length > 0
                ? sb.ToString()
                : "<span style='color:var(--text-muted);font-size:11px'>Sin asignar</span>";
        }

        // ============================================================
        // GET USER ROW DATA
        // Retorna todos los campos necesarios para renderizar / actualizar
        // una fila de la tabla sin recargar la página.
        // ============================================================
        private static object GetUserRowData(int userId) {
            try {
                int currentRoleId = System.Web.HttpContext.Current.Session["RoleId"] != null
                    ? (int)System.Web.HttpContext.Current.Session["RoleId"] : -1;

                var user = new UserManagementModel();
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    using (var cmd = new SqlCommand(@"
                        SELECT u.User_Id, u.Email, u.Username, u.Active, u.Locked,
                               u.Login_Type, r.Role_Id, r.Role_Name,
                               d.Department_Id, d.Department_Code, d.Department_Name
                        FROM MonthEnd_Users u
                        INNER JOIN MonthEnd_Users_Roles r ON u.Role_Id = r.Role_Id
                        LEFT  JOIN MonthEnd_Departments d ON d.Department_Id = u.Department_Id
                        WHERE u.User_Id = @UserId", conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var r = cmd.ExecuteReader()) {
                            if (!r.Read()) return null;
                            user.UserId       = (int)r["User_Id"];
                            user.Email        = r["Email"].ToString();
                            user.Username     = r["Username"]?.ToString();
                            user.Active       = (bool)r["Active"];
                            user.Locked       = (bool)r["Locked"];
                            user.LoginType    = r["Login_Type"].ToString();
                            user.RoleId       = (int)r["Role_Id"];
                            user.RoleName     = r["Role_Name"].ToString();
                            user.DepartmentId   = r["Department_Id"] != DBNull.Value ? (int?)r["Department_Id"] : null;
                            user.DepartmentCode = r["Department_Code"]?.ToString();
                            user.DepartmentName = r["Department_Name"]?.ToString();
                        }
                    }

                    user.WmsCodes      = GetUserWmsCodes(conn, userId);
                    user.WmsTagsHtml   = BuildLocationTagsHtml(conn, userId);
                    user.LocationNames = GetUserLocationNames(conn, userId);
                }

                return new {
                    UserId          = user.UserId,
                    Email           = user.Email,
                    Username        = user.Username ?? user.Email.Split('@')[0],
                    Initials        = user.Initials,
                    RoleId          = user.RoleId,
                    RoleName        = user.RoleName,
                    RoleBadge       = user.RoleBadge,
                    DepartmentCode  = user.DepartmentCode ?? "",
                    DepartmentName  = user.DepartmentName ?? "",
                    WmsCodes        = user.WmsCodes ?? "",
                    WmsTagsHtml     = user.WmsTagsHtml,
                    LocationNames   = user.LocationNames ?? "",
                    LoginIcon       = user.LoginIcon,
                    LoginTypeLabel  = user.LoginTypeLabel,
                    StatusLabel     = user.StatusLabel,
                    StatusBadge     = user.StatusBadge,
                    Active          = user.Active,
                    Locked          = user.Locked,
                    CurrentRoleId   = currentRoleId
                };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetUserRowData] ERROR: {ex.Message}");
                return null;
            }
        }

        private static (string Email, string Username, string RoleName, int RoleId) GetUserInfo(int userId) {
            using (SqlConnection conn = new SqlConnection(_connStr)) {
                string sql = @"
                    SELECT u.Email, u.Username, r.Role_Name, r.Role_Id
                    FROM MonthEnd_Users u
                    INNER JOIN MonthEnd_Users_Roles r ON u.Role_Id = r.Role_Id
                    WHERE u.User_Id = @UserId";

                using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader()) {
                        if (r.Read())
                            return (r["Email"].ToString(),
                                    r["Username"]?.ToString(),
                                    r["Role_Name"].ToString(),
                                    (int)r["Role_Id"]);
                    }
                }
            }
            return (null, null, null, -1);
        }

        private static string ComputePasswordHash(string plainText) {
            return BCrypt.Net.BCrypt.HashPassword(plainText, workFactor: 11);
        }

        // ============================================================
        // WEBMETHOD — GetUserDetail
        // Devuelve:
        //   OmsList      → MonthEnd_OMS visibles al admin (mismo MonthEnd_WMS padre), con Assigned
        //                  del target en MonthEnd_Users_WMS
        //   LocationList → Locaciones visibles al admin (tienen MonthEnd_OMS del admin),
        //                  con Assigned del target en MonthEnd_Users_Location y OmsIds[]
        //                  para filtrado dinámico en JS
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetUserDetail(int userId) {
            try {
                var session = System.Web.HttpContext.Current.Session;
                int currentRoleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
                int currentUserId = session["UserId"] != null ? (int)session["UserId"] : -1;
                bool isOwner = currentRoleId >= RoleLevel.Owner;

                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // ── 1. Datos básicos del usuario ─────────────────────────
                    string sqlUser = @"
                        SELECT u.User_Id, u.Email, u.Username, u.Active, u.Locked,
                               u.Login_Type, u.Role_Id, r.Role_Name,
                               d.Department_Id, d.Department_Code, d.Department_Name
                        FROM MonthEnd_Users u
                        INNER JOIN MonthEnd_Users_Roles r ON u.Role_Id = r.Role_Id
                        LEFT  JOIN MonthEnd_Departments d ON d.Department_Id = u.Department_Id
                        WHERE u.User_Id = @UserId";

                    UserManagementModel user = null;
                    using (SqlCommand cmd = new SqlCommand(sqlUser, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            if (r.Read()) {
                                user = new UserManagementModel {
                                    UserId = (int)r["User_Id"],
                                    Email = r["Email"].ToString(),
                                    Username = r["Username"]?.ToString(),
                                    Active = (bool)r["Active"],
                                    Locked = (bool)r["Locked"],
                                    LoginType = r["Login_Type"].ToString(),
                                    RoleId = (int)r["Role_Id"],
                                    RoleName = r["Role_Name"].ToString(),
                                    DepartmentId = r["Department_Id"] != DBNull.Value ? (int?)r["Department_Id"] : null,
                                    DepartmentCode = r["Department_Code"]?.ToString(),
                                    DepartmentName = r["Department_Name"]?.ToString()
                                };
                            }
                        }
                    }

                    if (user == null)
                        return new { Success = false, Message = "Usuario no encontrado" };

                    // ── 2. WMS asignados al usuario target (Assigned) + todos los activos
                    //    Owner  → todos los WMS activos
                    //    Otros  → todos los WMS activos (sin restricción — OMS ya no filtra)
                    string sqlWms = @"
                        SELECT
                            w.WMS_Id,
                            w.WMS_Code,
                            w.WMS_Name,
                            CASE WHEN uw_t.User_Id IS NOT NULL THEN 1 ELSE 0 END AS Assigned
                        FROM MonthEnd_WMS w
                        LEFT JOIN MonthEnd_Users_WMS uw_t ON uw_t.WMS_Id = w.WMS_Id
                                                         AND uw_t.User_Id = @UserId
                        WHERE w.Active = 1
                        ORDER BY w.WMS_Code";

                    var wmsList = new List<object>();
                    using (SqlCommand cmd = new SqlCommand(sqlWms, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                wmsList.Add(new {
                                    WmsId = (int)r["WMS_Id"],
                                    WmsCode = r["WMS_Code"].ToString(),
                                    WmsName = r["WMS_Name"].ToString(),
                                    Assigned = (int)r["Assigned"] == 1
                                });
                            }
                        }
                    }

                    // ── 3. Todas las locaciones activas, con Assigned del target
                    string sqlLoc = @"
                        SELECT
                            wl.Location_Id,
                            wl.Location_Name,
                            CASE WHEN ul.User_Id IS NOT NULL THEN 1 ELSE 0 END AS Assigned
                        FROM MonthEnd_Locations wl
                        LEFT JOIN MonthEnd_Users_Location ul ON ul.Location_Id = wl.Location_Id
                                                   AND ul.User_Id     = @UserId
                        WHERE wl.Active = 1
                        ORDER BY wl.Location_Name";

                    var locationList = new List<object>();

                    using (SqlCommand cmd = new SqlCommand(sqlLoc, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                locationList.Add(new {
                                    LocationId = (int)r["Location_Id"],
                                    LocationName = r["Location_Name"].ToString(),
                                    Assigned = (int)r["Assigned"] == 1
                                });
                            }
                        }
                    }

                    return new {
                        Success = true,
                        UserId = user.UserId,
                        Email = user.Email,
                        Username = user.Username ?? user.Email.Split('@')[0],
                        Initials = user.Initials,
                        RoleId = user.RoleId,
                        LoginType = user.LoginType,
                        Active = user.Active,
                        Locked = user.Locked,
                        DepartmentId = user.DepartmentId,
                        DepartmentCode = user.DepartmentCode,
                        DepartmentName = user.DepartmentName,
                        WmsList = wmsList,
                        LocationList = locationList
                    };
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetUserDetail: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — GetAllWms
        // Para el modal de usuario. Devuelve todos los WMS activos.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAllWms() {
            try {
                var wmsList = new List<object>();
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT WMS_Id, WMS_Code, WMS_Name
                        FROM   MonthEnd_WMS
                        WHERE  Active = 1
                        ORDER BY WMS_Code";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                wmsList.Add(new {
                                    WmsId = (int)r["WMS_Id"],
                                    WmsCode = r["WMS_Code"].ToString(),
                                    WmsName = r["WMS_Name"].ToString(),
                                    Assigned = false
                                });
                            }
                        }
                    }
                }
                return new { Success = true, Data = wmsList };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetAllWms: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — GetAllLocations
        // Devuelve todas las locaciones activas.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAllLocations() {
            try {
                var list = new List<object>();
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT Location_Id, Location_Name
                        FROM   MonthEnd_Locations
                        WHERE  Active = 1
                        ORDER BY Location_Name";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                list.Add(new {
                                    LocationId = (int)r["Location_Id"],
                                    LocationName = r["Location_Name"].ToString(),
                                    Assigned = false
                                });
                            }
                        }
                    }
                }
                return new { Success = true, Data = list };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetAllLocations: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — GetAllDepartments
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAllDepartments() {
            try {
                var list = new List<object>();
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = "SELECT Department_Id, Department_Code, Department_Name FROM MonthEnd_Departments WHERE Active = 1 ORDER BY Department_Code";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                list.Add(new {
                                    DepartmentId = (int)r["Department_Id"],
                                    DepartmentCode = r["Department_Code"].ToString(),
                                    DepartmentName = r["Department_Name"].ToString()
                                });
                            }
                        }
                    }
                }
                return new { Success = true, Data = list };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetAllDepartments: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — SaveUserChanges
        // Persiste en una sola transacción:
        //   1. UPDATE MonthEnd_Users
        //   2. DELETE + INSERT MonthEnd_Users_WMS    (WMS asignados)
        //   3. DELETE + INSERT MonthEnd_Users_Location (locaciones operativas)
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SaveUserChanges(
            int userId, int roleId, bool active, bool locked,
            int[] wmsIds,
            int[] locationIds,
            string username,
            string newPassword,
            int departmentId) {
            try {
                System.Diagnostics.Debug.WriteLine(
                    $"=== SaveUserChanges UserId:{userId} RoleId:{roleId} Active:{active} Locked:{locked} ===");

                var session = System.Web.HttpContext.Current.Session;
                int currentRoleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;

                var (targetEmail, targetUsername, _, targetRoleId) = GetUserInfo(userId);

                if (targetRoleId >= currentRoleId)
                    return new { Success = false, Message = "No tienes permisos para modificar este usuario" };

                // Capturar estado previo de Locked para detectar cambio
                bool previouslyLocked = false;
                using (var c = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(
                    "SELECT Locked FROM MonthEnd_Users WHERE User_Id = @UserId", c)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    c.Open();
                    var val = cmd.ExecuteScalar();
                    previouslyLocked = val != null && val != DBNull.Value && (bool)val;
                }

                if (string.IsNullOrWhiteSpace(username))
                    return new { Success = false, Message = "El username no puede estar vacío" };

                string newRoleName = "";
                using (SqlConnection connRole = new SqlConnection(_connStr)) {
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT Role_Name FROM MonthEnd_Users_Roles WHERE Role_Id = @RoleId", connRole)) {
                        cmd.Parameters.AddWithValue("@RoleId", roleId);
                        connRole.Open();
                        newRoleName = cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                }

                string performedBy = System.Web.HttpContext.Current.Session["Email"]?.ToString() ?? "Sistema";

                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction()) {
                        try {
                            // 1. UPDATE MonthEnd_Users
                            string sqlUser;
                            SqlCommand cmdUser;

                            if (!string.IsNullOrEmpty(newPassword)) {
                                string hash = ComputePasswordHash(newPassword);
                                sqlUser = @"
                                    UPDATE MonthEnd_Users
                                    SET Role_Id       = @RoleId,
                                        Active        = @Active,
                                        Locked        = @Locked,
                                        Lock_Date     = CASE WHEN @Locked = 1 THEN GETDATE() ELSE NULL END,
                                        Username      = @Username,
                                        Department_Id = @DepartmentId,
                                        Password_Hash = @PasswordHash
                                    WHERE User_Id = @UserId";
                                cmdUser = new SqlCommand(sqlUser, conn, tx);
                                cmdUser.Parameters.AddWithValue("@PasswordHash", hash);
                            } else {
                                sqlUser = @"
                                    UPDATE MonthEnd_Users
                                    SET Role_Id       = @RoleId,
                                        Active        = @Active,
                                        Locked        = @Locked,
                                        Lock_Date     = CASE WHEN @Locked = 1 THEN GETDATE() ELSE NULL END,
                                        Username      = @Username,
                                        Department_Id = @DepartmentId
                                    WHERE User_Id = @UserId";
                                cmdUser = new SqlCommand(sqlUser, conn, tx);
                            }

                            cmdUser.Parameters.AddWithValue("@UserId", userId);
                            cmdUser.Parameters.AddWithValue("@RoleId", roleId);
                            cmdUser.Parameters.AddWithValue("@Active", active);
                            cmdUser.Parameters.AddWithValue("@Locked", locked);
                            cmdUser.Parameters.AddWithValue("@Username", username.Trim());
                            cmdUser.Parameters.AddWithValue("@DepartmentId",
                                departmentId > 0 ? (object)departmentId : DBNull.Value);
                            cmdUser.ExecuteNonQuery();

                            // 2. Reemplazar MonthEnd_Users_WMS
                            using (SqlCommand cmd = new SqlCommand(
                                "DELETE FROM MonthEnd_Users_WMS WHERE User_Id = @UserId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.ExecuteNonQuery();
                            }
                            if (wmsIds != null && wmsIds.Length > 0) {
                                foreach (int wmsId in wmsIds) {
                                    using (SqlCommand cmd = new SqlCommand(
                                        "INSERT INTO MonthEnd_Users_WMS (User_Id, WMS_Id) VALUES (@UserId, @WmsId)",
                                        conn, tx)) {
                                        cmd.Parameters.AddWithValue("@UserId", userId);
                                        cmd.Parameters.AddWithValue("@WmsId", wmsId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 3. Reemplazar MonthEnd_Users_Location (locaciones operativas)
                            using (SqlCommand cmd = new SqlCommand(
                                "DELETE FROM MonthEnd_Users_Location WHERE User_Id = @UserId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.ExecuteNonQuery();
                            }
                            if (locationIds != null && locationIds.Length > 0) {
                                foreach (int locationId in locationIds) {
                                    using (SqlCommand cmd = new SqlCommand(
                                        "INSERT INTO MonthEnd_Users_Location (User_Id, Location_Id) VALUES (@UserId, @LocationId)",
                                        conn, tx)) {
                                        cmd.Parameters.AddWithValue("@UserId", userId);
                                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            tx.Commit();
                            System.Diagnostics.Debug.WriteLine("✓ SaveUserChanges — Commit exitoso");

                        } catch (Exception ex) {
                            tx.Rollback();
                            System.Diagnostics.Debug.WriteLine($"ERROR tx.Rollback: {ex.Message}");
                            return new { Success = false, Message = "Error al guardar cambios" };
                        }
                    }
                }

                EmailService.NotifyUserUpdated(
                    targetEmail: targetEmail,
                    targetUsername: targetUsername,
                    newRole: newRoleName,
                    performedByEmail: performedBy
                );

                // Notificaciones de bloqueo/desbloqueo si cambió el estado
                if (locked && !previouslyLocked) {
                    System.Threading.Tasks.Task.Run(() =>
                        EmailService.NotifyUserBlocked(targetEmail, targetUsername, performedBy));
                } else if (!locked && previouslyLocked) {
                    System.Threading.Tasks.Task.Run(() =>
                        EmailService.NotifyUserUnblocked(targetEmail, targetUsername, performedBy));
                }

                return new { Success = true, Message = "Cambios guardados correctamente", Row = GetUserRowData(userId) };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR SaveUserChanges: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — CreateUser
        // Alta de nuevo usuario.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object CreateUser(
            string email, string username, int roleId,
            int[] wmsIds, int[] locationIds, int departmentId) {
            try {
                var session = System.Web.HttpContext.Current.Session;
                int currentRole = session["RoleId"] != null ? (int)session["RoleId"] : -1;
                int currentUser = session["UserId"] != null ? (int)session["UserId"] : -1;
                string createdBy = session["Email"]?.ToString() ?? "Sistema";

                if (roleId >= RoleLevel.Owner)
                    return new { Success = false, Message = "No se puede crear un usuario con rol Owner" };

                if (roleId >= currentRole)
                    return new { Success = false, Message = "No tienes permisos para asignar ese rol" };

                // Email duplicado
                using (var c = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM MonthEnd_Users WHERE Email = @Email", c)) {
                        cmd.Parameters.AddWithValue("@Email", email.Trim().ToLower());
                        c.Open();
                        if ((int)cmd.ExecuteScalar() > 0)
                            return new { Success = false, Message = "Ya existe un usuario con ese email" };
                    }
                }

                string roleName = "";
                using (var c = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(
                        "SELECT Role_Name FROM MonthEnd_Users_Roles WHERE Role_Id = @RoleId", c)) {
                        cmd.Parameters.AddWithValue("@RoleId", roleId);
                        c.Open();
                        roleName = cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                }

                int newUserId = -1;
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();
                    using (var tx = conn.BeginTransaction()) {
                        try {
                            string sqlInsert = @"
                                INSERT INTO MonthEnd_Users
                                    (Email, Username, Login_Type, Role_Id, Active, Locked, Department_Id)
                                VALUES (@Email, @Username, 'Google', @RoleId, 1, 0, @DeptId);
                                SELECT SCOPE_IDENTITY();";
                            using (var cmd = new SqlCommand(sqlInsert, conn, tx)) {
                                cmd.Parameters.AddWithValue("@Email",
                                    email.Trim().ToLower());
                                cmd.Parameters.AddWithValue("@Username",
                                    string.IsNullOrWhiteSpace(username)
                                        ? email.Split('@')[0] : username.Trim());
                                cmd.Parameters.AddWithValue("@RoleId", roleId);
                                cmd.Parameters.AddWithValue("@DeptId",
                                    departmentId > 0 ? (object)departmentId : DBNull.Value);
                                newUserId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            if (wmsIds != null) {
                                foreach (int wmsId in wmsIds) {
                                    using (var cmd = new SqlCommand(
                                        "INSERT INTO MonthEnd_Users_WMS (User_Id, WMS_Id) VALUES (@UserId, @WmsId)",
                                        conn, tx)) {
                                        cmd.Parameters.AddWithValue("@UserId", newUserId);
                                        cmd.Parameters.AddWithValue("@WmsId", wmsId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            if (locationIds != null) {
                                foreach (int locationId in locationIds) {
                                    using (var cmd = new SqlCommand(
                                        "INSERT INTO MonthEnd_Users_Location (User_Id, Location_Id) VALUES (@UserId, @LocationId)",
                                        conn, tx)) {
                                        cmd.Parameters.AddWithValue("@UserId", newUserId);
                                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            tx.Commit();
                            System.Diagnostics.Debug.WriteLine($"[CreateUser] Id={newUserId} Email={email}");
                        } catch (Exception ex) {
                            tx.Rollback();
                            System.Diagnostics.Debug.WriteLine($"[CreateUser] Rollback: {ex.Message}");
                            return new { Success = false, Message = "Error al crear el usuario" };
                        }
                    }
                }

                EmailService.NotifyUserAdded(
                    targetEmail: email, targetUsername: username,
                    targetRole: roleName, performedByEmail: createdBy);

                return new { Success = true, Message = "Usuario creado correctamente", UserId = newUserId };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR CreateUser: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — ToggleUserActive
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object ToggleUserActive(int userId, bool active) {
            try {
                var (targetEmail, targetUsername, _, targetRoleId) = GetUserInfo(userId);
                string performedBy = System.Web.HttpContext.Current.Session["Email"]?.ToString() ?? "Sistema";

                int currentRoleId = System.Web.HttpContext.Current.Session["RoleId"] != null
                    ? (int)System.Web.HttpContext.Current.Session["RoleId"] : -1;

                if (targetRoleId >= currentRoleId)
                    return new { Success = false, Message = "No tienes permisos para modificar este usuario" };

                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = "UPDATE MonthEnd_Users SET Active = @Active WHERE User_Id = @UserId";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@Active", active);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }

                if (active) {
                    EmailService.NotifyUserAdded(
                        targetEmail: targetEmail, targetUsername: targetUsername,
                        targetRole: null, performedByEmail: performedBy);
                } else {
                    EmailService.NotifyUserRemoved(
                        targetEmail: targetEmail, targetUsername: targetUsername,
                        performedByEmail: performedBy);
                }

                return new { Success = true, Message = active ? "Usuario activado" : "Usuario desactivado", Row = GetUserRowData(userId) };

            } catch (Exception ex) {
                return new { Success = false, Message = ex.Message };
            }
        }
    }
}