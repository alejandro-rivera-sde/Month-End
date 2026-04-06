using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using Close_Portal.DataAccess;

namespace Close_Portal.Services {
    public static class EmailService {

        // ── SMTP — permanece en web.config (credenciales sensibles) ─────────
        private static readonly string SmtpHost = ConfigurationManager.AppSettings["Smtp_Host"];
        private static readonly int SmtpPort = int.Parse(ConfigurationManager.AppSettings["Smtp_Port"] ?? "587");
        private static readonly string SmtpUser = ConfigurationManager.AppSettings["Smtp_User"];
        private static readonly string SmtpPassword = ConfigurationManager.AppSettings["Smtp_Password"];
        private static readonly string SmtpFrom = ConfigurationManager.AppSettings["Smtp_From"];
        private static readonly bool SmtpSsl = bool.Parse(ConfigurationManager.AppSettings["Smtp_EnableSsl"] ?? "true");

        // ── Cache en memoria — fuente de verdad: DB ──────────────────────────
        private static volatile bool _notificationsEnabled = false;
        private static volatile bool _testMode = false;
        private static string _testRecipient = "";
        private static readonly object _cfgLock = new object();

        public static bool NotificationsEnabled => _notificationsEnabled;
        public static bool TestMode => _testMode;
        public static string TestRecipient => _testRecipient;

        private static readonly object _alertLock = new object();
        private static readonly Dictionary<string, bool> _alertEnabled =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
                // existentes
                { "UserAdded",           true }, { "UserRemoved",         true },
                { "UserUpdated",         true }, { "ClosureRequest",      true },
                { "GuardStarted",        true }, { "GuardClosed",         true },
                // nuevos
                { "GuardDraft",          true }, { "GuardDraftCancelled", true },
                { "GuardConfirmed",      true }, { "GuardCancelled",      true },
                { "ClosureResponse",     true },
                { "UserBlocked",         true }, { "UserUnblocked",       true },
                { "GuardDraftReminder",  true }, { "DefaultSpotReminder", true },
            };

        // Cache de grupo destinatario por alerta (clave = alertKey, valor = groupKey)
        private static readonly Dictionary<string, string> _alertGroupKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ── Cargar desde DB al arrancar ───────────────────────────────────
        public static void LoadConfig() {
            try {
                var da = new EmailDataAccess();
                var cfg = da.GetServiceConfig();
                lock (_cfgLock) {
                    _notificationsEnabled = cfg.NotificationsEnabled;
                    _testMode = cfg.TestMode;
                    _testRecipient = cfg.TestRecipient ?? "";
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
                Debug.WriteLine($"[EmailService.LoadConfig] Enabled:{_notificationsEnabled} Test:{_testMode}");
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailService.LoadConfig] ERROR: {ex.Message}");
            }
        }

