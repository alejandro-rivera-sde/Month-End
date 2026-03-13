using Close_Portal.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Services;

namespace Close_Portal.Pages {
    public partial class AssignRoles : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Administrador;

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e) { }

        // ============================================================
        // GET USERS
        // Owner(4): ve todos excepto otros Owners, sin restricción de WMS
        // Admin(3): ve solo usuarios que comparten al menos un WMS padre
        //           WMS derivado directamente de Users_OMS → OMS → WMS
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetUsers() {
            try {
                CheckAccess(RoleLevel.Administrador);
                var session = System.Web.HttpContext.Current.Session;
                int callerRole = (int)session["RoleId"];
                int callerId = (int)session["UserId"];

                int maxRoleVisible = callerRole >= RoleLevel.Owner
                    ? RoleLevel.Administrador
                    : RoleLevel.Manager;

                string sql;

                if (callerRole >= RoleLevel.Owner) {
                    sql = @"
                        SELECT
                            u.User_Id, u.Email, u.Username, u.Active,
                            u.Role_Id, r.Role_Name,
                            STUFF((
                                SELECT DISTINCT ', ' + w.WMS_Code
                                FROM Users_OMS uo2
                                INNER JOIN OMS o2 ON o2.OMS_Id = uo2.OMS_Id
                                INNER JOIN WMS w  ON w.WMS_Id  = o2.WMS_Id
                                WHERE uo2.User_Id = u.User_Id AND w.Active = 1
                                FOR XML PATH('')
                            ), 1, 2, '') AS WmsCodes
                        FROM Users u
                        INNER JOIN Users_Roles r ON u.Role_Id = r.Role_Id
                        WHERE u.Role_Id <= @MaxRoleVisible
                          AND u.User_Id <> @CallerId
                        ORDER BY u.Role_Id DESC, u.Username";
                } else {
                    // Admin: solo usuarios que comparten al menos un WMS en Users_OMS
                    sql = @"
                        SELECT DISTINCT
                            u.User_Id, u.Email, u.Username, u.Active,
                            u.Role_Id, r.Role_Name,
                            STUFF((
                                SELECT DISTINCT ', ' + w.WMS_Code
                                FROM Users_OMS uo2
                                INNER JOIN OMS o2 ON o2.OMS_Id = uo2.OMS_Id
                                INNER JOIN WMS w  ON w.WMS_Id  = o2.WMS_Id
                                WHERE uo2.User_Id = u.User_Id AND w.Active = 1
                                FOR XML PATH('')
                            ), 1, 2, '') AS WmsCodes
                        FROM Users u
                        INNER JOIN Users_Roles r ON u.Role_Id = r.Role_Id
                        WHERE u.Role_Id <= @MaxRoleVisible
                          AND u.User_Id <> @CallerId
                          AND EXISTS (
                              SELECT 1
                              FROM Users_OMS uo_t
                              INNER JOIN OMS o_t ON o_t.OMS_Id = uo_t.OMS_Id
                              INNER JOIN Users_OMS uo_c ON uo_c.User_Id = @CallerId
                              INNER JOIN OMS o_c ON o_c.OMS_Id = uo_c.OMS_Id
                              WHERE uo_t.User_Id = u.User_Id
                                AND o_t.WMS_Id   = o_c.WMS_Id
                          )
                        ORDER BY u.Role_Id DESC, u.Username";
                }

                var users = new List<object>();
                using (var conn = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@MaxRoleVisible", maxRoleVisible);
                        cmd.Parameters.AddWithValue("@CallerId", callerId);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                users.Add(new {
                                    userId = (int)r["User_Id"],
                                    email = r["Email"].ToString(),
                                    username = r["Username"]?.ToString() ?? "",
                                    active = (bool)r["Active"],
                                    roleId = (int)r["Role_Id"],
                                    roleName = r["Role_Name"].ToString(),
                                    wmsCodes = r["WmsCodes"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return new { success = true, data = users };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[AssignRoles.GetUsers] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET ASSIGNABLE ROLES
        // Owner: puede asignar hasta Admin(3)
        // Admin: puede asignar hasta Manager(2)
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAssignableRoles() {
            try {
                CheckAccess(RoleLevel.Administrador);
                var session = System.Web.HttpContext.Current.Session;
                int callerRole = (int)session["RoleId"];

                int maxAssignable = callerRole >= RoleLevel.Owner
                    ? RoleLevel.Administrador
                    : RoleLevel.Manager;

                var roles = new List<object>();
                using (var conn = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(@"
                        SELECT Role_Id, Role_Name FROM Users_Roles
                        WHERE Role_Id <= @Max
                        ORDER BY Role_Id DESC", conn)) {
                        cmd.Parameters.AddWithValue("@Max", maxAssignable);
                        conn.Open();
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                                roles.Add(new {
                                    roleId = (int)r["Role_Id"],
                                    roleName = r["Role_Name"].ToString()
                                });
                    }
                }
                return new { success = true, data = roles };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[AssignRoles.GetAssignableRoles] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SAVE ROLE CHANGE
        // Doble validación de jerarquía:
        //   1. target.currentRole <= maxVisible  (puedes verlo)
        //   2. newRoleId <= maxAssignable         (puedes asignarlo)
        // Admin además valida WMS compartido vía Users_OMS
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SaveRoleChange(int targetUserId, int newRoleId) {
            try {
                CheckAccess(RoleLevel.Administrador);
                var session = System.Web.HttpContext.Current.Session;
                int callerRole = (int)session["RoleId"];
                int callerId = (int)session["UserId"];

                if (targetUserId == callerId)
                    return new { success = false, message = "No puedes cambiar tu propio rol." };

                int maxAssignable = callerRole >= RoleLevel.Owner
                    ? RoleLevel.Administrador
                    : RoleLevel.Manager;

                if (newRoleId > maxAssignable)
                    return new { success = false, message = "No tienes permisos para asignar ese rol." };

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // 1. Verificar rol actual del target
                    int currentRole;
                    using (var cmd = new SqlCommand(
                        "SELECT Role_Id FROM Users WHERE User_Id = @UserId", conn)) {
                        cmd.Parameters.AddWithValue("@UserId", targetUserId);
                        var res = cmd.ExecuteScalar();
                        if (res == null)
                            return new { success = false, message = "Usuario no encontrado." };
                        currentRole = (int)res;
                    }

                    if (currentRole > maxAssignable)
                        return new { success = false, message = "No tienes permisos para modificar ese usuario." };

                    // 2. Admin: verificar WMS compartido vía Users_OMS
                    if (callerRole < RoleLevel.Owner) {
                        using (var cmd = new SqlCommand(@"
                            SELECT COUNT(*)
                            FROM Users_OMS uo_t
                            INNER JOIN OMS o_t ON o_t.OMS_Id = uo_t.OMS_Id
                            INNER JOIN Users_OMS uo_c ON uo_c.User_Id = @CallerId
                            INNER JOIN OMS o_c ON o_c.OMS_Id = uo_c.OMS_Id
                            WHERE uo_t.User_Id = @TargetId
                              AND o_t.WMS_Id   = o_c.WMS_Id", conn)) {
                            cmd.Parameters.AddWithValue("@CallerId", callerId);
                            cmd.Parameters.AddWithValue("@TargetId", targetUserId);
                            int shared = (int)cmd.ExecuteScalar();
                            if (shared == 0)
                                return new { success = false, message = "No compartes ningún WMS con ese usuario." };
                        }
                    }

                    // 3. Aplicar cambio
                    using (var cmd = new SqlCommand(
                        "UPDATE Users SET Role_Id = @RoleId WHERE User_Id = @UserId", conn)) {
                        cmd.Parameters.AddWithValue("@RoleId", newRoleId);
                        cmd.Parameters.AddWithValue("@UserId", targetUserId);
                        cmd.ExecuteNonQuery();
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[AssignRoles.SaveRoleChange] UserId={targetUserId} RoleId={newRoleId} by caller={callerId}");
                return new { success = true };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[AssignRoles.SaveRoleChange] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }
    }
}