using Close_Portal.Core;
using Close_Portal.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Services;

namespace Close_Portal.Pages {
    public partial class UserRegistration : SecurePage {
        protected override int RequiredRoleId => RoleLevel.Administrador;

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        // ============================================================
        // PAGE LOAD
        // Roles y OMS se cargan en el cliente vía GetAvailableRoles
        // y GetAvailableOms (WebMethods abajo).
        // ============================================================
        protected void Page_Load(object sender, EventArgs e) { }

        // ============================================================
        // WEBMETHOD — Obtener roles disponibles
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAvailableRoles() {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);

                var roles = new List<object>();
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT Role_Id, Role_Name
                        FROM Users_Roles
                        WHERE Role_Id < @OwnerRoleId
                        ORDER BY Role_Id DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@OwnerRoleId", RoleLevel.Owner);
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                roles.Add(new {
                                    RoleId = (int)r["Role_Id"],
                                    RoleName = r["Role_Name"].ToString()
                                });
                            }
                        }
                    }
                }
                return new { Success = true, Roles = roles };
            } catch (UnauthorizedAccessException ex) {
                return new { Success = false, Message = ex.Message };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetAvailableRoles: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — Obtener OMS disponibles
        // Owner         → todos los OMS activos
        // Administrador → solo OMS cuyo WMS_Id está en sus Users_OMS
        // Devuelve OmsId, OmsCode, OmsName, WmsId, WmsCode
        // agrupados por WMS para el checklist JS
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAvailableOms() {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);

                var session = System.Web.HttpContext.Current.Session;
                int roleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
                int userId = session["UserId"] != null ? (int)session["UserId"] : -1;
                bool isOwner = roleId >= RoleLevel.Owner;

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
                          WHERE uo2.User_Id = @UserId
                      )") + @"
                    ORDER BY w.WMS_Code, o.OMS_Code";

                var omsList = new List<object>();
                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                        if (!isOwner)
                            cmd.Parameters.AddWithValue("@UserId", userId);
                        conn.Open();
                        using (SqlDataReader r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                omsList.Add(new {
                                    OmsId = (int)r["OMS_Id"],
                                    OmsCode = r["OMS_Code"].ToString(),
                                    OmsName = r["OMS_Name"].ToString(),
                                    WmsId = (int)r["WMS_Id"],
                                    WmsCode = r["WMS_Code"].ToString()
                                });
                            }
                        }
                    }
                }

                return new { Success = true, OmsList = omsList };
            } catch (UnauthorizedAccessException ex) {
                return new { Success = false, Message = ex.Message };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR GetAvailableOms: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // WEBMETHOD — Crear usuario
        // Recibe omsIds[] → inserta en Users_OMS (scope de visibilidad)
        // Las locaciones operativas se asignan después en UserManagement
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object CreateUser(string email, string username, int roleId, int[] omsIds) {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);

                var session = System.Web.HttpContext.Current.Session;
                int currentRole = session["RoleId"] != null ? (int)session["RoleId"] : -1;
                int currentUser = session["UserId"] != null ? (int)session["UserId"] : -1;
                string createdBy = session["Email"]?.ToString() ?? "Sistema";

                // ── Validación 1: no puede asignar rol Owner ──────────────────
                if (roleId >= RoleLevel.Owner)
                    return new { Success = false, Message = "No se puede crear un usuario con rol Owner" };

                // ── Validación 2: Admin solo puede asignar sus OMS ────────────
                if (currentRole < RoleLevel.Owner && omsIds != null && omsIds.Length > 0) {
                    if (!ValidateOmsOwnership(currentUser, omsIds))
                        return new { Success = false, Message = "Solo puedes asignar OMS a los que tienes acceso" };
                }

                // ── Validación 3: email no duplicado ──────────────────────────
                if (EmailExists(email))
                    return new { Success = false, Message = "Ya existe un usuario con ese email" };

                string roleName = GetRoleName(roleId);
                int newUserId = -1;

                using (SqlConnection conn = new SqlConnection(_connStr)) {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction()) {
                        try {
                            // 1. INSERT Users
                            string sqlInsert = @"
                                INSERT INTO Users (Email, Username, Login_Type, Role_Id, Active, Locked)
                                VALUES (@Email, @Username, 'Google', @RoleId, 1, 0);
                                SELECT SCOPE_IDENTITY();";

                            using (SqlCommand cmd = new SqlCommand(sqlInsert, conn, tx)) {
                                cmd.Parameters.AddWithValue("@Email", email.Trim().ToLower());
                                cmd.Parameters.AddWithValue("@Username", string.IsNullOrWhiteSpace(username)
                                                                         ? email.Split('@')[0] : username.Trim());
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
                            System.Diagnostics.Debug.WriteLine($"[CreateUser] Id={newUserId} Email={email}");

                        } catch (Exception ex) {
                            tx.Rollback();
                            System.Diagnostics.Debug.WriteLine($"[CreateUser] Rollback: {ex.Message}");
                            return new { Success = false, Message = "Error al crear el usuario" };
                        }
                    }
                }

                EmailService.NotifyUserAdded(
                    targetEmail: email,
                    targetUsername: username,
                    targetRole: roleName,
                    performedByEmail: createdBy
                );

                return new { Success = true, Message = "Usuario creado correctamente", UserId = newUserId };

            } catch (UnauthorizedAccessException ex) {
                return new { Success = false, Message = ex.Message };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"ERROR CreateUser: {ex.Message}");
                return new { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // HELPERS PRIVADOS
        // ============================================================

        /// <summary>
        /// Verifica que todos los omsIds enviados pertenecen al mismo WMS
        /// que los OMS del creador (via Users_OMS).
        /// Evita que un Admin asigne OMS fuera de su scope.
        /// </summary>
        private static bool ValidateOmsOwnership(int creatorUserId, int[] omsIds) {
            if (omsIds == null || omsIds.Length == 0) return true;

            string ids = string.Join(",", omsIds);

            // Cuenta cuántos de los omsIds enviados pertenecen a un WMS
            // que el creador tiene en su Users_OMS
            string sql = $@"
                SELECT COUNT(DISTINCT o.OMS_Id)
                FROM OMS o
                INNER JOIN WMS w ON w.WMS_Id = o.WMS_Id
                WHERE o.OMS_Id IN ({ids})
                  AND w.WMS_Id IN (
                      SELECT DISTINCT o2.WMS_Id
                      FROM Users_OMS uo2
                      INNER JOIN OMS o2 ON o2.OMS_Id = uo2.OMS_Id
                      WHERE uo2.User_Id = @CreatorId
                  )";

            using (SqlConnection conn = new SqlConnection(_connStr)) {
                using (SqlCommand cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@CreatorId", creatorUserId);
                    conn.Open();
                    int accessible = (int)cmd.ExecuteScalar();
                    return accessible == omsIds.Length;
                }
            }
        }

        private static bool EmailExists(string email) {
            using (SqlConnection conn = new SqlConnection(_connStr)) {
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Users WHERE Email = @Email", conn)) {
                    cmd.Parameters.AddWithValue("@Email", email.Trim().ToLower());
                    conn.Open();
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

        private static string GetRoleName(int roleId) {
            using (SqlConnection conn = new SqlConnection(_connStr)) {
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT Role_Name FROM Users_Roles WHERE Role_Id = @RoleId", conn)) {
                    cmd.Parameters.AddWithValue("@RoleId", roleId);
                    conn.Open();
                    return cmd.ExecuteScalar()?.ToString() ?? "";
                }
            }
        }
    }
}