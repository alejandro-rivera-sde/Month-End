using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace Close_Portal.DataAccess {

    // ── View models ──────────────────────────────────────────────
    public class EmailGroupViewModel {
        public int GroupId { get; set; }
        public string GroupKey { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Color { get; set; }
        public bool Active { get; set; }
        public List<EmailGroupMemberViewModel> Members { get; set; } = new List<EmailGroupMemberViewModel>();
    }

    public class EmailGroupMemberViewModel {
        public int MemberId { get; set; }
        public int GroupId { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public bool Active { get; set; }
    }

    public class EmailServiceConfigViewModel {
        public bool NotificationsEnabled { get; set; }
        public bool TestMode { get; set; }
        public string TestRecipient { get; set; }
    }

    public class EmailAlertSettingViewModel {
        public string AlertKey { get; set; }
        public string Label { get; set; }
        public bool Enabled { get; set; }
    }

    public class EmailResult {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int Id { get; set; }
    }

    // ── Data Access ──────────────────────────────────────────────
    public class EmailDataAccess {

        private readonly string _connStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        // ============================================================
        // SERVICE CONFIG
        // ============================================================
        public EmailServiceConfigViewModel GetServiceConfig() {
            try {
                string sql = @"
                    SELECT Notifications_Enabled, Test_Mode, Test_Recipient
                    FROM   MonthEnd_Email_Service_Config
                    WHERE  Config_Id = 1";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        if (r.Read()) {
                            return new EmailServiceConfigViewModel {
                                NotificationsEnabled = (bool)r["Notifications_Enabled"],
                                TestMode = (bool)r["Test_Mode"],
                                TestRecipient = r["Test_Recipient"]?.ToString() ?? ""
                            };
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.GetServiceConfig] ERROR: {ex.Message}");
            }
            return new EmailServiceConfigViewModel();
        }

        public EmailResult SaveServiceConfig(bool notificationsEnabled, bool testMode,
                                             string testRecipient, int updatedBy) {
            try {
                string sql = @"
                    UPDATE MonthEnd_Email_Service_Config SET
                        Notifications_Enabled = @Enabled,
                        Test_Mode             = @TestMode,
                        Test_Recipient        = @Recipient,
                        Updated_At            = GETDATE(),
                        Updated_By            = @UpdatedBy
                    WHERE Config_Id = 1";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Enabled", notificationsEnabled);
                    cmd.Parameters.AddWithValue("@TestMode", testMode);
                    cmd.Parameters.AddWithValue("@Recipient", testRecipient ?? "");
                    cmd.Parameters.AddWithValue("@UpdatedBy", updatedBy);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new EmailResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.SaveServiceConfig] ERROR: {ex.Message}");
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // ALERT SETTINGS
        // ============================================================
        public List<EmailAlertSettingViewModel> GetAlertSettings() {
            var list = new List<EmailAlertSettingViewModel>();
            try {
                string sql = "SELECT Alert_Key, Label, Enabled FROM MonthEnd_Email_Alert_Settings ORDER BY Alert_Key";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read())
                            list.Add(new EmailAlertSettingViewModel {
                                AlertKey = r["Alert_Key"].ToString(),
                                Label = r["Label"].ToString(),
                                Enabled = (bool)r["Enabled"]
                            });
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.GetAlertSettings] ERROR: {ex.Message}");
            }
            return list;
        }

        public EmailResult SetAlertEnabled(string alertKey, bool enabled, int updatedBy) {
            try {
                string sql = @"
                    UPDATE MonthEnd_Email_Alert_Settings SET
                        Enabled    = @Enabled,
                        Updated_At = GETDATE(),
                        Updated_By = @UpdatedBy
                    WHERE Alert_Key = @Key";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Key", alertKey);
                    cmd.Parameters.AddWithValue("@Enabled", enabled);
                    cmd.Parameters.AddWithValue("@UpdatedBy", updatedBy);
                    conn.Open();
                    int rows = cmd.ExecuteNonQuery();
                    if (rows == 0)
                        return new EmailResult { Success = false, Message = $"Alerta '{alertKey}' no encontrada." };
                }
                return new EmailResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.SetAlertEnabled] ERROR: {ex.Message}");
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        public EmailResult SetBulkAlerts(bool enabled, int updatedBy) {
            try {
                string sql = @"
                    UPDATE MonthEnd_Email_Alert_Settings SET
                        Enabled    = @Enabled,
                        Updated_At = GETDATE(),
                        Updated_By = @UpdatedBy";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Enabled", enabled);
                    cmd.Parameters.AddWithValue("@UpdatedBy", updatedBy);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new EmailResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.SetBulkAlerts] ERROR: {ex.Message}");
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // GROUPS
        // ============================================================
        public List<EmailGroupViewModel> GetGroups(bool activeOnly = true) {
            var groups = new List<EmailGroupViewModel>();
            try {
                string sql = @"
                    SELECT Group_Id, Group_Key, Label, Description, Icon, Color, Active
                    FROM   MonthEnd_Email_Groups
                    WHERE  (@ActiveOnly = 0 OR Active = 1)
                    ORDER BY Group_Id";
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@ActiveOnly", activeOnly ? 1 : 0);
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read())
                                groups.Add(new EmailGroupViewModel {
                                    GroupId = (int)r["Group_Id"],
                                    GroupKey = r["Group_Key"].ToString(),
                                    Label = r["Label"].ToString(),
                                    Description = r["Description"].ToString(),
                                    Icon = r["Icon"].ToString(),
                                    Color = r["Color"].ToString(),
                                    Active = (bool)r["Active"]
                                });
                        }
                    }

                    // Load members
                    foreach (var g in groups) {
                        g.Members = GetGroupMembers(conn, g.GroupId);
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.GetGroups] ERROR: {ex.Message}");
            }
            return groups;
        }

        private List<EmailGroupMemberViewModel> GetGroupMembers(SqlConnection conn, int groupId) {
            var list = new List<EmailGroupMemberViewModel>();
            string sql = @"
                SELECT Member_Id, Group_Id, Email, Display_Name, Active
                FROM   MonthEnd_Email_Group_Members
                WHERE  Group_Id = @GroupId AND Active = 1
                ORDER BY Display_Name, Email";
            using (var cmd = new SqlCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@GroupId", groupId);
                using (var r = cmd.ExecuteReader()) {
                    while (r.Read())
                        list.Add(new EmailGroupMemberViewModel {
                            MemberId = (int)r["Member_Id"],
                            GroupId = (int)r["Group_Id"],
                            Email = r["Email"].ToString(),
                            DisplayName = r["Display_Name"].ToString(),
                            Active = (bool)r["Active"]
                        });
                }
            }
            return list;
        }

        public EmailResult CreateGroup(string groupKey, string label, string description,
                                       string icon, string color) {
            try {
                if (string.IsNullOrWhiteSpace(groupKey) || string.IsNullOrWhiteSpace(label))
                    return new EmailResult { Success = false, Message = "Clave y nombre son requeridos." };

                string sql = @"
                    IF EXISTS (SELECT 1 FROM MonthEnd_Email_Groups WHERE Group_Key = @Key)
                        SELECT -1 AS Id
                    ELSE BEGIN
                        INSERT INTO MonthEnd_Email_Groups (Group_Key, Label, Description, Icon, Color)
                        OUTPUT INSERTED.Group_Id AS Id
                        VALUES (@Key, @Label, @Desc, @Icon, @Color)
                    END";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Key", groupKey.Trim());
                    cmd.Parameters.AddWithValue("@Label", label.Trim());
                    cmd.Parameters.AddWithValue("@Desc", description?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@Icon", icon?.Trim() ?? "group");
                    cmd.Parameters.AddWithValue("@Color", color?.Trim() ?? "blue");
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    int id = val != null ? Convert.ToInt32(val) : -1;
                    if (id == -1)
                        return new EmailResult { Success = false, Message = "Ya existe un grupo con esa clave." };
                    return new EmailResult { Success = true, Id = id };
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.CreateGroup] ERROR: {ex.Message}");
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        public EmailResult UpdateGroup(int groupId, string label, string description,
                                       string icon, string color) {
            try {
                string sql = @"
                    UPDATE MonthEnd_Email_Groups SET
                        Label       = @Label,
                        Description = @Desc,
                        Icon        = @Icon,
                        Color       = @Color
                    WHERE Group_Id = @GroupId";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@GroupId", groupId);
                    cmd.Parameters.AddWithValue("@Label", label?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@Desc", description?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@Icon", icon?.Trim() ?? "group");
                    cmd.Parameters.AddWithValue("@Color", color?.Trim() ?? "blue");
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new EmailResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.UpdateGroup] ERROR: {ex.Message}");
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        public EmailResult DeleteGroup(int groupId) {
            try {
                // Soft delete — ON DELETE CASCADE limpia los miembros si haces hard delete
                // Aquí hacemos soft para preservar historial
                string sql = "UPDATE MonthEnd_Email_Groups SET Active = 0 WHERE Group_Id = @GroupId";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@GroupId", groupId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new EmailResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.DeleteGroup] ERROR: {ex.Message}");
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // GROUP MEMBERS
        // ============================================================
        public EmailResult AddMember(int groupId, string email, string displayName) {
            try {
                if (string.IsNullOrWhiteSpace(email))
                    return new EmailResult { Success = false, Message = "El correo es requerido." };

                email = email.Trim().ToLower();

                string sql = @"
                    IF EXISTS (
                        SELECT 1 FROM MonthEnd_Email_Group_Members
                        WHERE Group_Id = @GroupId AND Email = @Email AND Active = 1
                    )
                        SELECT -1 AS Id
                    ELSE BEGIN
                        -- Reactivar si existe inactivo
                        IF EXISTS (
                            SELECT 1 FROM MonthEnd_Email_Group_Members
                            WHERE Group_Id = @GroupId AND Email = @Email AND Active = 0
                        ) BEGIN
                            UPDATE MonthEnd_Email_Group_Members
                            SET Active = 1, Display_Name = @Name
                            WHERE Group_Id = @GroupId AND Email = @Email;
                            SELECT Member_Id AS Id FROM MonthEnd_Email_Group_Members
                            WHERE Group_Id = @GroupId AND Email = @Email;
                        END ELSE BEGIN
                            INSERT INTO MonthEnd_Email_Group_Members (Group_Id, Email, Display_Name)
                            OUTPUT INSERTED.Member_Id AS Id
                            VALUES (@GroupId, @Email, @Name)
                        END
                    END";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@GroupId", groupId);
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Name", displayName?.Trim() ?? "");
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    int id = val != null ? Convert.ToInt32(val) : -1;
                    if (id == -1)
                        return new EmailResult { Success = false, Message = "Este correo ya pertenece al grupo." };
                    return new EmailResult { Success = true, Id = id };
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.AddMember] ERROR: {ex.Message}");
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        public EmailResult RemoveMember(int memberId) {
            try {
                string sql = "UPDATE MonthEnd_Email_Group_Members SET Active = 0 WHERE Member_Id = @Id";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Id", memberId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new EmailResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.RemoveMember] ERROR: {ex.Message}");
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // RESOLVE GROUP EMAILS — string "a@x.com;b@x.com" para Send()
        // ============================================================
        public string ResolveGroupEmails(string groupKey) {
            try {
                string sql = @"
                    SELECT m.Email
                    FROM   MonthEnd_Email_Group_Members m
                    INNER JOIN MonthEnd_Email_Groups g ON g.Group_Id = m.Group_Id
                    WHERE  g.Group_Key = @Key AND g.Active = 1 AND m.Active = 1";
                var emails = new List<string>();
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Key", groupKey);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            emails.Add(r["Email"].ToString().Trim());
                }
                return emails.Count > 0 ? string.Join(";", emails) : null;
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailDataAccess.ResolveGroupEmails] ERROR: {ex.Message}");
                return null;
            }
        }
    }
}