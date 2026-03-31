using Close_Portal.Core;
using Close_Portal.DataAccess;
using Close_Portal.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web;
using System.Web.Services;
using System.Web.SessionState;

namespace Close_Portal.Pages.IT {
    public partial class EmailServicePage : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Owner;

        protected void Page_Load(object sender, EventArgs e) {
            // Sincronizar cache con DB en cada carga de página
            if (!IsPostBack) EmailService.LoadConfig();
        }

        // ─── Session helper ────────────────────────────────────────
        private static bool TryGetOwner(out HttpSessionState session, out int userId) {
            session = HttpContext.Current.Session;
            userId = 0;
            if (session["UserId"] == null) return false;
            userId = (int)session["UserId"];
            int roleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
            return roleId >= RoleLevel.Owner;
        }

        // ============================================================
        // GET FULL CONFIG
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetEmailConfig() {
            try {
                if (!TryGetOwner(out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var da = new EmailDataAccess();
                var cfg = da.GetServiceConfig();

                // SMTP (read-only desde web.config)
                var appCfg = ConfigurationManager.AppSettings;
                var smtp = new {
                    host = appCfg["Smtp_Host"] ?? "—",
                    port = appCfg["Smtp_Port"] ?? "587",
                    user = appCfg["Smtp_User"] ?? "—",
                    from = appCfg["Smtp_From"] ?? "—",
                    ssl = appCfg["Smtp_EnableSsl"] ?? "true"
                };

                // Grupos desde DB
                var groups = da.GetGroups();
                var groupDto = new List<object>();
                foreach (var g in groups) {
                    var members = new List<object>();
                    foreach (var m in g.Members)
                        members.Add(new {
                            memberId = m.MemberId,
                            email = m.Email,
                            displayName = m.DisplayName
                        });
                    groupDto.Add(new {
                        groupId = g.GroupId,
                        groupKey = g.GroupKey,
                        label = g.Label,
                        description = g.Description,
                        icon = g.Icon,
                        color = g.Color,
                        members
                    });
                }

                // Alertas desde DB con metadata estática
                var alertMeta = GetAlertMeta();
                var alertSettings = da.GetAlertSettings();
                var alertMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in alertSettings) alertMap[a.AlertKey] = a.Enabled;

                var alertsDto = new List<object>();
                foreach (var meta in alertMeta) {
                    bool enabled = alertMap.TryGetValue(meta.Key, out bool v) ? v : true;
                    alertsDto.Add(new {
                        key = meta.Key,
                        label = meta.Label,
                        icon = meta.Icon,
                        recipients = meta.Recipients,
                        enabled
                    });
                }

                return new {
                    success = true,
                    notificationsEnabled = cfg.NotificationsEnabled,
                    testMode = cfg.TestMode,
                    testRecipient = cfg.TestRecipient ?? "",
                    smtp,
                    groups = groupDto,
                    alerts = alertsDto
                };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetEmailConfig] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SERVICE CONFIG — guardar en DB + actualizar cache
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SetNotificationsEnabled(bool enabled) {
            try {
                if (!TryGetOwner(out _, out int userId))
                    return new { success = false, message = "Acceso no autorizado." };

                var da = new EmailDataAccess();
                var cfg = da.GetServiceConfig();
                var r = da.SaveServiceConfig(enabled, cfg.TestMode, cfg.TestRecipient, userId);
                if (r.Success) EmailService.InvalidateCache(notificationsEnabled: enabled);
                return r.Success ? (object)new { success = true }
                                 : new { success = false, message = r.Message };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        [WebMethod(EnableSession = true)]
        public static object SetTestMode(bool enabled, string testRecipient) {
            try {
                if (!TryGetOwner(out _, out int userId))
                    return new { success = false, message = "Acceso no autorizado." };

                var da = new EmailDataAccess();
                var cfg = da.GetServiceConfig();
                var r = da.SaveServiceConfig(cfg.NotificationsEnabled, enabled,
                                               testRecipient?.Trim() ?? "", userId);
                if (r.Success) EmailService.InvalidateCache(testMode: enabled,
                                                            testRecipient: testRecipient?.Trim() ?? "");
                return r.Success ? (object)new { success = true }
                                 : new { success = false, message = r.Message };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // ALERT SETTINGS
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SetAlertEnabled(string alertKey, bool enabled) {
            try {
                if (!TryGetOwner(out _, out int userId))
                    return new { success = false, message = "Acceso no autorizado." };

                var r = new EmailDataAccess().SetAlertEnabled(alertKey, enabled, userId);
                if (r.Success) EmailService.SetAlertEnabledCache(alertKey, enabled);
                return r.Success ? (object)new { success = true }
                                 : new { success = false, message = r.Message };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        [WebMethod(EnableSession = true)]
        public static object SetBulkAlerts(bool enabled) {
            try {
                if (!TryGetOwner(out _, out int userId))
                    return new { success = false, message = "Acceso no autorizado." };

                var r = new EmailDataAccess().SetBulkAlerts(enabled, userId);
                if (r.Success)
                    foreach (var key in new[] { "UserAdded", "UserRemoved", "UserUpdated",
                                                "ClosureRequest", "GuardStarted", "GuardClosed" })
                        EmailService.SetAlertEnabledCache(key, enabled);

                return r.Success ? (object)new { success = true }
                                 : new { success = false, message = r.Message };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GRUPOS — CRUD
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object CreateGroup(string groupKey, string label,
                                         string description, string icon, string color) {
            try {
                if (!TryGetOwner(out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var r = new EmailDataAccess().CreateGroup(groupKey, label, description, icon, color);
                return r.Success ? (object)new { success = true, groupId = r.Id }
                                 : new { success = false, message = r.Message };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        [WebMethod(EnableSession = true)]
        public static object UpdateGroup(int groupId, string label,
                                         string description, string icon, string color) {
            try {
                if (!TryGetOwner(out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var r = new EmailDataAccess().UpdateGroup(groupId, label, description, icon, color);
                return r.Success ? (object)new { success = true }
                                 : new { success = false, message = r.Message };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        [WebMethod(EnableSession = true)]
        public static object DeleteGroup(int groupId) {
            try {
                if (!TryGetOwner(out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var r = new EmailDataAccess().DeleteGroup(groupId);
                return r.Success ? (object)new { success = true }
                                 : new { success = false, message = r.Message };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // MIEMBROS — CRUD
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object AddMember(int groupId, string email, string displayName) {
            try {
                if (!TryGetOwner(out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var r = new EmailDataAccess().AddMember(groupId, email, displayName);
                return r.Success ? (object)new { success = true, memberId = r.Id }
                                 : new { success = false, message = r.Message };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        [WebMethod(EnableSession = true)]
        public static object RemoveMember(int memberId) {
            try {
                if (!TryGetOwner(out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var r = new EmailDataAccess().RemoveMember(memberId);
                return r.Success ? (object)new { success = true }
                                 : new { success = false, message = r.Message };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SEND TEST EMAIL
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SendTestEmail(string alertKey, string overrideRecipient) {
            try {
                if (!TryGetOwner(out var session, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                if (!EmailService.NotificationsEnabled)
                    return new { success = false, message = "Activa las notificaciones primero." };

                string recipient = !string.IsNullOrWhiteSpace(overrideRecipient)
                    ? overrideRecipient.Trim()
                    : !string.IsNullOrWhiteSpace(EmailService.TestRecipient)
                        ? EmailService.TestRecipient
                        : ConfigurationManager.AppSettings["Call_God"];

                if (string.IsNullOrWhiteSpace(recipient))
                    return new { success = false, message = "Configura un correo de prueba primero." };

                string performer = session["Email"]?.ToString() ?? "IT";

                // Guardar estado temporal, forzar test mode para este envío
                bool wasTest = EmailService.TestMode;
                string wasRecip = EmailService.TestRecipient;
                bool wasDisabled = !EmailService.IsAlertEnabled(alertKey);

                EmailService.InvalidateCache(testMode: true, testRecipient: recipient);
                if (wasDisabled) EmailService.SetAlertEnabledCache(alertKey, true);

                try {
                    SendTestAlertByKey(alertKey, performer);
                } finally {
                    EmailService.InvalidateCache(testMode: wasTest, testRecipient: wasRecip);
                    if (wasDisabled) EmailService.SetAlertEnabledCache(alertKey, false);
                }

                return new { success = true, sentTo = recipient };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[SendTestEmail] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ─── Helpers ────────────────────────────────────────────────
        private static void SendTestAlertByKey(string key, string performer) {
            switch (key) {
                case "UserAdded":
                    EmailService.NotifyUserAdded("test@example.com", "Usuario de Prueba", "Admin", performer);
                    break;
                case "UserRemoved":
                    EmailService.NotifyUserRemoved("test@example.com", "Usuario de Prueba", performer);
                    break;
                case "UserUpdated":
                    EmailService.NotifyUserUpdated("test@example.com", "Usuario de Prueba", "Administrador", performer);
                    break;
                case "ClosureRequest":
                    EmailService.NotifyClosureRequest(
                        EmailService.TestRecipient, "Manager Prueba", "Regular Prueba",
                        "regular@test.com", "TEST", "Bodega de Prueba", "Notas de prueba", 0);
                    break;
                case "GuardStarted":
                    EmailService.NotifyGuardStarted(DateTime.Now, performer,
                        new List<(string, string, string, string)> {
                            ("AR", "Cuentas por Cobrar", "Usuario Prueba", "test@example.com")
                        });
                    break;
                case "GuardClosed":
                    EmailService.NotifyGuardClosed(DateTime.Now, performer);
                    break;
            }
        }

        private static List<(string Key, string Label, string Icon, string Recipients)> GetAlertMeta() =>
            new List<(string, string, string, string)> {
                ("UserAdded",      "Usuario agregado",      "person_add",      "Guardia activa / Call God"),
                ("UserRemoved",    "Usuario desactivado",   "person_remove",   "Guardia activa / Call God"),
                ("UserUpdated",    "Usuario modificado",    "manage_accounts", "Team IT + Guardia activa"),
                ("ClosureRequest", "Solicitud de cierre",   "lock",            "Manager de la locación"),
                ("GuardStarted",   "Guardia iniciada",      "security",        "Responsables de spots"),
                ("GuardClosed",    "Guardia cerrada",       "lock_clock",      "Guardia activa / Call God"),
            };
    }
}