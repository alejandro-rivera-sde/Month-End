using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using Close_Portal.DataAccess;

namespace Close_Portal.Services {
    public static class EmailService {

        // ── Deshabilitar todas las notificaciones temporalmente ──────────
        // Cambiar a true para reactivar el envío de correos.
        private static readonly bool NOTIFICATIONS_ENABLED = false;

        private static readonly string SmtpHost = ConfigurationManager.AppSettings["Smtp_Host"];
        private static readonly int SmtpPort = int.Parse(ConfigurationManager.AppSettings["Smtp_Port"] ?? "587");
        private static readonly string SmtpUser = ConfigurationManager.AppSettings["Smtp_User"];
        private static readonly string SmtpPassword = ConfigurationManager.AppSettings["Smtp_Password"];
        private static readonly string SmtpFrom = ConfigurationManager.AppSettings["Smtp_From"];
        private static readonly bool SmtpSsl = bool.Parse(ConfigurationManager.AppSettings["Smtp_EnableSsl"] ?? "true");
        private static readonly string AdminEmails = ConfigurationManager.AppSettings["Call_God"];
        private static readonly string TeamIT = ConfigurationManager.AppSettings["Notify_TeamIT"];

        // ════════════════════════════════════════════════════════════════
        // RESOLUCIÓN DE GUARDIAS ACTIVOS
        // Devuelve los emails de Owners en guardia ahora mismo.
        // Si no hay nadie, hace fallback a Call_God (AdminEmails).
        // ════════════════════════════════════════════════════════════════
        private static string ResolveGuardRecipients() {
            var active = GuardDataAccess.GetActiveGuardEmails();
            if (!string.IsNullOrWhiteSpace(active)) {
                Debug.WriteLine($"[EmailService] Destinatarios → Guardias activos: {active}");
                return active;
            }
            Debug.WriteLine("[EmailService] Sin guardias activos → fallback a Call_God");
            return AdminEmails;
        }

        // ════════════════════════════════════════════════════════════════
        // NOTIFICACIONES DE USUARIOS (ahora van a guardias activos)
        // ════════════════════════════════════════════════════════════════