        // ── Actualizar cache sin re-leer toda la DB ───────────────────────
        public static void InvalidateCache(bool? notificationsEnabled = null,
                                           bool? testMode = null, string testRecipient = null) {
            lock (_cfgLock) {
                if (notificationsEnabled.HasValue) _notificationsEnabled = notificationsEnabled.Value;
                if (testMode.HasValue) _testMode = testMode.Value;
                if (testRecipient != null) _testRecipient = testRecipient;
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

        // ── Resolver destinatarios configurables ─────────────────────────
        // Primero busca el grupo configurado en cache/DB para esa alerta.
        // Si no hay, usa fallbackGroupKey. Permite al Owner cambiar
        // destinatarios sin tocar código.
        private static string GetAlertRecipients(string alertKey, string fallbackGroupKey) {
            string groupKey;
            lock (_alertLock) {
                _alertGroupKeys.TryGetValue(alertKey, out groupKey);
            }
            if (string.IsNullOrWhiteSpace(groupKey))
                groupKey = fallbackGroupKey;
            return string.IsNullOrWhiteSpace(groupKey) ? null : ResolveGroup(groupKey);
        }

        // ════════════════════════════════════════════════════════════════
        // RESOLUCIÓN DE GRUPOS desde DB
        // ════════════════════════════════════════════════════════════════
        private static string ResolveGroup(string groupKey) =>
            new EmailDataAccess().ResolveGroupEmails(groupKey);

        private static string ResolveGuardRecipients() {
            var active = GuardDataAccess.GetActiveGuardEmails();
            if (!string.IsNullOrWhiteSpace(active)) return active;
            return ResolveGroup("CallGod");
        }

        // ════════════════════════════════════════════════════════════════
        // NOTIFICACIONES PÚBLICAS
        // ════════════════════════════════════════════════════════════════
        public static void NotifyUserAdded(string targetEmail, string targetUsername,
                                           string targetRole, string performedByEmail) {
            Send(
                subject: $"[Close Portal] Usuario agregado: {targetUsername ?? targetEmail}",
                body: BuildTemplate("Nuevo usuario agregado", "#10b981", "&#10010;", new[] {
                    $"<b>Usuario:</b> {targetUsername ?? "(sin nombre)"}",
                    $"<b>Email:</b> {targetEmail}",
                    targetRole != null ? $"<b>Rol asignado:</b> {targetRole}" : null,
                    $"<b>Realizado por:</b> {performedByEmail}",
                    $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }),
                recipientList: GetAlertRecipients("UserAdded", "Administradores"),
                alertKey: "UserAdded");
        }

        public static void NotifyUserRemoved(string targetEmail, string targetUsername,
                                             string performedByEmail) {
            Send(
                subject: $"[Close Portal] Usuario desactivado: {targetUsername ?? targetEmail}",
                body: BuildTemplate("Usuario desactivado", "#ef4444", "&#10006;", new[] {
                    $"<b>Usuario:</b> {targetUsername ?? "(sin nombre)"}",
                    $"<b>Email:</b> {targetEmail}",
                    $"<b>Realizado por:</b> {performedByEmail}",
                    $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }),
                recipientList: GetAlertRecipients("UserRemoved", "Administradores"),
                alertKey: "UserRemoved");
        }

        public static void NotifyUserUpdated(string targetEmail, string targetUsername,
                                             string newRole, string performedByEmail) {
            Send(
                subject: $"[Close Portal] Usuario modificado: {targetUsername ?? targetEmail}",
                body: BuildTemplate("Usuario modificado", "#6366f1", "&#9998;", new[] {
                    $"<b>Usuario:</b> {targetUsername ?? "(sin nombre)"}",
                    $"<b>Email:</b> {targetEmail}",
                    $"<b>Nuevo rol:</b> {newRole}",
                    $"<b>Realizado por:</b> {performedByEmail}",
                    $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }),
                recipientList: GetAlertRecipients("UserUpdated", "Administradores"),
                alertKey: "UserUpdated");
        }

        public static void NotifyClosureRequest(
                string managerEmail, string managerName,
                string requesterName, string requesterEmail,
                string wmsCode, string wmsName, string notes, int requestId) {
            Send(
                subject: $"[Close Portal] Solicitud de cierre — {wmsCode}",
                body: BuildClosureRequestTemplate(managerName, requesterName,
                          requesterEmail, wmsCode, wmsName, notes, requestId),
                recipientList: managerEmail,
                alertKey: "ClosureRequest");
        }

        public static void NotifyGuardStarted(
                DateTime startTime, string startedByEmail,
                List<(string DeptCode, string DeptName, string Username, string Email)> spots) {

            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in spots)
                if (!string.IsNullOrWhiteSpace(s.Email)) recipients.Add(s.Email.Trim());

            string activeGuards = GuardDataAccess.GetActiveGuardEmails();
            if (!string.IsNullOrWhiteSpace(activeGuards))
                foreach (var addr in activeGuards.Split(';'))
                    if (!string.IsNullOrWhiteSpace(addr)) recipients.Add(addr.Trim());

            if (recipients.Count == 0) return;

            var spotRows = new System.Text.StringBuilder();
            foreach (var s in spots)
                spotRows.Append($@"
                <tr>
                  <td style='padding:10px 14px;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'>
                    <span style='display:inline-block;background:#fef3c7;color:#d97706;border:1px solid #fde68a;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700;margin-right:10px;vertical-align:middle;'>{System.Web.HttpUtility.HtmlEncode(s.DeptCode)}</span>
                    {System.Web.HttpUtility.HtmlEncode(s.DeptName)}
                  </td>
                  <td style='padding:10px 14px;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;font-weight:600;'>
                    {System.Web.HttpUtility.HtmlEncode(s.Username ?? s.Email)}
                  </td>
                </tr>");

            string body = $@"<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f8fafc;font-family:sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0'><tr><td align='center' style='padding:40px 20px;'>
<table width='560' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
  <tr><td style='background:#f59e0b;padding:28px 32px;text-align:center;'>
    <div style='font-size:40px;color:#fff;margin-bottom:8px;'>&#128737;</div>
    <h1 style='margin:0;color:#fff;font-size:20px;font-weight:700;'>Guardia iniciada</h1>
    <p style='margin:6px 0 0;color:rgba(255,255,255,0.85);font-size:13px;'>Close Portal &mdash; Novamex</p>
  </td></tr>
  <tr><td style='padding:28px 32px;'>
    <p style='margin:0 0 20px;font-size:15px;color:#334155;'>Guardia iniciada el <strong>{startTime:dd/MM/yyyy}</strong> a las <strong>{startTime:HH:mm} hrs</strong> por <strong>{System.Web.HttpUtility.HtmlEncode(startedByEmail)}</strong>.</p>
    <table width='100%' cellpadding='0' cellspacing='0' style='border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;'>
      <thead><tr style='background:#f8fafc;'>
        <th style='padding:10px 14px;text-align:left;font-size:12px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e2e8f0;'>Departamento</th>
        <th style='padding:10px 14px;text-align:left;font-size:12px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e2e8f0;'>Responsable</th>
      </tr></thead>
      <tbody>{spotRows}</tbody>
    </table>
  </td></tr>
  <tr><td style='background:#f8fafc;padding:14px 32px;text-align:center;border-top:1px solid #e2e8f0;'>
    <p style='margin:0;color:#94a3b8;font-size:12px;'>Notificación automática de Close Portal.</p>
  </td></tr>
</table></td></tr></table></body></html>";

            Send(subject: $"[Close Portal] Guardia iniciada — {startTime:dd/MM/yyyy HH:mm} hrs",
                 body: body, recipientList: string.Join(";", recipients), alertKey: "GuardStarted");
        }

        public static void NotifyGuardClosed(DateTime closedAt, string triggeredByEmail) {
            Send(
                subject: $"[Close Portal] Guardia cerrada — {closedAt:dd/MM/yyyy HH:mm} hrs",
                body: BuildTemplate("Guardia cerrada", "#6366f1", "&#128274;", new[] {
                    "<b>Todas las locaciones cerraron correctamente.</b>",
                    $"<b>Cierre registrado:</b> {closedAt:dd/MM/yyyy HH:mm} hrs",
                    $"<b>Disparado por:</b> {triggeredByEmail ?? "Sistema"}"
                }),
                recipientList: GetAlertRecipients("GuardClosed", "Administradores"),
                alertKey: "GuardClosed");
        }

        // ════════════════════════════════════════════════════════════════
        // GUARDIA — DRAFT PROGRAMADA
        // Destinatarios: grupo dinámico "Administradores" (Role_Id=3)
        // ════════════════════════════════════════════════════════════════
        public static void NotifyGuardDraft(int guardId, DateTime startTime, string createdByEmail) {
            Send(
                subject: $"[Close Portal] Guardia programada — {startTime:dd/MM/yyyy HH:mm} hrs",
                body: BuildTemplate("Guardia programada", "#f59e0b", "&#128197;", new[] {
                    "<b>Se ha programado una nueva guardia de cierre.</b>",
                    $"<b>Inicio programado:</b> {startTime:dd/MM/yyyy HH:mm} hrs",
                    $"<b>Creada por:</b> {createdByEmail ?? "Sistema"}",
                    $"<b>Fecha de notificación:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs",
                    "<span style='color:#64748b;font-size:13px;'>La guardia se encuentra en estado borrador. Aún puede cancelarse o modificarse.</span>"
                }),
                recipientList: GetAlertRecipients("GuardDraft", "Administradores"),
                alertKey: "GuardDraft");
        }

        // ════════════════════════════════════════════════════════════════
        // GUARDIA — DRAFT CANCELADA
        // Destinatarios: grupo dinámico "Administradores"
        // ════════════════════════════════════════════════════════════════
        public static void NotifyGuardDraftCancelled(DateTime? startTime, string cancelledByEmail) {
            string fechaStr = startTime.HasValue
                ? startTime.Value.ToString("dd/MM/yyyy HH:mm") + " hrs" : "—";
            Send(
                subject: $"[Close Portal] Guardia cancelada (draft) — {fechaStr}",
                body: BuildTemplate("Guardia draft cancelada", "#ef4444", "&#10006;", new[] {
                    "<b>La guardia programada en borrador ha sido cancelada.</b>",
                    $"<b>Inicio que estaba programado:</b> {fechaStr}",
                    $"<b>Cancelada por:</b> {cancelledByEmail ?? "Sistema"}",
                    $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }),
                recipientList: GetAlertRecipients("GuardDraftCancelled", "Administradores"),
                alertKey: "GuardDraftCancelled");
        }

        // ════════════════════════════════════════════════════════════════
        // GUARDIA — CONFIRMADA (creada)
        // Destinatarios: usuarios con locaciones involucradas + spots
        // ════════════════════════════════════════════════════════════════
        public static void NotifyGuardConfirmed(
                int guardId, DateTime? startTime,
                string confirmedByEmail,
                List<(string DeptCode, string DeptName, string Username, string Email)> spots) {

            string audience = new EmailDataAccess().GetGuardAudienceEmails(guardId);
            if (string.IsNullOrWhiteSpace(audience)) return;

            string fechaStr = startTime.HasValue
                ? startTime.Value.ToString("dd/MM/yyyy HH:mm") + " hrs" : "—";

            var spotRows = new System.Text.StringBuilder();
            foreach (var s in spots)
                spotRows.Append($@"
                <tr>
                  <td style='padding:8px 14px;border-bottom:1px solid #f1f5f9;font-size:13px;color:#334155;'>
                    <span style='display:inline-block;background:#fef3c7;color:#d97706;border:1px solid #fde68a;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700;margin-right:8px;'>{System.Web.HttpUtility.HtmlEncode(s.DeptCode)}</span>
                    {System.Web.HttpUtility.HtmlEncode(s.DeptName)}
                  </td>
                  <td style='padding:8px 14px;border-bottom:1px solid #f1f5f9;font-size:13px;color:#334155;font-weight:600;'>
                    {System.Web.HttpUtility.HtmlEncode(s.Username ?? s.Email ?? "Sin asignar")}
                  </td>
                </tr>");

            string spotsTable = spots.Count > 0
                ? $@"<table width='100%' cellpadding='0' cellspacing='0' style='border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;margin-top:16px;'>
                      <thead><tr style='background:#f8fafc;'>
                        <th style='padding:8px 14px;text-align:left;font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;border-bottom:1px solid #e2e8f0;'>Departamento</th>
                        <th style='padding:8px 14px;text-align:left;font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;border-bottom:1px solid #e2e8f0;'>Responsable</th>
                      </tr></thead>
                      <tbody>{spotRows}</tbody></table>"
                : "";

            string body = $@"<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f8fafc;font-family:sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0'><tr><td align='center' style='padding:40px 20px;'>
<table width='560' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
  <tr><td style='background:#10b981;padding:28px 32px;text-align:center;'>
    <div style='font-size:40px;color:#fff;margin-bottom:8px;'>&#128737;</div>
    <h1 style='margin:0;color:#fff;font-size:20px;font-weight:700;'>Guardia confirmada</h1>
    <p style='margin:6px 0 0;color:rgba(255,255,255,0.85);font-size:13px;'>Close Portal &mdash; Novamex</p>
  </td></tr>
  <tr><td style='padding:28px 32px;'>
    <p style='margin:0 0 8px;font-size:15px;color:#334155;'>La guardia ha sido <strong>confirmada y está activa</strong>.</p>
    <table width='100%' cellpadding='0' cellspacing='0'>
      <tr><td style='padding:8px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'><b>Inicio:</b> {fechaStr}</td></tr>
      <tr><td style='padding:8px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'><b>Confirmada por:</b> {System.Web.HttpUtility.HtmlEncode(confirmedByEmail ?? "Sistema")}</td></tr>
    </table>
    {spotsTable}
  </td></tr>
  <tr><td style='background:#f8fafc;padding:14px 32px;text-align:center;border-top:1px solid #e2e8f0;'>
    <p style='margin:0;color:#94a3b8;font-size:12px;'>Notificación automática de Close Portal.</p>
  </td></tr>
</table></td></tr></table></body></html>";

            Send(subject: $"[Close Portal] Guardia confirmada — {fechaStr}",
                 body: body, recipientList: audience, alertKey: "GuardConfirmed");
        }

        // ════════════════════════════════════════════════════════════════
        // GUARDIA — CANCELADA (estaba confirmada)
        // Destinatarios: misma audiencia que GuardConfirmed
        // ════════════════════════════════════════════════════════════════
        public static void NotifyGuardCancelled(
                DateTime? startTime, string cancelledByEmail, string audienceEmails) {

            if (string.IsNullOrWhiteSpace(audienceEmails)) return;

            string fechaStr = startTime.HasValue
                ? startTime.Value.ToString("dd/MM/yyyy HH:mm") + " hrs" : "—";

            Send(
                subject: $"[Close Portal] Guardia cancelada — {fechaStr}",
                body: BuildTemplate("Guardia cancelada", "#ef4444", "&#10006;", new[] {
                    "<b>La guardia activa ha sido cancelada.</b>",
                    $"<b>Inicio que estaba programado:</b> {fechaStr}",
                    $"<b>Cancelada por:</b> {cancelledByEmail ?? "Sistema"}",
                    $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }),
                recipientList: audienceEmails,
                alertKey: "GuardCancelled");
        }

        // ════════════════════════════════════════════════════════════════
        // SOLICITUD DE CIERRE — RESPUESTA
        // Destinatario: el Regular que hizo la solicitud
        // ════════════════════════════════════════════════════════════════
        public static void NotifyClosureResponse(
                string requesterEmail, string requesterName,
                string locationName, string status,
                string reviewNotes, string reviewedBy) {

            bool approved = status == "Approved";
            string color = approved ? "#10b981" : "#ef4444";
            string icon = approved ? "&#10003;" : "&#10006;";
            string label = approved ? "aprobada" : "rechazada";

            string notesLine = !string.IsNullOrWhiteSpace(reviewNotes)
                ? $"<b>Comentario del revisor:</b> {System.Web.HttpUtility.HtmlEncode(reviewNotes)}"
                : null;

            Send(
                subject: $"[Close Portal] Solicitud de cierre {label} — {locationName}",
                body: BuildTemplate($"Solicitud {label}", color, icon, new[] {
                    $"Hola <b>{System.Web.HttpUtility.HtmlEncode(requesterName)}</b>, tu solicitud de cierre fue <b>{label}</b>.",
                    $"<b>Locación:</b> {System.Web.HttpUtility.HtmlEncode(locationName)}",
                    $"<b>Estado:</b> {(approved ? "Aprobada ✓" : "Rechazada ✗")}",
                    $"<b>Revisado por:</b> {System.Web.HttpUtility.HtmlEncode(reviewedBy ?? "—")}",
                    $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs",
                    notesLine
                }),
                recipientList: requesterEmail,
                alertKey: "ClosureResponse");
        }

        // ════════════════════════════════════════════════════════════════
        // USUARIO BLOQUEADO
        // Destinatario: el propio usuario bloqueado
        // ════════════════════════════════════════════════════════════════
        public static void NotifyUserBlocked(
                string targetEmail, string targetUsername, string performedByEmail) {
            Send(
                subject: "[Close Portal] Tu cuenta ha sido bloqueada",
                body: BuildTemplate("Cuenta bloqueada", "#ef4444", "&#128274;", new[] {
                    $"Hola <b>{System.Web.HttpUtility.HtmlEncode(targetUsername ?? targetEmail)}</b>.",
                    "Tu cuenta de <b>Close Portal</b> ha sido bloqueada temporalmente.",
                    "Si crees que esto es un error, contacta al administrador del sistema.",
                    $"<b>Acción realizada por:</b> {System.Web.HttpUtility.HtmlEncode(performedByEmail ?? "Sistema")}",
                    $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }),
                recipientList: targetEmail,
                alertKey: "UserBlocked");
        }

        // ════════════════════════════════════════════════════════════════
        // USUARIO DESBLOQUEADO
        // Destinatario: el propio usuario desbloqueado
        // ════════════════════════════════════════════════════════════════
        public static void NotifyUserUnblocked(
                string targetEmail, string targetUsername, string performedByEmail) {
            Send(
                subject: "[Close Portal] Tu cuenta ha sido desbloqueada",
                body: BuildTemplate("Cuenta desbloqueada", "#10b981", "&#128275;", new[] {
                    $"Hola <b>{System.Web.HttpUtility.HtmlEncode(targetUsername ?? targetEmail)}</b>.",
                    "Tu cuenta de <b>Close Portal</b> ha sido desbloqueada. Ya puedes iniciar sesión.",
                    $"<b>Acción realizada por:</b> {System.Web.HttpUtility.HtmlEncode(performedByEmail ?? "Sistema")}",
                    $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }),
                recipientList: targetEmail,
                alertKey: "UserUnblocked");
        }

        // ════════════════════════════════════════════════════════════════
        // DEFAULT SPOT REMINDER
        // Se envía al confirmar la guardia si hay spots sin asignar.
        // Destinatario: grupo estático DefaultSpot{deptCode}
        // ════════════════════════════════════════════════════════════════
        public static void NotifyDefaultSpotReminders(int guardId) {
            var da = new EmailDataAccess();
            var unfilledDepts = da.GetUnfilledSpotDeptCodes(guardId);
            if (unfilledDepts == null || unfilledDepts.Count == 0) return;

            foreach (var deptCode in unfilledDepts) {
                string groupKey = $"DefaultSpot{deptCode.ToUpper()}";
                string recipients = ResolveGroup(groupKey);
                if (string.IsNullOrWhiteSpace(recipients)) continue;

                Send(
                    subject: $"[Close Portal] Spot {deptCode} sin asignar — guardia activa",
                    body: BuildTemplate($"Spot {deptCode} sin responsable", "#f59e0b", "&#9888;", new[] {
                        $"La guardia ha sido confirmada pero el spot de <b>{deptCode}</b> no tiene responsable asignado.",
                        "Por favor, accede a Close Portal y asigna un responsable para este departamento.",
                        $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                    }),
                    recipientList: recipients,
                    alertKey: "DefaultSpotReminder");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // GUARD DRAFT REMINDER — llamado por el timer de Startup.cs
        // Verifica guardias draft sin confirmar que superaron el threshold
        // ════════════════════════════════════════════════════════════════
        public static void CheckDraftReminders() {
            try {
                if (!IsAlertEnabled("GuardDraftReminder")) return;

                var da = new EmailDataAccess();
                int threshold = da.GetReminderThresholdMinutes(120);
                var drafts = da.GetDraftGuardsForReminder(threshold);

                foreach (var (guardId, createdAt) in drafts) {
                    string admins = da.GetDeptAdminEmails(guardId);
                    if (string.IsNullOrWhiteSpace(admins)) {
                        da.MarkReminderSent(guardId); // marcar igual para no re-intentar
                        continue;
                    }

                    int hoursElapsed = (int)Math.Round((DateTime.Now - createdAt).TotalHours);

                    Send(
                        subject: $"[Close Portal] Recordatorio: guardia sin confirmar ({hoursElapsed}h)",
                        body: BuildTemplate("Guardia sin confirmar", "#f59e0b", "&#9201;", new[] {
                            $"Existe una guardia en borrador que lleva <b>{hoursElapsed} hora{(hoursElapsed != 1 ? "s" : "")}</b> sin ser confirmada.",
                            $"<b>Programada el:</b> {createdAt:dd/MM/yyyy HH:mm} hrs",
                            "Por favor, ingresa a Close Portal y confirma la guardia o cancélala si ya no es necesaria.",
                            $"<b>Fecha de este recordatorio:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                        }),
                        recipientList: admins,
                        alertKey: "GuardDraftReminder");

                    da.MarkReminderSent(guardId);
                    Debug.WriteLine($"[EmailService.CheckDraftReminders] Reminder enviado para Guard {guardId}");
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailService.CheckDraftReminders] ERROR: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // SEND
        // ════════════════════════════════════════════════════════════════
        private static void Send(string subject, string body,
                                 string recipientList = null, string alertKey = null) {
            if (!_notificationsEnabled) {
                Debug.WriteLine($"[EmailService] Deshabilitado. Omitido: {subject}"); return;
            }
            if (alertKey != null && !IsAlertEnabled(alertKey)) {
                Debug.WriteLine($"[EmailService] Alerta '{alertKey}' deshabilitada. Omitido."); return;
            }

            string targets;
            if (_testMode && !string.IsNullOrWhiteSpace(_testRecipient)) {
                targets = _testRecipient;
                subject = "[TEST] " + subject;
            } else {
                targets = recipientList ?? ResolveGroup("CallGod");
            }

            if (string.IsNullOrWhiteSpace(targets)) {
                Debug.WriteLine("[EmailService] Sin destinatarios. Omitido."); return;
            }

            try {
                using (var mail = new MailMessage()) {
                    mail.From = new MailAddress(SmtpFrom, "Close Portal");
                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = true;
                    foreach (var addr in targets.Split(';')) {
                        var t = addr.Trim();
                        if (!string.IsNullOrEmpty(t)) mail.To.Add(t);
                    }
                    using (var smtp = new SmtpClient(SmtpHost, SmtpPort)) {
                        smtp.Credentials = new NetworkCredential(SmtpUser, SmtpPassword);
                        smtp.EnableSsl = SmtpSsl;
                        smtp.Send(mail);
                    }
                }
                Debug.WriteLine($"[EmailService] Enviado a: {targets}");
            } catch (Exception ex) {
                Debug.WriteLine($"[EmailService] ERROR: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // BUILDERS
        // ════════════════════════════════════════════════════════════════
        private static string BuildTemplate(string title, string color,
                                            string icon, string[] lines) {
            string rows = "";
            foreach (var l in lines) {
                if (string.IsNullOrEmpty(l)) continue;
                rows += $"<tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'>{l}</td></tr>";
            }
            return $@"<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f8fafc;font-family:sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0'><tr><td align='center' style='padding:40px 20px;'>
<table width='540' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
<tr><td style='background:{color};padding:28px 32px;text-align:center;'>
  <div style='font-size:40px;color:#fff;margin-bottom:8px;'>{icon}</div>
  <h1 style='margin:0;color:#fff;font-size:20px;font-weight:700;'>{title}</h1>
  <p style='margin:6px 0 0;color:rgba(255,255,255,0.85);font-size:13px;'>Close Portal &mdash; Novamex</p>
</td></tr>
<tr><td style='padding:28px 32px;'><table width='100%' cellpadding='0' cellspacing='0'>{rows}</table></td></tr>
<tr><td style='background:#f8fafc;padding:14px 32px;text-align:center;border-top:1px solid #e2e8f0;'>
  <p style='margin:0;color:#94a3b8;font-size:12px;'>Notificación automática de Close Portal.</p>
</td></tr>
</table></td></tr></table></body></html>";
        }

        private static string BuildClosureRequestTemplate(
                string managerName, string requesterName, string requesterEmail,
                string wmsCode, string wmsName, string notes, int requestId) {

            string notesRow = string.IsNullOrWhiteSpace(notes) ? "" :
                $"<tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'><b>Notas:</b> {System.Web.HttpUtility.HtmlEncode(notes)}</td></tr>";

            return $@"<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f8fafc;font-family:sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0'><tr><td align='center' style='padding:40px 20px;'>
<table width='540' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
<tr><td style='background:#6366f1;padding:28px 32px;text-align:center;'>
  <div style='font-size:40px;color:#fff;margin-bottom:8px;'>&#128274;</div>
  <h1 style='margin:0;color:#fff;font-size:20px;font-weight:700;'>Solicitud de cierre</h1>
  <p style='margin:6px 0 0;color:rgba(255,255,255,0.85);font-size:13px;'>Close Portal &mdash; Novamex</p>
</td></tr>
<tr><td style='padding:28px 32px;'>
  <p style='margin:0 0 20px;font-size:15px;color:#334155;'>Hola <strong>{System.Web.HttpUtility.HtmlEncode(managerName)}</strong>, tienes una nueva solicitud de cierre pendiente.</p>
  <table width='100%' cellpadding='0' cellspacing='0'>
    <tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'><b>Solicitud #:</b> {requestId}</td></tr>
    <tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'><b>Bodega:</b> {System.Web.HttpUtility.HtmlEncode(wmsCode)} — {System.Web.HttpUtility.HtmlEncode(wmsName)}</td></tr>
    <tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'><b>Solicitado por:</b> {System.Web.HttpUtility.HtmlEncode(requesterName)} ({System.Web.HttpUtility.HtmlEncode(requesterEmail)})</td></tr>
    {notesRow}
  </table>
</td></tr>
<tr><td style='background:#f8fafc;padding:14px 32px;text-align:center;border-top:1px solid #e2e8f0;'>
  <p style='margin:0;color:#94a3b8;font-size:12px;'>Solicitud #{requestId} — Close Portal.</p>
</td></tr>
</table></td></tr></table></body></html>";
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