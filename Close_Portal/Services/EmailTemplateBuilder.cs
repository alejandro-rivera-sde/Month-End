using System.Collections.Generic;
using System.Text;

namespace Close_Portal.Services {

    // Ensambla la estructura HTML de los correos.
    // No conoce conceptos de negocio: recibe título, estilo y contenido ya formateado.
    public static class EmailTemplateBuilder {

        // ── API pública ───────────────────────────────────────────────────

        // Plantilla estándar: cabecera coloreada + lista de filas de datos.
        public static string Simple(string title, EmailAlertStyle style, string[] lines) {
            string rows = DataRows(lines);
            string body = $"<tr><td style='padding:28px 32px;'>" +
                          $"<table width='100%' cellpadding='0' cellspacing='0'>{rows}</table>" +
                          $"</td></tr>";
            return Layout(Header(title, style), body);
        }

        // Plantilla con tabla de spots: párrafo introductorio + tabla de dos columnas.
        public static string WithSpotsTable(
                string title, EmailAlertStyle style, string introParagraph,
                IList<(string DeptCode, string DeptName, string Username, string Email)> spots) {

            string spotsHtml = $@"<table width='100%' cellpadding='0' cellspacing='0' style='border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;'>
  <thead><tr style='background:#f8fafc;'>
    <th style='padding:10px 14px;text-align:left;font-size:12px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e2e8f0;'>Departamento</th>
    <th style='padding:10px 14px;text-align:left;font-size:12px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #e2e8f0;'>Responsable</th>
  </tr></thead>
  <tbody>{SpotRows(spots)}</tbody>
</table>";

            string body = $@"<tr><td style='padding:28px 32px;'>
  <p style='margin:0 0 20px;font-size:15px;color:#334155;'>{introParagraph}</p>
  {spotsHtml}
</td></tr>";

            return Layout(Header(title, style), body);
        }

        // Plantilla de guardia confirmada: párrafo + filas de info + tabla de spots opcional.
        public static string WithInfoAndOptionalSpotsTable(
                string title, EmailAlertStyle style,
                string introParagraph, string[] infoLines,
                IList<(string DeptCode, string DeptName, string Username, string Email)> spots) {

            string infoRows = DataRows(infoLines);

            string spotsSection = spots != null && spots.Count > 0
                ? $@"<table width='100%' cellpadding='0' cellspacing='0' style='border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;margin-top:16px;'>
  <thead><tr style='background:#f8fafc;'>
    <th style='padding:8px 14px;text-align:left;font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;border-bottom:1px solid #e2e8f0;'>Departamento</th>
    <th style='padding:8px 14px;text-align:left;font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;border-bottom:1px solid #e2e8f0;'>Responsable</th>
  </tr></thead>
  <tbody>{SpotRows(spots, compact: true)}</tbody>
</table>"
                : "";

            string body = $@"<tr><td style='padding:28px 32px;'>
  <p style='margin:0 0 8px;font-size:15px;color:#334155;'>{introParagraph}</p>
  <table width='100%' cellpadding='0' cellspacing='0'>{infoRows}</table>
  {spotsSection}
</td></tr>";

            return Layout(Header(title, style), body);
        }

        // Plantilla de solicitud de cierre: párrafo dirigido al manager + filas de datos + pie personalizado.
        public static string ClosureRequest(
                string title, EmailAlertStyle style,
                string introParagraph, string[] dataLines,
                string footerText) {

            string rows = DataRows(dataLines);
            string body = $@"<tr><td style='padding:28px 32px;'>
  <p style='margin:0 0 20px;font-size:15px;color:#334155;'>{introParagraph}</p>
  <table width='100%' cellpadding='0' cellspacing='0'>{rows}</table>
</td></tr>";

            return Layout(Header(title, style), body, footerText);
        }

        // ── Helpers privados ──────────────────────────────────────────────

        private static string Header(string title, EmailAlertStyle style) =>
            $@"<tr><td style='background:{style.Color};padding:28px 32px;text-align:center;'>
  <div style='font-size:40px;color:#fff;margin-bottom:8px;'>{style.Icon}</div>
  <h1 style='margin:0;color:#fff;font-size:20px;font-weight:700;'>{title}</h1>
  <p style='margin:6px 0 0;color:rgba(255,255,255,0.85);font-size:13px;'>Close Portal &mdash; Novamex</p>
</td></tr>";

        private static string Layout(string headerRow, string bodyRow,
                                     string footerText = "Notificación automática de Close Portal.") =>
            $@"<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f8fafc;font-family:sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0'><tr><td align='center' style='padding:40px 20px;'>
<table width='560' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
{headerRow}
{bodyRow}
<tr><td style='background:#f8fafc;padding:14px 32px;text-align:center;border-top:1px solid #e2e8f0;'>
  <p style='margin:0;color:#94a3b8;font-size:12px;'>{footerText}</p>
</td></tr>
</table></td></tr></table></body></html>";

        private static string DataRows(string[] lines) {
            if (lines == null) return "";
            var sb = new StringBuilder();
            foreach (var l in lines) {
                if (string.IsNullOrEmpty(l)) continue;
                sb.Append($"<tr><td style='padding:10px 0;border-bottom:1px solid #f1f5f9;font-size:14px;color:#334155;'>{l}</td></tr>");
            }
            return sb.ToString();
        }

        private static string SpotRows(
                IEnumerable<(string DeptCode, string DeptName, string Username, string Email)> spots,
                bool compact = false) {

            string p  = compact ? "8px 14px" : "10px 14px";
            string fs = compact ? "13px"      : "14px";
            var sb = new StringBuilder();
            foreach (var s in spots)
                sb.Append($@"
<tr>
  <td style='padding:{p};border-bottom:1px solid #f1f5f9;font-size:{fs};color:#334155;'>
    <span style='display:inline-block;background:#fef3c7;color:#d97706;border:1px solid #fde68a;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:700;margin-right:10px;vertical-align:middle;'>{System.Web.HttpUtility.HtmlEncode(s.DeptCode)}</span>
    {System.Web.HttpUtility.HtmlEncode(s.DeptName)}
  </td>
  <td style='padding:{p};border-bottom:1px solid #f1f5f9;font-size:{fs};color:#334155;font-weight:600;'>
    {System.Web.HttpUtility.HtmlEncode(s.Username ?? s.Email ?? "Sin asignar")}
  </td>
</tr>");
            return sb.ToString();
        }
    }
}
