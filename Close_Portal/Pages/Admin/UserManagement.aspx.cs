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
        protected override int RequiredRoleId => RoleLevel.Owner;

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e) {
            if (!IsPostBack) {
                LoadStats();
                LoadWmsFilter();
                LoadRoles();
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
                        FROM Users";

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

                    string sqlWms = "SELECT COUNT(*) FROM WMS WHERE Active = 1";
                    using (SqlCommand cmd = new SqlCommand(sqlWms, conn)) {
                        litActiveWms.Text = cmd.ExecuteScalar().ToString();
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadStats: {ex.Message}");
            }
        }

        // ============================================================
        // LOAD WMS FILTER (toolbar)
        // ============================================================
        private void LoadWmsFilter() {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = "SELECT WMS_Id, WMS_Code, WMS_Name FROM WMS WHERE Active = 1 ORDER BY WMS_Code";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);
                        rptWmsFilter.DataSource = dt;
                        rptWmsFilter.DataBind();
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadWmsFilter: {ex.Message}");
            }
        }

        // ============================================================
        // LOAD ROLES
        // ============================================================
        private void LoadRoles() {
            try {
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = "SELECT Role_Id, Role_Name FROM Users_Roles ORDER BY Role_Id DESC";
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        conn.Open();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);
                        rptRoles.DataSource = dt;
                        rptRoles.DataBind();
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR LoadRoles: {ex.Message}");
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
                            r.Role_Id, r.Role_Name
                        FROM Users u
                        INNER JOIN Users_Roles r ON u.Role_Id = r.Role_Id
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
                                    RoleName = reader["Role_Name"].ToString()
                                });
                            }
                        }
                    }

                    foreach (var user in users) {
                        using (SqlConnection c2 = new SqlConnection(_connStr)) {
                            c2.Open();
                            user.WmsCodes = GetUserWmsCodes(c2, user.UserId);
                            user.WmsTagsHtml = BuildLocationTagsHtml(c2, user.UserId);
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
        //    Deriva WMS desde Users_OMS → OMS → WMS
        private string GetUserWmsCodes(SqlConnection conn, int userId) {
            string sql = @"
                SELECT DISTINCT w.WMS_Code
                FROM Users_OMS uo
                INNER JOIN OMS o ON o.OMS_Id = uo.OMS_Id
                INNER JOIN WMS w ON w.WMS_Id = o.WMS_Id
                WHERE uo.User_Id = @UserId AND w.Active = 1
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

        // ── Tags HTML de locaciones operativas para la columna de la tabla
        private string BuildLocationTagsHtml(SqlConnection conn, int userId) {
            string sql = @"
                SELECT wl.Location_Name
                FROM Users_Location ul
                INNER JOIN WMS_Location wl ON wl.Location_Id = ul.Location_Id
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

        private static (string Email, string Username, string RoleName, int RoleId) GetUserInfo(int userId) {
            using (SqlConnection conn = new SqlConnection(_connStr)) {
                string sql = @"
                    SELECT u.Email, u.Username, r.Role_Name, r.Role_Id
                    FROM Users u
                    INNER JOIN Users_Roles r ON u.Role_Id = r.Role_Id
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
        //   OmsList      → OMS visibles al admin (mismo WMS padre), con Assigned
        //                  del target en Users_OMS
        //   LocationList → Locaciones visibles al admin (tienen OMS del admin),
        //                  con Assigned del target en Users_Location y OmsIds[]
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
                               u.Login_Type, u.Role_Id, r.Role_Name
                        FROM Users u
                        INNER JOIN Users_Roles r ON u.Role_Id = r.Role_Id
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
                                    RoleName = r["Role_Name"].ToString()
                                };
                            }
                        }
                    }

                    if (user == null)
                        return new { Success = false, Message = "Usuario no encontrado" };

                    // ── 2. OMS visibles al admin, con Assigned del target ────
                    //    Owner  → todos los OMS activos
                    //    Otros  → OMS cuyo WMS_Id está en los WMS del admin
                    //             (derivado de Users_OMS del admin)
                    string sqlOms = @"
                        SELECT
                            o.OMS_Id,
                            o.OMS_Code,
                            o.OMS_Name,
                            w.WMS_Id,
                            w.WMS_Code,
                            CASE WHEN uo_t.User_Id IS NOT NULL THEN 1 ELSE 0 END AS Assigned
                        FROM OMS o
                        INNER JOIN WMS     w    ON w.WMS_Id   = o.WMS_Id
                        LEFT  JOIN Users_OMS uo_t ON uo_t.OMS_Id = o.OMS_Id
                                                 AND uo_t.User_Id = @UserId
                        WHERE w.Active = 1
                        " + (isOwner ? "" : @"
                          AND w.WMS_Id IN (
                              SELECT DISTINCT o2.WMS_Id
                              FROM Users_OMS uo2
                              INNER JOIN OMS o2 ON o2.OMS_Id = uo2.OMS_Id
                              WHERE uo2.User_Id = @CurrentUserId
                          )") + @"
                        ORDER BY w.WMS_Code, o.OMS_Code";

                    var omsList = new List<object>();
                    using (SqlCommand cmd = new SqlCommand(sqlOms, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        if (!isOwner)
                            cmd.Parameters.AddWithValue("@CurrentUserId", currentUserId);
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                omsList.Add(new {
                                    OmsId = (int)r["OMS_Id"],
                                    OmsCode = r["OMS_Code"].ToString(),
                                    OmsName = r["OMS_Name"].ToString(),
                                    WmsId = (int)r["WMS_Id"],
                                    WmsCode = r["WMS_Code"].ToString(),
                                    Assigned = (int)r["Assigned"] == 1
                                });
                            }
                        }
                    }

                    // ── 3. Locaciones visibles al admin, con Assigned del target
                    //    Visibilidad: locaciones con al menos un OMS del admin en Users_OMS
                    //    Cada ítem incluye OmsIds[] para que JS filtre por OMS seleccionados
                    string sqlLoc = @"
                        SELECT
                            wl.Location_Id,
                            wl.Location_Name,
                            lo.OMS_Id,
                            o.OMS_Code,
                            CASE WHEN ul.User_Id IS NOT NULL THEN 1 ELSE 0 END AS Assigned
                        FROM WMS_Location wl
                        LEFT JOIN Location_OMS   lo ON lo.Location_Id = wl.Location_Id
                        LEFT JOIN OMS            o  ON o.OMS_Id       = lo.OMS_Id
                        LEFT JOIN Users_Location ul ON ul.Location_Id = wl.Location_Id
                                                   AND ul.User_Id     = @UserId
                        WHERE wl.Active = 1
                        " + (isOwner ? "" : @"
                          AND EXISTS (
                              SELECT 1
                              FROM Location_OMS lo2
                              INNER JOIN Users_OMS uo ON uo.OMS_Id  = lo2.OMS_Id
                                                     AND uo.User_Id = @CurrentUserId
                              WHERE lo2.Location_Id = wl.Location_Id
                          )") + @"
                        ORDER BY wl.Location_Name, o.OMS_Code";

                    var locMap = new Dictionary<int, (string Name, bool Assigned, List<int> OmsIds, List<string> OmsCodes)>();

                    using (SqlCommand cmd = new SqlCommand(sqlLoc, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        if (!isOwner)
                            cmd.Parameters.AddWithValue("@CurrentUserId", currentUserId);
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                int locId = (int)r["Location_Id"];
                                if (!locMap.ContainsKey(locId))
                                    locMap[locId] = (r["Location_Name"].ToString(),
                                                     (int)r["Assigned"] == 1,
                                                     new List<int>(),
                                                     new List<string>());
                                if (r["OMS_Id"] != DBNull.Value) {
                                    locMap[locId].OmsIds.Add((int)r["OMS_Id"]);
                                    locMap[locId].OmsCodes.Add(r["OMS_Code"].ToString());
                                }
                            }
                        }
                    }

                    var locationList = new List<object>();
                    foreach (var kv in locMap) {
                        locationList.Add(new {
                            LocationId = kv.Key,
                            LocationName = kv.Value.Name,
                            OmsIds = kv.Value.OmsIds.ToArray(),
                            OmsLabel = kv.Value.OmsCodes.Count > 0
                                           ? string.Join(", ", kv.Value.OmsCodes)
                                           : "—",
                            Assigned = kv.Value.Assigned
                        });
                    }

                    return new {
                        Success = true,
                        UserId = user.UserId,
                        Email = user.Email,
                        Username = user.Username ?? user.Email.Split('@')[0],
                        Initials = user.Initials,
                        RoleId = user.RoleId,
                        Active = user.Active,
                        Locked = user.Locked,
                        OmsList = omsList,
                        LocationList = locationList
                    };
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetUserDetail: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — GetAllOms
        // Para el modal de nuevo usuario. Misma visibilidad que GetUserDetail.
        // Devuelve todos los OMS disponibles con Assigned = false.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAllOms() {
            try {
                var session = System.Web.HttpContext.Current.Session;
                int currentRoleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
                int currentUserId = session["UserId"] != null ? (int)session["UserId"] : -1;
                bool isOwner = currentRoleId >= RoleLevel.Owner;

                string sql = @"
                    SELECT
                        o.OMS_Id,
                        o.OMS_Code,
                        o.OMS_Name,
                        w.WMS_Id,
                        w.WMS_Code
                    FROM OMS o
                    INNER JOIN WMS w ON w.WMS_Id = o.WMS_Id
                    WHERE w.Active = 1
                    " + (isOwner ? "" : @"
                      AND w.WMS_Id IN (
                          SELECT DISTINCT o2.WMS_Id
                          FROM Users_OMS uo2
                          INNER JOIN OMS o2 ON o2.OMS_Id = uo2.OMS_Id
                          WHERE uo2.User_Id = @CurrentUserId
                      )") + @"
                    ORDER BY w.WMS_Code, o.OMS_Code";

                var omsList = new List<object>();
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        if (!isOwner)
                            cmd.Parameters.AddWithValue("@CurrentUserId", currentUserId);
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                omsList.Add(new {
                                    OmsId = (int)r["OMS_Id"],
                                    OmsCode = r["OMS_Code"].ToString(),
                                    OmsName = r["OMS_Name"].ToString(),
                                    WmsId = (int)r["WMS_Id"],
                                    WmsCode = r["WMS_Code"].ToString(),
                                    Assigned = false
                                });
                            }
                        }
                    }
                }

                return new { Success = true, Data = omsList };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetAllOms: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — GetAllLocations
        // Para el modal de nuevo usuario. Visibilidad via Users_OMS del admin.
        // Devuelve OmsIds[] para que JS filtre dinámicamente.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAllLocations() {
            try {
                var session = System.Web.HttpContext.Current.Session;
                int currentRoleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
                int currentUserId = session["UserId"] != null ? (int)session["UserId"] : -1;
                bool isOwner = currentRoleId >= RoleLevel.Owner;

                string sql = @"
                    SELECT
                        wl.Location_Id,
                        wl.Location_Name,
                        lo.OMS_Id,
                        o.OMS_Code
                    FROM WMS_Location wl
                    LEFT JOIN Location_OMS lo ON lo.Location_Id = wl.Location_Id
                    LEFT JOIN OMS          o  ON o.OMS_Id       = lo.OMS_Id
                    WHERE wl.Active = 1
                    " + (isOwner ? "" : @"
                      AND EXISTS (
                          SELECT 1
                          FROM Location_OMS lo2
                          INNER JOIN Users_OMS uo ON uo.OMS_Id  = lo2.OMS_Id
                                                 AND uo.User_Id = @CurrentUserId
                          WHERE lo2.Location_Id = wl.Location_Id
                      )") + @"
                    ORDER BY wl.Location_Name, o.OMS_Code";

                var locMap = new Dictionary<int, (string Name, List<int> OmsIds, List<string> OmsCodes)>();

                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        if (!isOwner)
                            cmd.Parameters.AddWithValue("@CurrentUserId", currentUserId);
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                int locId = (int)r["Location_Id"];
                                if (!locMap.ContainsKey(locId))
                                    locMap[locId] = (r["Location_Name"].ToString(), new List<int>(), new List<string>());
                                if (r["OMS_Id"] != DBNull.Value) {
                                    locMap[locId].OmsIds.Add((int)r["OMS_Id"]);
                                    locMap[locId].OmsCodes.Add(r["OMS_Code"].ToString());
                                }
                            }
                        }
                    }
                }

                var list = new List<object>();
                foreach (var kv in locMap) {
                    list.Add(new {
                        LocationId = kv.Key,
                        LocationName = kv.Value.Name,
                        OmsIds = kv.Value.OmsIds.ToArray(),
                        OmsLabel = kv.Value.OmsCodes.Count > 0
                                       ? string.Join(", ", kv.Value.OmsCodes)
                                       : "—",
                        Assigned = false
                    });
                }

                return new { Success = true, Data = list };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetAllLocations: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — SaveUserChanges
        // Persiste en una sola transacción:
        //   1. UPDATE Users
        //   2. DELETE + INSERT Users_OMS    (scope de visibilidad)
        //   3. DELETE + INSERT Users_Location (locaciones operativas)
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SaveUserChanges(
            int userId, int roleId, bool active, bool locked,
            int[] omsIds,
            int[] locationIds,
            string username,
            string newPassword) {
            try {
                System.Diagnostics.Debug.WriteLine(
                    $"=== SaveUserChanges UserId:{userId} RoleId:{roleId} Active:{active} Locked:{locked} ===");

                var session = System.Web.HttpContext.Current.Session;
                int currentRoleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;

                var (targetEmail, targetUsername, _, targetRoleId) = GetUserInfo(userId);

                if (targetRoleId >= currentRoleId)
                    return new { Success = false, Message = "No tienes permisos para modificar este usuario" };

                if (string.IsNullOrWhiteSpace(username))
                    return new { Success = false, Message = "El username no puede estar vacío" };

                string newRoleName = "";
                using (SqlConnection connRole = new SqlConnection(_connStr)) {
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT Role_Name FROM Users_Roles WHERE Role_Id = @RoleId", connRole)) {
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
                            // 1. UPDATE Users
                            string sqlUser;
                            SqlCommand cmdUser;

                            if (!string.IsNullOrEmpty(newPassword)) {
                                string hash = ComputePasswordHash(newPassword);
                                sqlUser = @"
                                    UPDATE Users
                                    SET Role_Id       = @RoleId,
                                        Active        = @Active,
                                        Locked        = @Locked,
                                        Lock_Date     = CASE WHEN @Locked = 1 THEN GETDATE() ELSE NULL END,
                                        Username      = @Username,
                                        Password_Hash = @PasswordHash
                                    WHERE User_Id = @UserId";
                                cmdUser = new SqlCommand(sqlUser, conn, tx);
                                cmdUser.Parameters.AddWithValue("@PasswordHash", hash);
                            } else {
                                sqlUser = @"
                                    UPDATE Users
                                    SET Role_Id   = @RoleId,
                                        Active    = @Active,
                                        Locked    = @Locked,
                                        Lock_Date = CASE WHEN @Locked = 1 THEN GETDATE() ELSE NULL END,
                                        Username  = @Username
                                    WHERE User_Id = @UserId";
                                cmdUser = new SqlCommand(sqlUser, conn, tx);
                            }

                            cmdUser.Parameters.AddWithValue("@UserId", userId);
                            cmdUser.Parameters.AddWithValue("@RoleId", roleId);
                            cmdUser.Parameters.AddWithValue("@Active", active);
                            cmdUser.Parameters.AddWithValue("@Locked", locked);
                            cmdUser.Parameters.AddWithValue("@Username", username.Trim());
                            cmdUser.ExecuteNonQuery();

                            // 2. Reemplazar Users_OMS (scope de visibilidad)
                            using (SqlCommand cmd = new SqlCommand(
                                "DELETE FROM Users_OMS WHERE User_Id = @UserId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.ExecuteNonQuery();
                            }
                            if (omsIds != null && omsIds.Length > 0) {
                                foreach (int omsId in omsIds) {
                                    using (SqlCommand cmd = new SqlCommand(
                                        "INSERT INTO Users_OMS (User_Id, OMS_Id) VALUES (@UserId, @OmsId)",
                                        conn, tx)) {
                                        cmd.Parameters.AddWithValue("@UserId", userId);
                                        cmd.Parameters.AddWithValue("@OmsId", omsId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 3. Reemplazar Users_Location (locaciones operativas)
                            using (SqlCommand cmd = new SqlCommand(
                                "DELETE FROM Users_Location WHERE User_Id = @UserId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.ExecuteNonQuery();
                            }
                            if (locationIds != null && locationIds.Length > 0) {
                                foreach (int locationId in locationIds) {
                                    using (SqlCommand cmd = new SqlCommand(
                                        "INSERT INTO Users_Location (User_Id, Location_Id) VALUES (@UserId, @LocationId)",
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

                return new { Success = true, Message = "Cambios guardados correctamente" };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR SaveUserChanges: {ex.Message}");
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
                    string sql = "UPDATE Users SET Active = @Active WHERE User_Id = @UserId";
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

                return new { Success = true, Message = active ? "Usuario activado" : "Usuario desactivado" };

            } catch (Exception ex) {
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — CreateUser
        // Crea un nuevo usuario Google con rol y OMS asignados.
        // Solo accesible para Owner (hereda RequiredRoleId de la página).
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object CreateUser(string email, string username, int roleId, int[] omsIds) {
            try {
                SecurePage.CheckAccess(RoleLevel.Owner);

                var session = System.Web.HttpContext.Current.Session;
                string createdBy = session["Email"]?.ToString() ?? "Sistema";

                // No se puede asignar rol Owner
                if (roleId >= RoleLevel.Owner)
                    return new { Success = false, Message = "No se puede crear un usuario con rol Owner" };

                // Email requerido y dominio correcto
                if (string.IsNullOrWhiteSpace(email) || !email.Trim().ToLower().EndsWith("@novamex.com"))
                    return new { Success = false, Message = "El email debe ser @novamex.com" };

                // Email duplicado
                using (SqlConnection connCheck = new SqlConnection(_connStr)) {
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM Users WHERE Email = @Email", connCheck)) {
                        cmd.Parameters.AddWithValue("@Email", email.Trim().ToLower());
                        connCheck.Open();
                        if ((int)cmd.ExecuteScalar() > 0)
                            return new { Success = false, Message = "Ya existe un usuario con ese email" };
                    }
                }

                // Obtener nombre del rol para el correo
                string roleName = "";
                using (SqlConnection connRole = new SqlConnection(_connStr)) {
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT Role_Name FROM Users_Roles WHERE Role_Id = @RoleId", connRole)) {
                        cmd.Parameters.AddWithValue("@RoleId", roleId);
                        connRole.Open();
                        roleName = cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                }

                int newUserId = -1;
                string resolvedUsername = string.IsNullOrWhiteSpace(username)
                    ? email.Trim().ToLower().Split('@')[0]
                    : username.Trim();

                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction()) {
                        try {
                            // 1. INSERT Users
                            using (SqlCommand cmd = new SqlCommand(@"
                                INSERT INTO Users (Email, Username, Login_Type, Role_Id, Active, Locked)
                                VALUES (@Email, @Username, 'Google', @RoleId, 1, 0);
                                SELECT SCOPE_IDENTITY();", conn, tx)) {
                                cmd.Parameters.AddWithValue("@Email", email.Trim().ToLower());
                                cmd.Parameters.AddWithValue("@Username", resolvedUsername);
                                cmd.Parameters.AddWithValue("@RoleId", roleId);
                                newUserId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // 2. INSERT Users_OMS (scope de visibilidad)
                            if (omsIds != null && omsIds.Length > 0) {
                                foreach (int omsId in omsIds) {
                                    using (SqlCommand cmd = new SqlCommand(
                                        "INSERT INTO Users_OMS (User_Id, OMS_Id) VALUES (@UserId, @OmsId)",
                                        conn, tx)) {
                                        cmd.Parameters.AddWithValue("@UserId", newUserId);
                                        cmd.Parameters.AddWithValue("@OmsId", omsId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            tx.Commit();

                        } catch (Exception ex) {
                            tx.Rollback();
                            System.Diagnostics.Debug.WriteLine($"[CreateUser] Rollback: {ex.Message}");
                            return new { Success = false, Message = "Error al crear el usuario" };
                        }
                    }
                }

                System.Threading.Tasks.Task.Run(() =>
                    EmailService.NotifyUserAdded(
                        targetEmail: email,
                        targetUsername: resolvedUsername,
                        targetRole: roleName,
                        performedByEmail: createdBy
                    )
                );

                return new { Success = true, Message = "Usuario creado correctamente", UserId = newUserId };

            } catch (UnauthorizedAccessException ex) {
                return new { Success = false, Message = ex.Message };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[CreateUser] ERROR: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }
    }
}