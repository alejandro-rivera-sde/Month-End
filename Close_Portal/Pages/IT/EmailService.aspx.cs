using Close_Portal.Core;
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

        protected void Page_Load(object sender, EventArgs e) { }

        // ─── Session helper ────────────────────────────────────────────
        private static bool TryGetOwnerSession(out HttpSessionState session) {
            session = HttpContext.Current.Session;
            if (session["UserId"] == null) return false;
            int roleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
            return roleId >= RoleLevel.Owner;
        }

        // ============================================================
        // GET EMAIL CONFIG — estado completo del servicio
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetEmailConfig() {
            try {
                if (!TryGetOwnerSession(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var cfg = ConfigurationManager.AppSettings;
                var smtp = new {
                    host = cfg["Smtp_Host"] ?? "—",
                    port = cfg["Smtp_Port"] ?? "587",
                    user = cfg["Smtp_User"] ?? "—",
                    from = cfg["Smtp_From"] ?? "—",
                    ssl = cfg["Smtp_EnableSsl"] ?? "true"
                };

                var groups = new List<object> {
                    new {
                        key   = "CallGod",
                        label = "Call God",
                        desc  = "Administradores principales. Fallback cuando no hay guardia activa.",
                        icon  = "emergency",
                        color = "red",
                        emails = SplitEmails(cfg["Call_God"])
                    },
                    new {
                        key   = "TeamIT",
                        label = "Team IT",
                        desc  = "Equipo de IT. Recibe notificaciones de cambios de usuario.",
                        icon  = "computer",
                        color = "blue",
                        emails = SplitEmails(cfg["Notify_TeamIT"])
                    }
                };

                var alerts = new List<object> {
                    new { key = "UserAdded",      label = "Usuario agregado",         icon = "person_add",     recipients = "Guardia activa / Call God" },
                    new { key = "UserRemoved",    label = "Usuario desactivado",      icon = "person_remove",  recipients = "Guardia activa / Call God" },
                    new { key = "UserUpdated",    label = "Usuario modificado",       icon = "manage_accounts",recipients = "Team IT + Guardia activa"  },
                    new { key = "ClosureRequest", label = "Solicitud de cierre",      icon = "lock",           recipients = "Manager de la locación"    },
                    new { key = "GuardStarted",   label = "Guardia iniciada",         icon = "security",       recipients = "Responsables de spots"     },
                    new { key = "GuardClosed",    label = "Guardia cerrada",          icon = "lock_clock",     recipients = "Guardia activa / Call God" },
                };

                // Inyectar estado enabled por alerta
                var alertStates = EmailService.GetAlertStates();
                var alertsWithState = new List<object>();
                foreach (var a in alerts) {
                    dynamic d = a;
                    string k = d.key;
                    alertsWithState.Add(new {
                        key = (string)d.key,
                        label = (string)d.label,
                        icon = (string)d.icon,
                        recipients = (string)d.recipients,
                        enabled = alertStates.TryGetValue(k, out bool v) && v
                    });
                }

                return new {
                    success = true,
                    notificationsEnabled = EmailService.NotificationsEnabled,
                    testMode = EmailService.TestMode,
                    testRecipient = EmailService.TestRecipient ?? "",
                    smtp,
                    groups,
                    alerts = alertsWithState
                };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[EmailConfig.GetEmailConfig] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SET NOTIFICATIONS ENABLED
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SetNotificationsEnabled(bool enabled) {
            try {
                if (!TryGetOwnerSession(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                EmailService.NotificationsEnabled = enabled;
                System.Diagnostics.Debug.WriteLine($"[EmailService] NotificationsEnabled → {enabled}");
                return new { success = true };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SET TEST MODE
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SetTestMode(bool enabled, string testRecipient) {
            try {
                if (!TryGetOwnerSession(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                EmailService.TestMode = enabled;
                EmailService.TestRecipient = (testRecipient ?? "").Trim();
                System.Diagnostics.Debug.WriteLine(
                    $"[EmailService] TestMode → {enabled} | Recipient → {EmailService.TestRecipient}");
                return new { success = true };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SET ALERT ENABLED
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SetAlertEnabled(string alertKey, bool enabled) {
            try {
                if (!TryGetOwnerSession(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                EmailService.SetAlertEnabled(alertKey, enabled);
                System.Diagnostics.Debug.WriteLine($"[EmailService] Alert '{alertKey}' → {enabled}");
                return new { success = true };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SET BULK ALERTS
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SetBulkAlerts(bool enabled) {
            try {
                if (!TryGetOwnerSession(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                foreach (var key in new[] { "UserAdded", "UserRemoved", "UserUpdated",
                                            "ClosureRequest", "GuardStarted", "GuardClosed" })
                    EmailService.SetAlertEnabled(key, enabled);

                System.Diagnostics.Debug.WriteLine($"[EmailService] Bulk alerts → {enabled}");
                return new { success = true };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SEND TEST EMAIL — envía un correo de prueba para una alerta
        // Si testRecipient está vacío usa el TestRecipient configurado,
        // si también está vacío usa Call_God.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SendTestEmail(string alertKey, string overrideRecipient) {
            try {
                if (!TryGetOwnerSession(out var session))
                    return new { success = false, message = "Acceso no autorizado." };

                string performer = session["Email"]?.ToString() ?? "IT";
                string recipient = !string.IsNullOrWhiteSpace(overrideRecipient)
                    ? overrideRecipient.Trim()
                    : !string.IsNullOrWhiteSpace(EmailService.TestRecipient)
                        ? EmailService.TestRecipient
                        : ConfigurationManager.AppSettings["Call_God"];

                if (string.IsNullOrWhiteSpace(recipient))
                    return new { success = false, message = "No hay destinatario configurado para la prueba." };

                if (!EmailService.NotificationsEnabled)
                    return new { success = false, message = "Las notificaciones están desactivadas. Actívalas primero para enviar una prueba." };

                // Enviar correo de prueba usando los templates reales
                bool wasPreviouslyDisabled = !EmailService.IsAlertEnabled(alertKey);
                if (wasPreviouslyDisabled) EmailService.SetAlertEnabled(alertKey, true);

                bool wasTestMode = EmailService.TestMode;
                string wasRecipient = EmailService.TestRecipient;
                EmailService.TestMode = true;
                EmailService.TestRecipient = recipient;

                try {
                    SendTestAlertByKey(alertKey, performer);
                } finally {
                    EmailService.TestMode = wasTestMode;
                    EmailService.TestRecipient = wasRecipient;
                    if (wasPreviouslyDisabled) EmailService.SetAlertEnabled(alertKey, false);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[EmailService] Test '{alertKey}' enviado a {recipient}");
                return new { success = true, sentTo = recipient };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[SendTestEmail] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ─── Helpers ────────────────────────────────────────────────
        private static List<string> SplitEmails(string raw) {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var e in raw.Split(';'))
                if (!string.IsNullOrWhiteSpace(e)) list.Add(e.Trim());
            return list;
        }

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
                        EmailService.TestRecipient, "Manager Prueba",
                        "Regular Prueba", "regular@test.com",
                        "TEST", "Bodega de Prueba", "Notas de prueba", 0);
                    break;
                case "GuardStarted":
                    var spots = new List<(string, string, string, string)> {
                        ("AR",   "Cuentas por Cobrar", "Usuario Prueba", "test@example.com")
                    };
                    EmailService.NotifyGuardStarted(DateTime.Now, performer, spots);
                    break;
                case "GuardClosed":
                    EmailService.NotifyGuardClosed(DateTime.Now, performer);
                    break;
            }
        }
    }
}