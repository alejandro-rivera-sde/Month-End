using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Hosting;

namespace Close_Portal.Services {

    // Carga plantillas HTML desde App_Data/EmailTemplates/ y reemplaza tokens.
    // No contiene ninguna cadena HTML; todo el marcado vive en los archivos .html.
    public static class EmailTemplateBuilder {

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, string> _cache =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        // ── API pública ───────────────────────────────────────────────────

        public static string Simple(string title, EmailAlertStyle style, string[] lines,
                                    string lang = "es") =>
            Load("simple")
                .Replace("{{COLOR}}",          style.Color)
                .Replace("{{TITLE}}",          title)
                .Replace("{{DATA_ROWS}}",      DataRows(lines))
                .Replace("{{FOOTER_TEXT}}",    EmailStrings.FooterAuto(lang))
                .Replace("{{FOOTER_NOREPLY}}", EmailStrings.FooterNoReply(lang));

        public static string WithSpotsTable(
                string title, EmailAlertStyle style, string introParagraph,
                IList<(string DeptCode, string DeptName, string Username, string Email)> spots,
                string lang = "es") =>
            Load("withspots")
                .Replace("{{COLOR}}",          style.Color)
                .Replace("{{TITLE}}",          title)
                .Replace("{{INTRO}}",          introParagraph)
                .Replace("{{TH_DEPT}}",        lang == "en" ? "Department" : "Departamento")
                .Replace("{{TH_RESPONSIBLE}}", lang == "en" ? "Responsible" : "Responsable")
                .Replace("{{SPOT_ROWS}}",      SpotRows(spots, compact: false, lang: lang))
                .Replace("{{FOOTER_TEXT}}",    EmailStrings.FooterAuto(lang))
                .Replace("{{FOOTER_NOREPLY}}", EmailStrings.FooterNoReply(lang));

        public static string WithInfoAndOptionalSpotsTable(
                string title, EmailAlertStyle style,
                string introParagraph, string[] infoLines,
                IList<(string DeptCode, string DeptName, string Username, string Email)> spots,
                string lang = "es") {

            string spotsSection = spots != null && spots.Count > 0
                ? Load("_spotstable_compact")
                    .Replace("{{TH_DEPT}}",        lang == "en" ? "Department" : "Departamento")
                    .Replace("{{TH_RESPONSIBLE}}", lang == "en" ? "Responsible" : "Responsable")
                    .Replace("{{SPOT_ROWS}}",      SpotRows(spots, compact: true, lang: lang))
                : "";

            return Load("infospots")
                .Replace("{{COLOR}}",          style.Color)
                .Replace("{{TITLE}}",          title)
                .Replace("{{INTRO}}",          introParagraph)
                .Replace("{{INFO_ROWS}}",      DataRows(infoLines))
                .Replace("{{SPOTS_SECTION}}", spotsSection)
                .Replace("{{FOOTER_TEXT}}",    EmailStrings.FooterAuto(lang))
                .Replace("{{FOOTER_NOREPLY}}", EmailStrings.FooterNoReply(lang));
        }

        public static string ClosureRequest(
                string title, EmailAlertStyle style,
                string introParagraph, string[] dataLines,
                string footerText, string lang = "es") =>
            Load("closurerequest")
                .Replace("{{COLOR}}",          style.Color)
                .Replace("{{TITLE}}",          title)
                .Replace("{{INTRO}}",          introParagraph)
                .Replace("{{DATA_ROWS}}",      DataRows(dataLines))
                .Replace("{{FOOTER_TEXT}}",    footerText)
                .Replace("{{FOOTER_NOREPLY}}", EmailStrings.FooterNoReply(lang));

        // ── Helpers privados ──────────────────────────────────────────────

        private static string DataRows(string[] lines) {
            if (lines == null) return "";
            string tpl = Load("_datarow");
            var sb = new StringBuilder();
            foreach (var l in lines)
                if (!string.IsNullOrEmpty(l))
                    sb.Append(tpl.Replace("{{CONTENT}}", l));
            return sb.ToString();
        }

        private static string SpotRows(
                IEnumerable<(string DeptCode, string DeptName, string Username, string Email)> spots,
                bool compact, string lang = "es") {
            string tpl = Load(compact ? "_spotrow_compact" : "_spotrow");
            var sb = new StringBuilder();
            foreach (var s in spots)
                sb.Append(tpl
                    .Replace("{{DEPT_CODE}}", System.Web.HttpUtility.HtmlEncode(s.DeptCode))
                    .Replace("{{DEPT_NAME}}", System.Web.HttpUtility.HtmlEncode(s.DeptName))
                    .Replace("{{USERNAME}}",  System.Web.HttpUtility.HtmlEncode(
                        s.Username ?? s.Email ?? EmailStrings.Unassigned(lang))));
            return sb.ToString();
        }

        private static string Load(string name) {
            lock (_lock) {
                if (_cache.TryGetValue(name, out string cached)) return cached;
            }
            string path = Path.Combine(
                HostingEnvironment.MapPath("~/App_Data/EmailTemplates/"), name + ".html");
            string content = File.ReadAllText(path, Encoding.UTF8);
            lock (_lock) { _cache[name] = content; }
            return content;
        }
    }
}
