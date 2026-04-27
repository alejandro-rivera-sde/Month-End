using Close_Portal.Core;
using Close_Portal.DataAccess;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;

namespace Close_Portal.Services {
    public static class EmailService {

        // ── SMTP — permanece en web.config (credenciales sensibles) ─────────
        private static readonly string SmtpHost     = ConfigurationManager.AppSettings["Smtp_Host"];
        private static readonly int    SmtpPort     = int.Parse(ConfigurationManager.AppSettings["Smtp_Port"] ?? "587");
        private static readonly string SmtpUser     = ConfigurationManager.AppSettings["Smtp_User"];
        private static readonly string SmtpPassword = ConfigurationManager.AppSettings["Smtp_Password"];
        private static readonly string SmtpFrom     = ConfigurationManager.AppSettings["Smtp_From"];
        private static readonly bool   SmtpSsl      = bool.Parse(ConfigurationManager.AppSettings["Smtp_EnableSsl"] ?? "true");

        // ── Cache en memoria — fuente de verdad: DB ──────────────────────────
        private static volatile bool _notificationsEnabled = false;
        private static volatile bool _testMode             = false;
        private static string        _testRecipient        = "";
        private static readonly object _cfgLock = new object();

        public static bool   NotificationsEnabled => _notificationsEnabled;
        public static bool   TestMode             => _testMode;
        public static string TestRecipient        => _testRecipient;

        private static readonly object _alertLock = new object();
        private static readonly Dictionary<string, bool> _alertEnabled =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
                { "UserAdded",           true }, { "UserRemoved",         true },
                { "UserUpdated",         true }, { "ClosureRequest",      true },
                { "GuardStarted",        true }, { "GuardClosed",         true },
                { "GuardDraft",          true }, { "GuardDraftCancelled", true },
                { "GuardConfirmed",      true }, { "GuardCancelled",      true },
                { "ClosureResponse",     true },
                { "UserBlocked",         true }, { "UserUnblocked",       true },
                { "GuardDraftReminder",  true }, { "DefaultSpotReminder", true },
                { "LocationReverted",    true },
            };

        private static readonly Dictionary<string, string> _alertGroupKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ── Cargar desde DB al arrancar ───────────────────────────────────
        public static void LoadConfig() {
            try {
                var da  = new EmailDataAccess();
                var cfg = da.GetServiceConfig();
                lock (_cfgLock) {
                    _notificationsEnabled = cfg.NotificationsEnabled;
                    _testMode             = cfg.TestMode;
                    _testRecipient        = cfg.TestRecipient ?? "";
                }
                var alerts = da.GetAlertSettings();
                lock (_alertLock) {
                    foreach (var a in alerts) {
                        if (_alertEnabled.ContainsKey(a.AlertKey))
                            _alertEnabled[a.AlertKey] = a.Enabled;
                        if (!string.IsNullOrWhiteSpace(a.AlertGroupKey))
                            _alertGroupKeys[a.AlertKey] = a.AlertGroupKey;
                    }
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailService.LoadConfig", ex);
            }
        }

        public static void InvalidateCache(bool? notificationsEnabled = null,
                                           bool? testMode = null, string testRecipient = null) {
            lock (_cfgLock) {
                if (notificationsEnabled.HasValue) _notificationsEnabled = notificationsEnabled.Value;
                if (testMode.HasValue)             _testMode             = testMode.Value;
                if (testRecipient != null)         _testRecipient        = testRecipient;
            }
        }

        public static bool IsAlertEnabled(string key) {
            lock (_alertLock) { return _alertEnabled.TryGetValue(key, out bool v) && v; }
        }
        public static void SetAlertEnabledCache(string key, bool enabled) {
            lock (_alertLock) { if (_alertEnabled.ContainsKey(key)) _alertEnabled[key] = enabled; }
        }
        public static void SetAlertGroupKeyCache(string key, string groupKey) {
            lock (_alertLock) { _alertGroupKeys[key] = groupKey ?? ""; }
        }
        public static Dictionary<string, bool> GetAlertStates() {
            lock (_alertLock) { return new Dictionary<string, bool>(_alertEnabled); }
        }

        // ════════════════════════════════════════════════════════════════
        // RESOLUCIÓN DE GRUPOS — con y sin lang
        // ════════════════════════════════════════════════════════════════
        private static string ResolveGroup(string groupKey) =>
            new EmailDataAccess().ResolveGroupEmails(groupKey);

        private static List<(string Email, string Lang)> ResolveGroupWithLang(string groupKey) =>
            new EmailDataAccess().ResolveGroupEmailsWithLang(groupKey);

        // Resuelve el grupo configurado para una alerta (con fallback) y devuelve (Email, Lang) por recipient.
        // En modo test devuelve directamente el test recipient para que Send() pueda dispararse siempre.
        private static List<(string Email, string Lang)> GetAlertRecipientsWithLang(
                string alertKey, string fallbackGroupKey) {
            string groupKey;
            lock (_alertLock) { _alertGroupKeys.TryGetValue(alertKey, out groupKey); }
            if (string.IsNullOrWhiteSpace(groupKey)) groupKey = fallbackGroupKey;
            if (string.IsNullOrWhiteSpace(groupKey)) return null;

            // En modo test no consultamos el grupo — Send() ya reemplaza el destinatario.
            // Devolvemos el test recipient para que los métodos Notify* no hagan early-return.
            if (_testMode && !string.IsNullOrWhiteSpace(_testRecipient)) {
                string lang = new EmailDataAccess().GetUserLangByEmail(_testRecipient);
                return new List<(string, string)> { (_testRecipient, lang) };
            }

            return ResolveGroupWithLang(groupKey);
        }

        // Agrupa una lista (Email, Lang) por idioma y devuelve el semicolón-string de emails por grupo.
        private static IEnumerable<(string Lang, string Emails)> GroupByLang(
                List<(string Email, string Lang)> recipients) {
            if (recipients == null || recipients.Count == 0) yield break;
            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (email, lang) in recipients) {
                string l = string.IsNullOrWhiteSpace(lang) ? "es" : lang;
                if (!groups.ContainsKey(l)) groups[l] = new List<string>();
                groups[l].Add(email);
            }
            foreach (var kv in groups)
                yield return (kv.Key, string.Join(";", kv.Value));
        }