        public static void NotifyUserAdded(string targetEmail, string targetUsername,
                                           string targetRole, string performedByEmail) {
            var roleInfo = targetRole != null ? $"<b>Rol asignado:</b> {targetRole}" : null;
            Send(
                subject: $"[Close Portal] Usuario agregado: {targetUsername ?? targetEmail}",
                body: BuildTemplate("Nuevo usuario agregado", "#10b981", "&#10010;", new[] {
                    $"<b>Usuario:</b> {targetUsername ?? "(sin nombre)"}",
                    $"<b>Email:</b> {targetEmail}",
                    roleInfo,
                    $"<b>Realizado por:</b> {performedByEmail}",
                    $"<b>Fecha:</b> {DateTime.Now:dd/MM/yyyy HH:mm} hrs"
                }),
                recipientList: ResolveGuardRecipients()
            );
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
                recipientList: ResolveGuardRecipients()
            );
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
                recipientList: MergeGroups(TeamIT, ResolveGuardRecipients())
            );
        }

        // ════════════════════════════════════════════════════════════════
        // NOTIFICACIÓN DE SOLICITUD DE CIERRE
        // Se envía al Manager cuando un Regular solicita cierre de bodega
        // ════════════════════════════════════════════════════════════════
        public static void NotifyClosureRequest(
                string managerEmail, string managerName,
                string requesterName, string requesterEmail,
                string wmsCode, string wmsName,
                string notes, int requestId) {
            Send(
                subject: $"[Close Portal] Solicitud de cierre — {wmsCode}",
                body: BuildClosureRequestTemplate(
                    managerName, requesterName, requesterEmail,
                    wmsCode, wmsName, notes, requestId),
                recipientList: managerEmail
            );
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
  <p style='margin:0 0 20px;font-size:15px;color:#334155;'>
    Hola <strong>{System.Web.HttpUtility.HtmlEncode(managerName)}</strong>, tienes una nueva solicitud de cierre pendiente de revisión.
  </p>
  <table width='100%' cellpadding='0' cellspacing='0'>
    <tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'><b>Solicitud #:</b> {requestId}</td></tr>
    <tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'><b>Bodega:</b> {System.Web.HttpUtility.HtmlEncode(wmsCode)} — {System.Web.HttpUtility.HtmlEncode(wmsName)}</td></tr>
    <tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'><b>Solicitado por:</b> {System.Web.HttpUtility.HtmlEncode(requesterName)} ({System.Web.HttpUtility.HtmlEncode(requesterEmail)})</td></tr>
    {notesRow}
  </table>
  <p style='margin:24px 0 0;font-size:13px;color:#64748b;'>Ingresa a Close Portal para revisar y aprobar o rechazar la solicitud.</p>
</td></tr>
<tr><td style='background:#f8fafc;padding:14px 32px;text-align:center;border-top:1px solid #e2e8f0;'>
  <p style='margin:0;color:#94a3b8;font-size:12px;'>Notificación automática de Close Portal. Solicitud #{requestId}.</p>
</td></tr>
</table></td></tr></table></body></html>";
        }

        // ════════════════════════════════════════════════════════════════
        // NOTIFICACIÓN DE INICIO DE GUARDIA
        // Se envía a todos los responsables de los spots + guardias activos
        // cuando se declara el inicio formal de la guardia.
        // spots: lista de (DeptCode, DeptName, Username, Email)
        // ════════════════════════════════════════════════════════════════

        public static void NotifyGuardStarted(
                DateTime startTime,
                string startedByEmail,
                System.Collections.Generic.List<(string DeptCode, string DeptName, string Username, string Email)> spots) {

            // Destinatarios: todos los responsables de spots + guardias activos
            var recipients = new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var s in spots)
                if (!string.IsNullOrWhiteSpace(s.Email))
                    recipients.Add(s.Email.Trim());

            string activeGuards = GuardDataAccess.GetActiveGuardEmails();
            if (!string.IsNullOrWhiteSpace(activeGuards))
                foreach (var addr in activeGuards.Split(';'))
                    if (!string.IsNullOrWhiteSpace(addr)) recipients.Add(addr.Trim());

            if (recipients.Count == 0) return;

            // Construir filas de spots para el template
            var spotRows = new System.Text.StringBuilder();
            foreach (var s in spots) {
                spotRows.Append($@"
                <tr>
                  <td style='padding:10px 14px;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'>
                    <span style='display:inline-block;background:#fef3c7;color:#d97706;border:1px solid #fde68a;
                                 padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700;
                                 margin-right:10px;vertical-align:middle;'>{System.Web.HttpUtility.HtmlEncode(s.DeptCode)}</span>
                    {System.Web.HttpUtility.HtmlEncode(s.DeptName)}
                  </td>
                  <td style='padding:10px 14px;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;font-weight:600;'>
                    {System.Web.HttpUtility.HtmlEncode(s.Username ?? s.Email)}
                  </td>
                </tr>");
            }

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
    <p style='margin:0 0 20px;font-size:15px;color:#334155;'>
      La guardia fue declarada iniciada el <strong>{startTime:dd/MM/yyyy}</strong>
      a las <strong>{startTime:HH:mm} hrs</strong> por <strong>{System.Web.HttpUtility.HtmlEncode(startedByEmail)}</strong>.
    </p>
    <p style='margin:0 0 12px;font-size:13px;font-weight:700;color:#64748b;
              text-transform:uppercase;letter-spacing:0.5px;'>Responsables asignados</p>
    <table width='100%' cellpadding='0' cellspacing='0'
           style='border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;'>
      <thead>
        <tr style='background:#f8fafc;'>
          <th style='padding:10px 14px;text-align:left;font-size:12px;color:#64748b;
                     font-weight:700;text-transform:uppercase;letter-spacing:0.5px;
                     border-bottom:1px solid #e2e8f0;'>Departamento</th>
          <th style='padding:10px 14px;text-align:left;font-size:12px;color:#64748b;
                     font-weight:700;text-transform:uppercase;letter-spacing:0.5px;
                     border-bottom:1px solid #e2e8f0;'>Responsable</th>
        </tr>
      </thead>
      <tbody>{spotRows}</tbody>
    </table>
  </td></tr>

  <tr><td style='background:#f8fafc;padding:14px 32px;text-align:center;border-top:1px solid #e2e8f0;'>
    <p style='margin:0;color:#94a3b8;font-size:12px;'>Notificación automática de Close Portal.</p>
  </td></tr>

</table></td></tr></table></body></html>";

            Send(
                subject: $"[Close Portal] Guardia iniciada — {startTime:dd/MM/yyyy HH:mm} hrs",
                body: body,
                recipientList: string.Join(";", recipients)
            );
        }

        // ════════════════════════════════════════════════════════════════
        // NOTIFICACIÓN DE CIERRE DE GUARDIA
        // Se envía a guardias activos (o Call_God) cuando el sistema
        // cierra automáticamente la guardia al completarse todas las locaciones.
        // ════════════════════════════════════════════════════════════════

        public static void NotifyGuardClosed(DateTime closedAt, string triggeredByEmail) {
            Send(
                subject: $"[Close Portal] Guardia cerrada — {closedAt:dd/MM/yyyy HH:mm} hrs",
                body: BuildTemplate("Guardia cerrada automáticamente", "#6366f1", "&#128274;", new[] {
                    "<b>Todas las locaciones cerraron correctamente.</b>",
                    $"<b>Cierre registrado:</b> {closedAt:dd/MM/yyyy HH:mm} hrs",
                    $"<b>Disparado por:</b> {triggeredByEmail ?? "Sistema"}"
                }),
                recipientList: ResolveGuardRecipients()
            );
        }

        // ════════════════════════════════════════════════════════════════
        // BUILDER Y SENDER
        // ════════════════════════════════════════════════════════════════

        private static string BuildTemplate(string title, string color, string icon, string[] lines) {
            string rows = "";
            foreach (var line in lines) {
                if (string.IsNullOrEmpty(line)) continue;
                rows += $"<tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'>{line}</td></tr>";
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

        private static void Send(string subject, string body, string recipientList = null) {
            if (!NOTIFICATIONS_ENABLED) {
                Debug.WriteLine($"[EmailService] Notificaciones deshabilitadas. Omitido: {subject}");
                return;
            }
            var targets = recipientList ?? AdminEmails;

            if (string.IsNullOrWhiteSpace(targets)) {
                Debug.WriteLine("[EmailService] Sin destinatarios. Omitido.");
                return;
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