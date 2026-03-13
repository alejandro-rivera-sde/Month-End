using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Close_Portal.Pages {
    public partial class AcceptInvitation : Page {

        private static readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        private int _invitationId;
        private string _invitedEmail = "";
        private string _roleName = "";
        private string _wmsCodes = "";
        private string _invitedByName = "";
        private string _tokenValue = "";

        protected void Page_Load(object sender, EventArgs e) {
            _tokenValue = Request.QueryString["token"];

            if (string.IsNullOrWhiteSpace(_tokenValue)) {
                ShowPanel("invalid"); return;
            }

            LoadInvitation(_tokenValue);
        }

        private void LoadInvitation(string token) {
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    // FIX: Ahora usa User_Invitation_OMS → OMS (en lugar de User_Invitation_WMS → WMS)
                    // Muestra los OMS codes asignados en la invitación
                    string sql = @"
                        SELECT
                            i.Invitation_Id, i.Email, i.Is_Active, i.Accepted_At,
                            r.Role_Name,
                            u.Username AS InvitedByName,
                            STUFF((
                                SELECT ', ' + o.OMS_Code
                                FROM User_Invitation_OMS io
                                INNER JOIN OMS o ON o.OMS_Id = io.OMS_Id
                                WHERE io.Invitation_Id = i.Invitation_Id
                                FOR XML PATH('')
                            ), 1, 2, '') AS WmsCodes
                        FROM User_Invitations i
                        INNER JOIN Users_Roles r ON i.Role_Id    = r.Role_Id
                        INNER JOIN Users       u ON i.Invited_By = u.User_Id
                        WHERE i.Token = @Token";

                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = Guid.Parse(token);
                        conn.Open();
                        using (var r = cmd.ExecuteReader()) {
                            if (!r.Read()) { ShowPanel("invalid"); return; }

                            _invitationId = (int)r["Invitation_Id"];
                            _invitedEmail = r["Email"].ToString();
                            _roleName = r["Role_Name"].ToString();
                            _wmsCodes = r["WmsCodes"]?.ToString() ?? "";
                            _invitedByName = r["InvitedByName"].ToString();

                            bool isActive = (bool)r["Is_Active"];
                            bool isAccepted = r["Accepted_At"] != DBNull.Value;

                            if (isAccepted) ShowPanel("already_accepted");
                            else if (!isActive) ShowPanel("cancelled");
                            else ShowPanel("pending");
                        }
                    }
                }
            } catch (FormatException) {
                ShowPanel("invalid");
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[AcceptInvitation.LoadInvitation] ERROR: {ex.Message}");
                ShowPanel("invalid");
            }
        }

        private void ShowPanel(string state) {
            PanelInvalid.Visible = state == "invalid";
            PanelCancelled.Visible = state == "cancelled";
            PanelAccepted.Visible = state == "already_accepted";
            PanelPending.Visible = state == "pending";

            if (state == "pending") {
                LitInvitedBy.Text = HttpUtility.HtmlEncode(_invitedByName);
                LitEmail.Text = HttpUtility.HtmlEncode(_invitedEmail);
                LitEmailNotice.Text = HttpUtility.HtmlEncode(_invitedEmail);
                LitRole.Text = HttpUtility.HtmlEncode(_roleName);
                LitWms.Text = BuildWmsTags(_wmsCodes);
            }
        }

        private static string BuildWmsTags(string wmsCodes) {
            if (string.IsNullOrEmpty(wmsCodes)) return "";
            var sb = new StringBuilder();
            foreach (var code in wmsCodes.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                sb.Append($"<span class='ai-wms-tag'>{HttpUtility.HtmlEncode(code.Trim())}</span>");
            return sb.ToString();
        }

        protected void BtnAccept_Click(object sender, EventArgs e) {
            if (!PanelPending.Visible) return;

            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    int roleId;
                    string invitedEmail;

                    using (var cmd = new SqlCommand(@"
                        SELECT Role_Id, Email FROM User_Invitations
                        WHERE Token = @Token AND Is_Active = 1 AND Accepted_At IS NULL", conn)) {
                        cmd.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = Guid.Parse(_tokenValue);
                        using (var r = cmd.ExecuteReader()) {
                            if (!r.Read()) {
                                ShowError("Esta invitación ya no está disponible. Solicita una nueva.");
                                return;
                            }
                            roleId = (int)r["Role_Id"];
                            invitedEmail = r["Email"].ToString().Trim().ToLower();
                        }
                    }

                    using (var tx = conn.BeginTransaction()) {
                        try {
                            // 1. Crear usuario o actualizar si ya existe
                            int userId = 0;
                            using (var cmd = new SqlCommand(
                                "SELECT User_Id FROM Users WHERE Email = @Email", conn, tx)) {
                                cmd.Parameters.AddWithValue("@Email", invitedEmail);
                                var found = cmd.ExecuteScalar();
                                if (found != null) userId = (int)found;
                            }

                            if (userId > 0) {
                                using (var cmd = new SqlCommand(
                                    "UPDATE Users SET Role_Id = @RoleId, Active = 1 WHERE User_Id = @UserId", conn, tx)) {
                                    cmd.Parameters.AddWithValue("@RoleId", roleId);
                                    cmd.Parameters.AddWithValue("@UserId", userId);
                                    cmd.ExecuteNonQuery();
                                }
                            } else {
                                string username = invitedEmail.Split('@')[0];
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO Users (Email, Username, Role_Id, Login_Type, Active, Locked)
                                    OUTPUT INSERTED.User_Id
                                    VALUES (@Email, @Username, @RoleId, 'Google', 1, 0)", conn, tx)) {
                                    cmd.Parameters.AddWithValue("@Email", invitedEmail);
                                    cmd.Parameters.AddWithValue("@Username", username);
                                    cmd.Parameters.AddWithValue("@RoleId", roleId);
                                    userId = (int)cmd.ExecuteScalar();
                                }
                                System.Diagnostics.Debug.WriteLine($"[AcceptInvitation] Usuario creado. UserId={userId}");
                            }

                            // 2. FIX: Sincronizar Users_OMS desde User_Invitation_OMS
                            //    (antes usaba Users_WMS / User_Invitation_WMS, tablas obsoletas)
                            using (var cmd = new SqlCommand(
                                "DELETE FROM Users_OMS WHERE User_Id = @UserId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.ExecuteNonQuery();
                            }

                            var omsIds = new List<int>();
                            using (var cmd = new SqlCommand(
                                "SELECT OMS_Id FROM User_Invitation_OMS WHERE Invitation_Id = @InvId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@InvId", _invitationId);
                                using (var r = cmd.ExecuteReader())
                                    while (r.Read()) omsIds.Add((int)r["OMS_Id"]);
                            }

                            foreach (int omsId in omsIds) {
                                using (var cmd = new SqlCommand(
                                    "INSERT INTO Users_OMS (User_Id, OMS_Id) VALUES (@UserId, @OmsId)", conn, tx)) {
                                    cmd.Parameters.AddWithValue("@UserId", userId);
                                    cmd.Parameters.AddWithValue("@OmsId", omsId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // 3. Marcar invitación aceptada
                            using (var cmd = new SqlCommand(@"
                                UPDATE User_Invitations
                                SET Accepted_At = GETDATE(), Is_Active = 0
                                WHERE Invitation_Id = @InvId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@InvId", _invitationId);
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            System.Diagnostics.Debug.WriteLine($"[AcceptInvitation] ✓ Completado. UserId={userId} OmsIds={string.Join(",", omsIds)}");

                        } catch {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                Response.Redirect("~/Pages/Home/Login.aspx?invited=1");

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[AcceptInvitation.BtnAccept_Click] ERROR: {ex.Message}");
                ShowError("Ocurrió un error al procesar la invitación. Intenta de nuevo.");
            }
        }

        private void ShowError(string message) {
            LitError.Text = HttpUtility.HtmlEncode(message);
            PanelError.Visible = true;
        }
    }
}