        // Convierte un Dictionary<email, lang> + lista de emails en lista (Email, Lang).
        private static List<(string Email, string Lang)> ZipWithLangs(
                IEnumerable<string> emails, Dictionary<string, string> langs) {
            var list = new List<(string, string)>();
            foreach (var e in emails) {
                langs.TryGetValue(e, out string l);
                list.Add((e, string.IsNullOrWhiteSpace(l) ? "es" : l));
            }
            return list;
        }

        // ════════════════════════════════════════════════════════════════
        // NOTIFICACIONES PÚBLICAS
        // ════════════════════════════════════════════════════════════════

        public static void NotifyUserAdded(string targetEmail, string targetUsername,
                                           string targetRole, string performedByEmail) {
            var recipients = GetAlertRecipientsWithLang("UserAdded", "Administradores");
            if (recipients == null || recipients.Count == 0) return;
            foreach (var (lang, emails) in GroupByLang(recipients)) {
                Send(
                    subject: EmailStrings.UserAdded_Subject(lang, targetUsername ?? targetEmail),
                    body: EmailTemplateBuilder.Simple(EmailStrings.UserAdded_Title(lang), AlertStyles.UserAdded, new[] {
                        $"<b>{EmailStrings.LUser(lang)}:</b> {targetUsername ?? EmailStrings.NoName(lang)}",
                        $"<b>Email:</b> {targetEmail}",
                        targetRole != null ? $"<b>{EmailStrings.LRole(lang)}:</b> {targetRole}" : null,
                        $"<b>{EmailStrings.LBy(lang)}:</b> {performedByEmail}",
                        $"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                    }, lang),
                    recipientList: emails, alertKey: "UserAdded");
            }
        }

        public static void NotifyUserRemoved(string targetEmail, string targetUsername,
                                             string performedByEmail) {
            var recipients = GetAlertRecipientsWithLang("UserRemoved", "Administradores");
            if (recipients == null || recipients.Count == 0) return;
            foreach (var (lang, emails) in GroupByLang(recipients)) {
                Send(
                    subject: EmailStrings.UserRemoved_Subject(lang, targetUsername ?? targetEmail),
                    body: EmailTemplateBuilder.Simple(EmailStrings.UserRemoved_Title(lang), AlertStyles.UserRemoved, new[] {
                        $"<b>{EmailStrings.LUser(lang)}:</b> {targetUsername ?? EmailStrings.NoName(lang)}",
                        $"<b>Email:</b> {targetEmail}",
                        $"<b>{EmailStrings.LBy(lang)}:</b> {performedByEmail}",
                        $"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                    }, lang),
                    recipientList: emails, alertKey: "UserRemoved");
            }
        }

        public static void NotifyUserUpdated(string targetEmail, string targetUsername,
                                             IEnumerable<string> changes, string performedByEmail) {
            var recipients = GetAlertRecipientsWithLang("UserUpdated", "Administradores");
            if (recipients == null || recipients.Count == 0) return;
            var changeList = changes?.ToList() ?? new List<string>();
            foreach (var (lang, emails) in GroupByLang(recipients)) {
                var lines = new List<string> {
                    $"<b>{EmailStrings.LUser(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(targetUsername ?? EmailStrings.NoName(lang))}",
                    $"<b>Email:</b> {System.Web.HttpUtility.HtmlEncode(targetEmail)}",
                    $"<b>{EmailStrings.LChanges(lang)}:</b>"
                };
                foreach (var c in changeList)
                    lines.Add($"&nbsp;&nbsp;• {System.Web.HttpUtility.HtmlEncode(c)}");
                lines.Add($"<b>{EmailStrings.LBy(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(performedByEmail)}");
                lines.Add($"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs");
                Send(
                    subject: EmailStrings.UserUpdated_Subject(lang, targetUsername ?? targetEmail),
                    body: EmailTemplateBuilder.Simple(EmailStrings.UserUpdated_Title(lang), AlertStyles.UserUpdated, lines.ToArray(), lang),
                    recipientList: emails, alertKey: "UserUpdated");
            }
        }

        public static void NotifyClosureRequest(
                string managerEmail, string managerName,
                string requesterName, string requesterEmail,
                string wmsCode, string wmsName, string notes, int requestId) {

            string lang     = new EmailDataAccess().GetUserLangByEmail(managerEmail);
            string notesLine = string.IsNullOrWhiteSpace(notes) ? null
                : $"<b>Notes:</b> {System.Web.HttpUtility.HtmlEncode(notes)}";

            Send(
                subject: EmailStrings.ClosureRequest_Subject(lang, wmsCode),
                body: EmailTemplateBuilder.ClosureRequest(
                    EmailStrings.ClosureRequest_Title(lang),
                    AlertStyles.ClosureRequest,
                    EmailStrings.ClosureRequest_Intro(lang, System.Web.HttpUtility.HtmlEncode(managerName)),
                    new[] {
                        $"<b>{EmailStrings.LRequest(lang)} #:</b> {requestId}",
                        $"<b>{EmailStrings.LWarehouse(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(wmsCode)} — {System.Web.HttpUtility.HtmlEncode(wmsName)}",
                        $"<b>{EmailStrings.LRequestedBy(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(requesterName)} ({System.Web.HttpUtility.HtmlEncode(requesterEmail)})",
                        notesLine
                    },
                    EmailStrings.ClosureRequest_Footer(lang, requestId),
                    lang),
                recipientList: managerEmail,
                alertKey: "ClosureRequest");
        }

        public static void NotifyGuardStarted(
                DateTime startTime, string startedByEmail,
                List<(string DeptCode, string DeptName, string Username, string Email)> spots) {

            var emailSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in spots)
                if (!string.IsNullOrWhiteSpace(s.Email)) emailSet.Add(s.Email.Trim());

            string activeGuards = GuardDataAccess.GetActiveGuardEmails();
            if (!string.IsNullOrWhiteSpace(activeGuards))
                foreach (var addr in activeGuards.Split(';'))
                    if (!string.IsNullOrWhiteSpace(addr)) emailSet.Add(addr.Trim());

            if (emailSet.Count == 0) return;

            var langs      = new EmailDataAccess().GetLangsByEmails(emailSet);
            var recipients = ZipWithLangs(emailSet, langs);

            foreach (var (lang, emails) in GroupByLang(recipients)) {
                Send(
                    subject: EmailStrings.GuardStarted_Subject(lang, startTime),
                    body: EmailTemplateBuilder.WithSpotsTable(
                        EmailStrings.GuardStarted_Title(lang), AlertStyles.GuardStarted,
                        EmailStrings.GuardStarted_Intro(lang, startTime, System.Web.HttpUtility.HtmlEncode(startedByEmail)),
                        spots, lang),
                    recipientList: emails, alertKey: "GuardStarted");
            }
        }

        public static void NotifyGuardClosed(DateTime closedAt, string triggeredByEmail) {
            var recipients = GetAlertRecipientsWithLang("GuardClosed", "Administradores");
            if (recipients == null || recipients.Count == 0) return;
            foreach (var (lang, emails) in GroupByLang(recipients)) {
                Send(
                    subject: EmailStrings.GuardClosed_Subject(lang, closedAt),
                    body: EmailTemplateBuilder.Simple(EmailStrings.GuardClosed_Title(lang), AlertStyles.GuardClosed, new[] {
                        EmailStrings.GuardClosed_Line1(lang),
                        $"<b>{EmailStrings.LClosureDate(lang)}:</b> {closedAt:dd/MM/yyyy HH:mm} hrs",
                        $"<b>{EmailStrings.LTriggeredBy(lang)}:</b> {triggeredByEmail ?? EmailStrings.System_(lang)}"
                    }, lang),
                    recipientList: emails, alertKey: "GuardClosed");
            }
        }

        public static void NotifyLocationReverted(int locationId, string locationName, string revertedBy) {
            var recipients = GetAlertRecipientsWithLang("LocationReverted", "Administradores");
            if (recipients == null || recipients.Count == 0) return;
            string safeLocation = System.Web.HttpUtility.HtmlEncode(locationName);
            string safeBy       = System.Web.HttpUtility.HtmlEncode(revertedBy ?? EmailStrings.System_("es"));
            foreach (var (lang, emails) in GroupByLang(recipients)) {
                Send(
                    subject: EmailStrings.LocationReverted_Subject(lang, locationName),
                    body: EmailTemplateBuilder.Simple(EmailStrings.LocationReverted_Title(lang), AlertStyles.LocationReverted, new[] {
                        EmailStrings.LocationReverted_Line1(lang, safeLocation),
                        EmailStrings.LocationReverted_Line2(lang),
                        $"<b>{EmailStrings.LReviewedBy(lang)}:</b> {safeBy}",
                        $"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                    }, lang),
                    recipientList: emails, alertKey: "LocationReverted");
            }
        }

        public static void NotifyGuardDraft(int guardId, DateTime startTime, string createdByEmail) {
            var recipients = GetAlertRecipientsWithLang("GuardDraft", "Administradores");
            if (recipients == null || recipients.Count == 0) return;
            foreach (var (lang, emails) in GroupByLang(recipients)) {
                Send(
                    subject: EmailStrings.GuardDraft_Subject(lang, startTime),
                    body: EmailTemplateBuilder.Simple(EmailStrings.GuardDraft_Title(lang), AlertStyles.GuardDraft, new[] {
                        EmailStrings.GuardDraft_Line1(lang),
                        $"<b>{EmailStrings.LScheduledStart(lang)}:</b> {startTime:dd/MM/yyyy HH:mm} hrs",
                        $"<b>{EmailStrings.LCreatedBy(lang)}:</b> {createdByEmail ?? EmailStrings.System_(lang)}",
                        $"<b>{EmailStrings.LNotifDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs",
                        EmailStrings.GuardDraft_Note(lang)
                    }, lang),
                    recipientList: emails, alertKey: "GuardDraft");
            }
        }

        public static void NotifyGuardDraftCancelled(DateTime? startTime, string cancelledByEmail) {
            string fechaStr = startTime.HasValue
                ? startTime.Value.ToString("dd/MM/yyyy HH:mm") + " hrs" : "—";
            var recipients = GetAlertRecipientsWithLang("GuardDraftCancelled", "Administradores");
            if (recipients == null || recipients.Count == 0) return;
            foreach (var (lang, emails) in GroupByLang(recipients)) {
                Send(
                    subject: EmailStrings.GuardDraftCancelled_Subject(lang, fechaStr),
                    body: EmailTemplateBuilder.Simple(EmailStrings.GuardDraftCancelled_Title(lang), AlertStyles.GuardDraftCancelled, new[] {
                        EmailStrings.GuardDraftCancelled_Line1(lang),
                        $"<b>{EmailStrings.LPrevStart(lang)}:</b> {fechaStr}",
                        $"<b>{EmailStrings.LCancelledBy(lang)}:</b> {cancelledByEmail ?? EmailStrings.System_(lang)}",
                        $"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                    }, lang),
                    recipientList: emails, alertKey: "GuardDraftCancelled");
            }
        }

        public static void NotifyGuardConfirmed(
                int guardId, DateTime? startTime,
                string confirmedByEmail,
                List<(string DeptCode, string DeptName, string Username, string Email)> spots,
                string audienceOverride = null) {

            List<(string Email, string Lang)> recipients;
            if (!string.IsNullOrWhiteSpace(audienceOverride)) {
                string overrideLang = new EmailDataAccess().GetUserLangByEmail(audienceOverride) ?? "es";
                recipients = new List<(string, string)> { (audienceOverride, overrideLang) };
            } else if (_testMode && !string.IsNullOrWhiteSpace(_testRecipient)) {
                string lang = new EmailDataAccess().GetUserLangByEmail(_testRecipient);
                recipients = new List<(string, string)> { (_testRecipient, lang) };
            } else {
                recipients = new EmailDataAccess().GetGuardAudienceEmailsWithLang(guardId);
            }
            if (recipients == null || recipients.Count == 0) return;

            string fechaStr = startTime.HasValue
                ? startTime.Value.ToString("dd/MM/yyyy HH:mm") + " hrs" : "—";

            foreach (var (lang, emails) in GroupByLang(recipients)) {
                Send(
                    subject: EmailStrings.GuardConfirmed_Subject(lang, fechaStr),
                    body: EmailTemplateBuilder.WithInfoAndOptionalSpotsTable(
                        EmailStrings.GuardConfirmed_Title(lang), AlertStyles.GuardConfirmed,
                        EmailStrings.GuardConfirmed_Intro(lang),
                        new[] {
                            $"<b>{EmailStrings.LStart(lang)}:</b> {fechaStr}",
                            $"<b>{EmailStrings.LConfirmedBy(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(confirmedByEmail ?? EmailStrings.System_(lang))}"
                        },
                        spots, lang),
                    recipientList: emails, alertKey: "GuardConfirmed");
            }
        }

        public static void NotifyGuardCancelled(
                DateTime? startTime, string cancelledByEmail, string audienceEmails) {
            if (string.IsNullOrWhiteSpace(audienceEmails)) return;

            string fechaStr = startTime.HasValue
                ? startTime.Value.ToString("dd/MM/yyyy HH:mm") + " hrs" : "—";

            List<(string Email, string Lang)> recipients;
            if (_testMode && !string.IsNullOrWhiteSpace(_testRecipient)) {
                string lang = new EmailDataAccess().GetUserLangByEmail(_testRecipient);
                recipients = new List<(string, string)> { (_testRecipient, lang) };
            } else {
                var emailList = new List<string>();
                foreach (var a in audienceEmails.Split(';'))
                    if (!string.IsNullOrWhiteSpace(a)) emailList.Add(a.Trim());

                var langs        = new EmailDataAccess().GetLangsByEmails(emailList);
                var guardAudience = ZipWithLangs(emailList, langs);
                var groupExtra    = GetAlertRecipientsWithLang("GuardCancelled", "Administradores");

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                recipients = new List<(string, string)>();
                foreach (var r in guardAudience)
                    if (seen.Add(r.Email)) recipients.Add(r);
                if (groupExtra != null)
                    foreach (var r in groupExtra)
                        if (seen.Add(r.Email)) recipients.Add(r);
            }

            foreach (var (lang, emails) in GroupByLang(recipients)) {
                Send(
                    subject: EmailStrings.GuardCancelled_Subject(lang, fechaStr),
                    body: EmailTemplateBuilder.Simple(EmailStrings.GuardCancelled_Title(lang), AlertStyles.GuardCancelled, new[] {
                        EmailStrings.GuardCancelled_Line1(lang),
                        $"<b>{EmailStrings.LPrevStart(lang)}:</b> {fechaStr}",
                        $"<b>{EmailStrings.LCancelledBy(lang)}:</b> {cancelledByEmail ?? EmailStrings.System_(lang)}",
                        $"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                    }, lang),
                    recipientList: emails, alertKey: "GuardCancelled");
            }
        }

        public static void NotifyClosureResponse(
                string requesterEmail, string requesterName,
                string locationName, string status,
                string reviewNotes, string reviewedBy) {

            bool   approved = status == "Approved";
            string lang     = new EmailDataAccess().GetUserLangByEmail(requesterEmail);
            string label    = EmailStrings.ClosureResponse_StatusLabel(lang, approved);

            string notesLine = !string.IsNullOrWhiteSpace(reviewNotes)
                ? $"<b>{EmailStrings.LReviewerComment(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(reviewNotes)}"
                : null;

            Send(
                subject: EmailStrings.ClosureResponse_Subject(lang, label, locationName),
                body: EmailTemplateBuilder.Simple(
                    EmailStrings.ClosureResponse_Title(lang, label), AlertStyles.ClosureResponse(approved), new[] {
                        EmailStrings.ClosureResponse_Line1(lang, System.Web.HttpUtility.HtmlEncode(requesterName), label),
                        $"<b>{EmailStrings.LLocation(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(locationName)}",
                        $"<b>{EmailStrings.LStatus(lang)}:</b> {(approved ? EmailStrings.LApproved(lang) : EmailStrings.LRejected(lang))}",
                        $"<b>{EmailStrings.LReviewedBy(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(reviewedBy ?? "—")}",
                        $"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs",
                        notesLine
                    }, lang),
                recipientList: requesterEmail, alertKey: "ClosureResponse");
        }

        public static void NotifyUserBlocked(
                string targetEmail, string targetUsername, string performedByEmail) {
            string lang    = new EmailDataAccess().GetUserLangByEmail(targetEmail);
            string dispName = System.Web.HttpUtility.HtmlEncode(targetUsername ?? targetEmail);
            Send(
                subject: EmailStrings.UserBlocked_Subject(lang),
                body: EmailTemplateBuilder.Simple(EmailStrings.UserBlocked_Title(lang), AlertStyles.UserBlocked, new[] {
                    EmailStrings.UserBlocked_Line1(lang, dispName),
                    EmailStrings.UserBlocked_Line2(lang),
                    EmailStrings.UserBlocked_Line3(lang),
                    $"<b>{EmailStrings.LActionBy(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(performedByEmail ?? EmailStrings.System_(lang))}",
                    $"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }, lang),
                recipientList: targetEmail, alertKey: "UserBlocked");
        }

        public static void NotifyUserUnblocked(
                string targetEmail, string targetUsername, string performedByEmail) {
            string lang    = new EmailDataAccess().GetUserLangByEmail(targetEmail);
            string dispName = System.Web.HttpUtility.HtmlEncode(targetUsername ?? targetEmail);
            Send(
                subject: EmailStrings.UserUnblocked_Subject(lang),
                body: EmailTemplateBuilder.Simple(EmailStrings.UserUnblocked_Title(lang), AlertStyles.UserUnblocked, new[] {
                    EmailStrings.UserBlocked_Line1(lang, dispName),
                    EmailStrings.UserUnblocked_Line2(lang),
                    $"<b>{EmailStrings.LActionBy(lang)}:</b> {System.Web.HttpUtility.HtmlEncode(performedByEmail ?? EmailStrings.System_(lang))}",
                    $"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }, lang),
                recipientList: targetEmail, alertKey: "UserUnblocked");
        }

        public static void NotifyDefaultSpotReminders(int guardId,
                IList<string> deptCodesOverride = null, string recipientOverride = null) {
            var da            = new EmailDataAccess();
            IList<string> unfilledDepts = deptCodesOverride ?? da.GetUnfilledSpotDeptCodes(guardId);
            if (unfilledDepts == null || unfilledDepts.Count == 0) return;

            foreach (var deptCode in unfilledDepts) {
                List<(string Email, string Lang)> recipients;
                if (!string.IsNullOrWhiteSpace(recipientOverride)) {
                    string overrideLang = new EmailDataAccess().GetUserLangByEmail(recipientOverride) ?? "es";
                    recipients = new List<(string, string)> { (recipientOverride, overrideLang) };
                } else if (_testMode && !string.IsNullOrWhiteSpace(_testRecipient)) {
                    string lang = new EmailDataAccess().GetUserLangByEmail(_testRecipient);
                    recipients = new List<(string, string)> { (_testRecipient, lang) };
                } else {
                    recipients = da.ResolveGroupEmailsWithLang($"DefaultSpot{deptCode.ToUpper()}");
                }
                if (recipients == null || recipients.Count == 0) continue;

                foreach (var (lang, emails) in GroupByLang(recipients)) {
                    Send(
                        subject: EmailStrings.DefaultSpotReminder_Subject(lang, deptCode),
                        body: EmailTemplateBuilder.Simple(
                            EmailStrings.DefaultSpotReminder_Title(lang, deptCode), AlertStyles.DefaultSpotReminder, new[] {
                                EmailStrings.DefaultSpotReminder_Line1(lang, deptCode),
                                EmailStrings.DefaultSpotReminder_Line2(lang),
                                $"<b>{EmailStrings.LDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                            }, lang),
                        recipientList: emails, alertKey: "DefaultSpotReminder");
                }
            }
        }

        public static void CheckDraftReminders() {
            try {
                if (!IsAlertEnabled("GuardDraftReminder")) return;

                var da        = new EmailDataAccess();
                int threshold = da.GetReminderThresholdMinutes(120);
                var drafts    = da.GetDraftGuardsForReminder(threshold);

                foreach (var (guardId, createdAt) in drafts) {
                    var recipients = da.GetDeptAdminEmailsWithLang(guardId);
                    if (recipients == null || recipients.Count == 0) {
                        da.MarkReminderSent(guardId);
                        continue;
                    }

                    int    hoursElapsed = (int)Math.Round((DateTime.Now - createdAt).TotalHours);
                    string scheduledStr = createdAt.ToString("dd/MM/yyyy HH:mm");

                    foreach (var (lang, emails) in GroupByLang(recipients)) {
                        string plural = hoursElapsed != 1 ? "s" : "";
                        Send(
                            subject: EmailStrings.GuardDraftReminder_Subject(lang, hoursElapsed),
                            body: EmailTemplateBuilder.Simple(
                                EmailStrings.GuardDraftReminder_Title(lang), AlertStyles.GuardDraftReminder, new[] {
                                    EmailStrings.GuardDraftReminder_Line1(lang, hoursElapsed, plural),
                                    $"<b>{EmailStrings.LScheduledOn(lang)}:</b> {scheduledStr} hrs",
                                    EmailStrings.GuardDraftReminder_Line3(lang),
                                    $"<b>{EmailStrings.LReminderDate(lang)}:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                                }, lang),
                            recipientList: emails, alertKey: "GuardDraftReminder");
                    }

                    da.MarkReminderSent(guardId);
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailService.CheckDraftReminders", ex);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // SEND
        // ════════════════════════════════════════════════════════════════
        private static void Send(string subject, string body,
                                 string recipientList = null, string alertKey = null) {
            if (!_notificationsEnabled) return;
            if (alertKey != null && !IsAlertEnabled(alertKey)) return;

            string targets;
            if (_testMode && !string.IsNullOrWhiteSpace(_testRecipient)) {
                targets = _testRecipient;
                subject = "[TEST] " + subject;
            } else {
                targets = recipientList ?? ResolveGroup("CallGod");
            }

            if (string.IsNullOrWhiteSpace(targets)) return;

            try {
                using (var mail = new MailMessage()) {
                    mail.From      = new MailAddress(SmtpFrom, "Close Portal");
                    mail.Subject   = subject;
                    mail.Body      = body;
                    mail.IsBodyHtml = true;
                    foreach (var addr in targets.Split(';')) {
                        var t = addr.Trim();
                        if (!string.IsNullOrEmpty(t)) mail.To.Add(t);
                    }
                    using (var smtp = new SmtpClient(SmtpHost, SmtpPort)) {
                        smtp.EnableSsl = SmtpSsl;
                        if (!string.IsNullOrWhiteSpace(SmtpUser) && !string.IsNullOrWhiteSpace(SmtpPassword)) {
                            smtp.Credentials = new NetworkCredential(SmtpUser, SmtpPassword);
                        } else {
                            smtp.UseDefaultCredentials = false;
                        }
                        smtp.Send(mail);
                    }
                }
            } catch (Exception ex) {
                AppLogger.Error("EmailService.Send", ex);
            }
        }

        private static string MergeGroups(params string[] groups) {
            var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in groups)
                if (!string.IsNullOrWhiteSpace(g))
                    foreach (var addr in g.Split(';'))
                        if (!string.IsNullOrWhiteSpace(addr)) all.Add(addr.Trim());
            return string.Join(";", all);
        }
    }
}
