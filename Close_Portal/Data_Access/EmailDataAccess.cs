using Close_Portal.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

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
        // 'static' | 'role' | 'spot'
        public string GroupType { get; set; } = "static";
        public int? RoleId { get; set; }    // válido cuando GroupType='role'
        public string DeptCode { get; set; } // válido cuando GroupType='spot'
        public bool IsDynamic => GroupType == "role" || GroupType == "spot";
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
        public int? ThresholdMinutes { get; set; }
        public string AlertGroupKey { get; set; }
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
                AppLogger.Error("EmailDataAccess.GetServiceConfig", ex);
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
                AppLogger.Error("EmailDataAccess.SaveServiceConfig", ex);
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // ALERT SETTINGS
        // ============================================================
        public List<EmailAlertSettingViewModel> GetAlertSettings() {
            var list = new List<EmailAlertSettingViewModel>();
            try {
                // Verificar qué columnas existen (resiliente a migraciones parciales)
                bool hasThreshold = false, hasGroupKey = false;
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT
                        CASE WHEN EXISTS(SELECT 1 FROM sys.columns
                             WHERE object_id = OBJECT_ID('MonthEnd_Email_Alert_Settings')
                               AND name = 'Threshold_Minutes') THEN 1 ELSE 0 END,
                        CASE WHEN EXISTS(SELECT 1 FROM sys.columns
                             WHERE object_id = OBJECT_ID('MonthEnd_Email_Alert_Settings')
                               AND name = 'Alert_Group_Key')   THEN 1 ELSE 0 END", conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        if (r.Read()) {
                            hasThreshold = (int)r[0] == 1;
                            hasGroupKey = (int)r[1] == 1;
                        }
                    }
                }

                string thresholdSel = hasThreshold ? ", Threshold_Minutes" : ", NULL AS Threshold_Minutes";
                string groupKeySel = hasGroupKey ? ", Alert_Group_Key" : ", NULL AS Alert_Group_Key";
                string sql = $"SELECT Alert_Key, Label, Enabled{thresholdSel}{groupKeySel} FROM MonthEnd_Email_Alert_Settings ORDER BY Alert_Key";

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read())
                            list.Add(new EmailAlertSettingViewModel {
                                AlertKey = r["Alert_Key"].ToString(),
                                Label = r["Label"].ToString(),
                                Enabled = (bool)r["Enabled"],
                                ThresholdMinutes = r["Threshold_Minutes"] as int?,
                                AlertGroupKey = r["Alert_Group_Key"] as string
                            });
                    }
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetAlertSettings", ex);
            }
            return list;
        }

        public EmailResult SetAlertEnabled(string alertKey, bool enabled, int updatedBy) {
            try {
                string sql = @"
                    IF EXISTS (SELECT 1 FROM MonthEnd_Email_Alert_Settings WHERE Alert_Key = @Key)
                        UPDATE MonthEnd_Email_Alert_Settings SET
                            Enabled    = @Enabled,
                            Updated_At = GETDATE(),
                            Updated_By = @UpdatedBy
                        WHERE Alert_Key = @Key
                    ELSE
                        INSERT INTO MonthEnd_Email_Alert_Settings
                            (Alert_Key, Label, Enabled, Updated_At, Updated_By)
                        VALUES (@Key, @Key, @Enabled, GETDATE(), @UpdatedBy)";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Key", alertKey);
                    cmd.Parameters.AddWithValue("@Enabled", enabled);
                    cmd.Parameters.AddWithValue("@UpdatedBy", updatedBy);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new EmailResult { Success = true };
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.SetAlertEnabled", ex);
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
                AppLogger.Error("EmailDataAccess.SetBulkAlerts", ex);
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // SET ALERT THRESHOLD — guarda el umbral en minutos
        // ============================================================
        public EmailResult SetAlertThreshold(string alertKey, int? thresholdMinutes, int updatedBy) {
            try {
                string sql = @"
                    IF EXISTS (SELECT 1 FROM MonthEnd_Email_Alert_Settings WHERE Alert_Key = @Key)
                        UPDATE MonthEnd_Email_Alert_Settings SET
                            Threshold_Minutes = @Threshold,
                            Updated_At        = GETDATE(),
                            Updated_By        = @UpdatedBy
                        WHERE Alert_Key = @Key
                    ELSE
                        INSERT INTO MonthEnd_Email_Alert_Settings
                            (Alert_Key, Label, Enabled, Threshold_Minutes, Updated_At, Updated_By)
                        VALUES (@Key, @Key, 1, @Threshold, GETDATE(), @UpdatedBy)";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Key", alertKey);
                    cmd.Parameters.AddWithValue("@Threshold",
                        thresholdMinutes.HasValue ? (object)thresholdMinutes.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@UpdatedBy", updatedBy);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new EmailResult { Success = true };
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.SetAlertThreshold", ex);
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // SET ALERT GROUP KEY — configura qué grupo recibe esta alerta
        // ============================================================
        public EmailResult SetAlertGroupKey(string alertKey, string groupKey, int updatedBy) {
            try {
                var gkParam = string.IsNullOrWhiteSpace(groupKey) ? (object)DBNull.Value : groupKey.Trim();
                string sql = @"
                    IF EXISTS (SELECT 1 FROM MonthEnd_Email_Alert_Settings WHERE Alert_Key = @Key)
                        UPDATE MonthEnd_Email_Alert_Settings SET
                            Alert_Group_Key = @GroupKey,
                            Updated_At      = GETDATE(),
                            Updated_By      = @UpdatedBy
                        WHERE Alert_Key = @Key
                    ELSE
                        INSERT INTO MonthEnd_Email_Alert_Settings
                            (Alert_Key, Label, Enabled, Alert_Group_Key, Updated_At, Updated_By)
                        VALUES (@Key, @Key, 1, @GroupKey, GETDATE(), @UpdatedBy)";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Key", alertKey);
                    cmd.Parameters.AddWithValue("@GroupKey", gkParam);
                    cmd.Parameters.AddWithValue("@UpdatedBy", updatedBy);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return new EmailResult { Success = true };
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.SetAlertGroupKey", ex);
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
                    SELECT Group_Id, Group_Key, Label, Description, Icon, Color, Active,
                           ISNULL(Group_Type, 'static') AS Group_Type,
                           Role_Id, Dept_Code
                    FROM   MonthEnd_Email_Groups
                    WHERE  (@ActiveOnly = 0 OR Active = 1)
                    ORDER BY Group_Id";
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@ActiveOnly", activeOnly ? 1 : 0);
                        using (var r = cmd.ExecuteReader()) {
                            while (r.Read()) {
                                var g = new EmailGroupViewModel {
                                    GroupId = (int)r["Group_Id"],
                                    GroupKey = r["Group_Key"].ToString(),
                                    Label = r["Label"].ToString(),
                                    Description = r["Description"].ToString(),
                                    Icon = r["Icon"].ToString(),
                                    Color = r["Color"].ToString(),
                                    Active = (bool)r["Active"],
                                    GroupType = r["Group_Type"].ToString(),
                                    RoleId = r["Role_Id"] as int?,
                                    DeptCode = r["Dept_Code"] as string
                                };
                                groups.Add(g);
                            }
                        }
                    }

                    // Solo cargar miembros para grupos estáticos
                    foreach (var g in groups)
                        if (!g.IsDynamic)
                            g.Members = GetGroupMembers(conn, g.GroupId);
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetGroups", ex);
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
                AppLogger.Error("EmailDataAccess.CreateGroup", ex);
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
                AppLogger.Error("EmailDataAccess.UpdateGroup", ex);
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
                AppLogger.Error("EmailDataAccess.DeleteGroup", ex);
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
                AppLogger.Error("EmailDataAccess.AddMember", ex);
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
                AppLogger.Error("EmailDataAccess.RemoveMember", ex);
                return new EmailResult { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // RESOLVE GROUP EMAILS
        // Ramifica según Group_Type:
        //   static → miembros en MonthEnd_Email_Group_Members
        //   role   → usuarios activos con Role_Id del grupo
        //   spot   → usuario asignado al spot del depto en guardia activa
        // ============================================================
        public string ResolveGroupEmails(string groupKey) {
            try {
                // 1. Obtener metadata del grupo
                string groupType = "static";
                int? roleId = null;
                string deptCode = null;

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(Group_Type,'static') AS Group_Type, Role_Id, Dept_Code
                    FROM MonthEnd_Email_Groups
                    WHERE Group_Key = @Key AND Active = 1", conn)) {
                    cmd.Parameters.AddWithValue("@Key", groupKey);
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        if (!r.Read()) return null;
                        groupType = r["Group_Type"].ToString();
                        roleId = r["Role_Id"] as int?;
                        deptCode = r["Dept_Code"] as string;
                    }
                }

                // 2. Resolver según tipo
                if (groupType == "role" && roleId.HasValue)
                    return ResolveByRole(roleId.Value);

                if (groupType == "spot" && !string.IsNullOrWhiteSpace(deptCode))
                    return ResolveBySpot(deptCode);

                // static (default)
                return ResolveStaticMembers(groupKey);

            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.ResolveGroupEmails", ex);
                return null;
            }
        }

        private string ResolveByRole(int roleId) {
            try {
                var emails = new List<string>();
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT Email FROM MonthEnd_Users
                    WHERE Role_Id = @RoleId AND Active = 1 AND Locked = 0
                      AND Email IS NOT NULL AND Email <> ''", conn)) {
                    cmd.Parameters.AddWithValue("@RoleId", roleId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) emails.Add(r["Email"].ToString().Trim());
                }
                return emails.Count > 0 ? string.Join(";", emails) : null;
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.ResolveByRole", ex);
                return null;
            }
        }

        private string ResolveBySpot(string deptCode) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT u.Email
                    FROM MonthEnd_Guard_Spots gs
                    INNER JOIN MonthEnd_Departments d ON d.Department_Id = gs.Department_Id
                    INNER JOIN MonthEnd_Users u        ON u.User_Id       = gs.User_Id
                    INNER JOIN MonthEnd_Guard_Schedule gs2 ON gs2.Guard_Id = gs.Guard_Id
                    WHERE d.Department_Code = @DeptCode
                      AND gs2.End_Time IS NULL
                      AND gs.User_Id IS NOT NULL
                      AND u.Active = 1 AND u.Locked = 0", conn)) {
                    cmd.Parameters.AddWithValue("@DeptCode", deptCode);
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    return val != null && val != DBNull.Value
                        ? val.ToString().Trim() : null;
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.ResolveBySpot", ex);
                return null;
            }
        }

        private string ResolveStaticMembers(string groupKey) {
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
                        while (r.Read()) emails.Add(r["Email"].ToString().Trim());
                }
                return emails.Count > 0 ? string.Join(";", emails) : null;
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.ResolveStaticMembers", ex);
                return null;
            }
        }

        // ============================================================
        // RESOLVE GROUP EMAILS — WITH LANG
        // Igual que ResolveGroupEmails pero retorna (Email, Lang) por destinatario.
        // Los miembros estáticos sin cuenta en MonthEnd_Users obtienen 'es' por defecto.
        // ============================================================
        public List<(string Email, string Lang)> ResolveGroupEmailsWithLang(string groupKey) {
            try {
                string groupType = "static";
                int? roleId = null;
                string deptCode = null;

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(Group_Type,'static') AS Group_Type, Role_Id, Dept_Code
                    FROM MonthEnd_Email_Groups
                    WHERE Group_Key = @Key AND Active = 1", conn)) {
                    cmd.Parameters.AddWithValue("@Key", groupKey);
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        if (!r.Read()) return null;
                        groupType = r["Group_Type"].ToString();
                        roleId    = r["Role_Id"] as int?;
                        deptCode  = r["Dept_Code"] as string;
                    }
                }

                if (groupType == "role" && roleId.HasValue)  return ResolveByRoleWithLang(roleId.Value);
                if (groupType == "spot" && !string.IsNullOrWhiteSpace(deptCode)) return ResolveBySpotWithLang(deptCode);
                return ResolveStaticMembersWithLang(groupKey);

            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.ResolveGroupEmailsWithLang", ex);
                return null;
            }
        }

        private List<(string Email, string Lang)> ResolveByRoleWithLang(int roleId) {
            var list = new List<(string, string)>();
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT Email, ISNULL(Preferred_Lang,'es') AS Preferred_Lang
                    FROM MonthEnd_Users
                    WHERE Role_Id = @RoleId AND Active = 1 AND Locked = 0
                      AND Email IS NOT NULL AND Email <> ''", conn)) {
                    cmd.Parameters.AddWithValue("@RoleId", roleId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add((r["Email"].ToString().Trim(), r["Preferred_Lang"].ToString()));
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.ResolveByRoleWithLang", ex);
            }
            return list;
        }

        private List<(string Email, string Lang)> ResolveBySpotWithLang(string deptCode) {
            var list = new List<(string, string)>();
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT u.Email, ISNULL(u.Preferred_Lang,'es') AS Preferred_Lang
                    FROM MonthEnd_Guard_Spots gs
                    INNER JOIN MonthEnd_Departments d  ON d.Department_Id = gs.Department_Id
                    INNER JOIN MonthEnd_Users u         ON u.User_Id       = gs.User_Id
                    INNER JOIN MonthEnd_Guard_Schedule gs2 ON gs2.Guard_Id = gs.Guard_Id
                    WHERE d.Department_Code = @DeptCode
                      AND gs2.End_Time IS NULL
                      AND gs.User_Id IS NOT NULL
                      AND u.Active = 1 AND u.Locked = 0", conn)) {
                    cmd.Parameters.AddWithValue("@DeptCode", deptCode);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add((r["Email"].ToString().Trim(), r["Preferred_Lang"].ToString()));
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.ResolveBySpotWithLang", ex);
            }
            return list;
        }

        private List<(string Email, string Lang)> ResolveStaticMembersWithLang(string groupKey) {
            var list = new List<(string, string)>();
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT m.Email, ISNULL(u.Preferred_Lang,'es') AS Preferred_Lang
                    FROM   MonthEnd_Email_Group_Members m
                    INNER JOIN MonthEnd_Email_Groups g ON g.Group_Id = m.Group_Id
                    LEFT  JOIN MonthEnd_Users u
                           ON  LOWER(u.Email) = LOWER(m.Email) AND u.Active = 1
                    WHERE  g.Group_Key = @Key AND g.Active = 1 AND m.Active = 1", conn)) {
                    cmd.Parameters.AddWithValue("@Key", groupKey);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add((r["Email"].ToString().Trim(), r["Preferred_Lang"].ToString()));
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.ResolveStaticMembersWithLang", ex);
            }
            return list;
        }

        // ============================================================
        // GET USER LANG BY EMAIL — para correos de destinatario único
        // ============================================================
        public string GetUserLangByEmail(string email) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(Preferred_Lang,'es')
                    FROM MonthEnd_Users
                    WHERE Email = @Email AND Active = 1", conn)) {
                    cmd.Parameters.AddWithValue("@Email", email?.Trim() ?? "");
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    return val?.ToString() ?? "es";
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetUserLangByEmail", ex);
                return "es";
            }
        }

        // ============================================================
        // GET LANGS BY EMAILS — para listas de correos sin lang previa
        // ============================================================
        public Dictionary<string, string> GetLangsByEmails(IEnumerable<string> emails) {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var list   = emails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            if (list == null || list.Count == 0) return result;

            try {
                var paramNames = new List<string>();
                using (var conn = new SqlConnection(_connStr))
                using (var cmd  = new SqlCommand()) {
                    cmd.Connection = conn;
                    for (int i = 0; i < list.Count; i++) {
                        string p = $"@e{i}";
                        paramNames.Add(p);
                        cmd.Parameters.AddWithValue(p, list[i].Trim());
                    }
                    cmd.CommandText = $@"
                        SELECT Email, ISNULL(Preferred_Lang,'es') AS Preferred_Lang
                        FROM MonthEnd_Users
                        WHERE Email IN ({string.Join(",", paramNames)})";
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            result[r["Email"].ToString()] = r["Preferred_Lang"].ToString();
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetLangsByEmails", ex);
            }

            foreach (var e in list)
                if (!result.ContainsKey(e)) result[e] = "es";

            return result;
        }

        // ============================================================
        // GET GUARD AUDIENCE EMAILS
        // Usuarios con locaciones en la guardia (cualquier rol) +
        // usuarios asignados a spots (para IT sin locaciones)
        // ============================================================
        public string GetGuardAudienceEmails(int guardId) {
            try {
                string sql = @"
                    SELECT DISTINCT u.Email
                    FROM MonthEnd_Users_Location ul
                    INNER JOIN MonthEnd_Users u ON u.User_Id = ul.User_Id
                    INNER JOIN MonthEnd_Guard_Locations gl
                           ON gl.Location_Id = ul.Location_Id AND gl.Guard_Id = @GuardId
                    WHERE u.Active = 1 AND u.Locked = 0
                      AND u.Email IS NOT NULL AND u.Email <> ''

                    UNION

                    SELECT DISTINCT u.Email
                    FROM MonthEnd_Guard_Spots gs
                    INNER JOIN MonthEnd_Users u ON u.User_Id = gs.User_Id
                    WHERE gs.Guard_Id = @GuardId
                      AND gs.User_Id IS NOT NULL
                      AND u.Active = 1 AND u.Locked = 0
                      AND u.Email IS NOT NULL AND u.Email <> ''";

                var emails = new List<string>();
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) emails.Add(r["Email"].ToString().Trim());
                }
                return emails.Count > 0 ? string.Join(";", emails) : null;
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetGuardAudienceEmails", ex);
                return null;
            }
        }

        public List<(string Email, string Lang)> GetGuardAudienceEmailsWithLang(int guardId) {
            var list = new List<(string, string)>();
            try {
                string sql = @"
                    SELECT DISTINCT u.Email, ISNULL(u.Preferred_Lang,'es') AS Preferred_Lang
                    FROM MonthEnd_Users_Location ul
                    INNER JOIN MonthEnd_Users u ON u.User_Id = ul.User_Id
                    INNER JOIN MonthEnd_Guard_Locations gl
                           ON gl.Location_Id = ul.Location_Id AND gl.Guard_Id = @GuardId
                    WHERE u.Active = 1 AND u.Locked = 0
                      AND u.Email IS NOT NULL AND u.Email <> ''

                    UNION

                    SELECT DISTINCT u.Email, ISNULL(u.Preferred_Lang,'es') AS Preferred_Lang
                    FROM MonthEnd_Guard_Spots gs
                    INNER JOIN MonthEnd_Users u ON u.User_Id = gs.User_Id
                    WHERE gs.Guard_Id = @GuardId
                      AND gs.User_Id IS NOT NULL
                      AND u.Active = 1 AND u.Locked = 0
                      AND u.Email IS NOT NULL AND u.Email <> ''";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd  = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add((r["Email"].ToString().Trim(), r["Preferred_Lang"].ToString()));
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetGuardAudienceEmailsWithLang", ex);
            }
            return list;
        }

        // ============================================================
        // GET UNFILLED SPOT DEPT CODES — spots vacíos al confirmar
        // Retorna los códigos de departamento cuyos spots no tienen usuario
        // ============================================================
        public List<string> GetUnfilledSpotDeptCodes(int guardId) {
            var list = new List<string>();
            try {
                string sql = @"
                    SELECT d.Department_Code
                    FROM MonthEnd_Guard_Spots gs
                    INNER JOIN MonthEnd_Departments d ON d.Department_Id = gs.Department_Id
                    WHERE gs.Guard_Id = @GuardId AND gs.User_Id IS NULL
                    ORDER BY d.Department_Code";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(r["Department_Code"].ToString());
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetUnfilledSpotDeptCodes", ex);
            }
            return list;
        }

        // ============================================================
        // GET DRAFT GUARDS FOR REMINDER
        // Guardias draft sin confirmar y sin reminder enviado,
        // cuya Created_At supera el threshold en minutos
        // ============================================================
        public List<(int GuardId, DateTime CreatedAt)> GetDraftGuardsForReminder(int thresholdMinutes) {
            var list = new List<(int, DateTime)>();
            try {
                string sql = @"
                    SELECT Guard_Id, Created_At
                    FROM MonthEnd_Guard_Schedule
                    WHERE Is_Confirmed    = 0
                      AND End_Time        IS NULL
                      AND Reminder_Sent_At IS NULL
                      AND DATEDIFF(MINUTE, Created_At, GETDATE()) >= @Threshold";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@Threshold", thresholdMinutes);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(((int)r["Guard_Id"], (DateTime)r["Created_At"]));
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetDraftGuardsForReminder", ex);
            }
            return list;
        }

        // ============================================================
        // MARK REMINDER SENT
        // ============================================================
        public void MarkReminderSent(int guardId) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    UPDATE MonthEnd_Guard_Schedule
                    SET Reminder_Sent_At = GETDATE()
                    WHERE Guard_Id = @GuardId", conn)) {
                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.MarkReminderSent", ex);
            }
        }

        // ============================================================
        // GET REMINDER THRESHOLD MINUTES
        // Lee de MonthEnd_Email_Alert_Settings el threshold de GuardDraftReminder
        // ============================================================
        public int GetReminderThresholdMinutes(int defaultMinutes = 120) {
            try {
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(Threshold_Minutes, @Default)
                    FROM MonthEnd_Email_Alert_Settings
                    WHERE Alert_Key = 'GuardDraftReminder'", conn)) {
                    cmd.Parameters.AddWithValue("@Default", defaultMinutes);
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    return val != null && val != DBNull.Value ? (int)val : defaultMinutes;
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetReminderThresholdMinutes", ex);
                return defaultMinutes;
            }
        }

        // ============================================================
        // GET UNFILLED SPOT DEPT ADMIN EMAILS
        // Para el reminder: admins (Role_Id=3) de cada depto con spot vacío
        // ============================================================
        public List<(string Email, string Lang)> GetDeptAdminEmailsWithLang(int guardId) {
            var list = new List<(string, string)>();
            try {
                string sql = @"
                    SELECT DISTINCT u.Email, ISNULL(u.Preferred_Lang,'es') AS Preferred_Lang
                    FROM MonthEnd_Guard_Spots gs
                    INNER JOIN MonthEnd_Departments d ON d.Department_Id = gs.Department_Id
                    INNER JOIN MonthEnd_Users u
                           ON u.Department_Id = gs.Department_Id
                    WHERE gs.Guard_Id  = @GuardId
                      AND gs.User_Id   IS NULL
                      AND u.Role_Id    = 3
                      AND u.Active     = 1
                      AND u.Locked     = 0
                      AND u.Email IS NOT NULL AND u.Email <> ''";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd  = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add((r["Email"].ToString().Trim(), r["Preferred_Lang"].ToString()));
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetDeptAdminEmailsWithLang", ex);
            }
            return list;
        }

        public string GetDeptAdminEmails(int guardId) {
            try {
                string sql = @"
                    SELECT DISTINCT u.Email
                    FROM MonthEnd_Guard_Spots gs
                    INNER JOIN MonthEnd_Departments d ON d.Department_Id = gs.Department_Id
                    INNER JOIN MonthEnd_Users u
                           ON u.Department_Id = gs.Department_Id
                    WHERE gs.Guard_Id  = @GuardId
                      AND gs.User_Id   IS NULL
                      AND u.Role_Id    = 3
                      AND u.Active     = 1
                      AND u.Locked     = 0
                      AND u.Email IS NOT NULL AND u.Email <> ''";
                var emails = new List<string>();
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) emails.Add(r["Email"].ToString().Trim());
                }
                return emails.Count > 0 ? string.Join(";", emails) : null;
            } catch (Exception ex) {
                AppLogger.Error("EmailDataAccess.GetDeptAdminEmails", ex);
                return null;
            }
        }
    }
}