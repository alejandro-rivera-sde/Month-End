using Close_Portal.Core;
using Close_Portal.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Services;

namespace Close_Portal.Pages {
    public partial class InvitationRegistration : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Administrador;

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e) {
            // UI cargada 100% por JS vía WebMethods
        }

        // ============================================================
        // GET AVAILABLE ROLES — excluye Owner y roles >= al creador
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAvailableRoles() {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);
                var session = System.Web.HttpContext.Current.Session;
                int callerRole = session["RoleId"] != null ? (int)session["RoleId"] : -1;

                var roles = new List<object>();
                using (var conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT Role_Id, Role_Name FROM Users_Roles
                        WHERE Role_Id < @CallerRole
                          AND Role_Id < @OwnerRole
                        ORDER BY Role_Id DESC";

                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@CallerRole", callerRole);
                        cmd.Parameters.AddWithValue("@OwnerRole", RoleLevel.Owner);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read())
                                roles.Add(new {
                                    roleId = (int)r["Role_Id"],
                                    roleName = r["Role_Name"].ToString()
                                });
                        }
                    }
                }
                return new { success = true, data = roles };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetAvailableRoles] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET AVAILABLE OMS — Owner: todos | Admin: solo OMS cuyo
        // WMS_Id está en sus Users_OMS
        // Devuelve OmsId, OmsCode, OmsName, WmsId, WmsCode
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

                var list = new List<object>();
                using (var conn = new SqlConnection(_connStr)) {
                    using (var cmd = new SqlCommand(sql, conn)) {
                        if (!isOwner)
                            cmd.Parameters.AddWithValue("@UserId", userId);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read())
                                list.Add(new {
                                    omsId = (int)r["OMS_Id"],
                                    omsCode = r["OMS_Code"].ToString(),
                                    omsName = r["OMS_Name"].ToString(),
                                    wmsId = (int)r["WMS_Id"],
                                    wmsCode = r["WMS_Code"].ToString()
                                });
                        }
                    }
                }
                return new { success = true, data = list };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetAvailableOms] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SEND INVITATION — crea token, inserta OMS en
        // User_Invitation_OMS y envía correo
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SendInvitation(string email, int roleId, int[] omsIds) {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);

                if (string.IsNullOrWhiteSpace(email))
                    return new { success = false, message = "El email es requerido." };
                if (omsIds == null || omsIds.Length == 0)
                    return new { success = false, message = "Debes seleccionar al menos un OMS." };

                var session = System.Web.HttpContext.Current.Session;
                int invitedBy = (int)session["UserId"];
                int callerRole = (int)session["RoleId"];
                string inviterEmail = session["Email"]?.ToString();

                if (roleId >= callerRole)
                    return new { success = false, message = "No puedes invitar a un rol igual o superior al tuyo." };

                email = email.Trim().ToLower();

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // Validar que no haya invitación pendiente para el mismo email
                    string sqlCheck = @"
                        SELECT COUNT(*) FROM User_Invitations
                        WHERE Email = @Email AND Is_Active = 1 AND Accepted_At IS NULL";
                    using (var cmd = new SqlCommand(sqlCheck, conn)) {
                        cmd.Parameters.AddWithValue("@Email", email);
                        int pending = (int)cmd.ExecuteScalar();
                        if (pending > 0)
                            return new { success = false, message = "Ya existe una invitación pendiente para ese email. Cancélala primero." };
                    }

                    // Nombre del rol para el correo
                    string roleName = "";
                    using (var cmd = new SqlCommand("SELECT Role_Name FROM Users_Roles WHERE Role_Id = @RoleId", conn)) {
                        cmd.Parameters.AddWithValue("@RoleId", roleId);
                        roleName = cmd.ExecuteScalar()?.ToString() ?? "";
                    }

                    Guid token;
                    int invitationId;

                    using (var tx = conn.BeginTransaction()) {
                        try {
                            // 1. Crear invitación
                            string sqlInsert = @"
                                INSERT INTO User_Invitations (Token, Email, Role_Id, Invited_By, Created_At, Is_Active)
                                OUTPUT INSERTED.Invitation_Id, INSERTED.Token
                                VALUES (NEWID(), @Email, @RoleId, @InvitedBy, GETDATE(), 1)";

                            using (var cmd = new SqlCommand(sqlInsert, conn, tx)) {
                                cmd.Parameters.AddWithValue("@Email", email);
                                cmd.Parameters.AddWithValue("@RoleId", roleId);
                                cmd.Parameters.AddWithValue("@InvitedBy", invitedBy);
                                using (var r = cmd.ExecuteReader()) {
                                    r.Read();
                                    invitationId = (int)r["Invitation_Id"];
                                    token = (Guid)r["Token"];
                                }
                            }

                            // 2. Insertar OMS de la invitación
                            foreach (int omsId in omsIds) {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO User_Invitation_OMS (Invitation_Id, OMS_Id)
                                    VALUES (@InvitationId, @OmsId)", conn, tx)) {
                                    cmd.Parameters.AddWithValue("@InvitationId", invitationId);
                                    cmd.Parameters.AddWithValue("@OmsId", omsId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();
                            System.Diagnostics.Debug.WriteLine($"[SendInvitation] Token={token} para {email} ✓");

                        } catch {
                            tx.Rollback();
                            throw;
                        }
                    }

                    // Construir link
                    var request = System.Web.HttpContext.Current.Request;
                    string baseUrl = $"{request.Url.Scheme}://{request.Url.Authority}";
                    string appPath = request.ApplicationPath.TrimEnd('/');
                    string acceptUrl = $"{baseUrl}{appPath}/Pages/Home/AcceptInvitation.aspx?token={token}";

                    System.Threading.Tasks.Task.Run(() => {
                        EmailService.NotifyInvitationSent(
                            targetEmail: email,
                            roleName: roleName,
                            inviterEmail: inviterEmail,
                            acceptUrl: acceptUrl
                        );
                    });

                    return new { success = true, message = $"Invitación enviada a {email}." };
                }

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[SendInvitation] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET INVITATIONS — lista de invitaciones del admin actual
        // Muestra OMS codes agrupados desde User_Invitation_OMS
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetInvitations() {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);
                var session = System.Web.HttpContext.Current.Session;
                int callerRole = (int)session["RoleId"];
                int callerId = (int)session["UserId"];

                var list = new List<object>();
                using (var conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        SELECT
                            i.Invitation_Id,
                            i.Token,
                            i.Email,
                            r.Role_Name,
                            u.Username    AS InvitedByName,
                            i.Created_At,
                            i.Accepted_At,
                            i.Is_Active,
                            STUFF((
                                SELECT ', ' + o.OMS_Code
                                FROM User_Invitation_OMS io
                                INNER JOIN OMS o ON io.OMS_Id = o.OMS_Id
                                WHERE io.Invitation_Id = i.Invitation_Id
                                FOR XML PATH('')
                            ), 1, 2, '') AS OmsCodes
                        FROM User_Invitations i
                        INNER JOIN Users_Roles r ON i.Role_Id    = r.Role_Id
                        INNER JOIN Users       u ON i.Invited_By = u.User_Id
                        WHERE (@IsOwner = 1 OR i.Invited_By = @CallerId)
                        ORDER BY i.Created_At DESC";

                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@IsOwner", callerRole >= RoleLevel.Owner ? 1 : 0);
                        cmd.Parameters.AddWithValue("@CallerId", callerId);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                list.Add(new {
                                    invitationId = (int)r["Invitation_Id"],
                                    token = r["Token"].ToString(),
                                    email = r["Email"].ToString(),
                                    roleName = r["Role_Name"].ToString(),
                                    invitedByName = r["InvitedByName"].ToString(),
                                    createdAt = ((DateTime)r["Created_At"]).ToString("dd/MM/yyyy HH:mm"),
                                    acceptedAt = r["Accepted_At"] != DBNull.Value
                                                    ? ((DateTime)r["Accepted_At"]).ToString("dd/MM/yyyy HH:mm")
                                                    : null,
                                    isActive = (bool)r["Is_Active"],
                                    omsCodes = r["OmsCodes"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return new { success = true, data = list };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetInvitations] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // CANCEL INVITATION — marca Is_Active = 0
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object CancelInvitation(int invitationId) {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);
                var session = System.Web.HttpContext.Current.Session;
                int callerRole = (int)session["RoleId"];
                int callerId = (int)session["UserId"];

                using (var conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        UPDATE User_Invitations
                        SET Is_Active = 0
                        WHERE Invitation_Id = @InvitationId
                          AND Is_Active = 1
                          AND Accepted_At IS NULL
                          AND (@IsOwner = 1 OR Invited_By = @CallerId)";

                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@InvitationId", invitationId);
                        cmd.Parameters.AddWithValue("@IsOwner", callerRole >= RoleLevel.Owner ? 1 : 0);
                        cmd.Parameters.AddWithValue("@CallerId", callerId);
                        conn.Open();
                        int rows = cmd.ExecuteNonQuery();
                        if (rows == 0)
                            return new { success = false, message = "Invitación no encontrada o ya procesada." };
                    }
                }
                return new { success = true };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[CancelInvitation] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }
    }
}