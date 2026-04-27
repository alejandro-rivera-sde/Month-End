using Close_Portal.Core;
using Close_Portal.Services;
using System;
using System.Web;
using System.Web.Services;
using System.Web.SessionState;

namespace Close_Portal.Pages.IT {
    public partial class Processes : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Owner;

        protected void Page_Load(object sender, EventArgs e) { }

        private static bool TryGetOwner(out HttpSessionState session) {
            session = HttpContext.Current.Session;
            if (session["UserId"] == null) return false;
            int roleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
            return roleId >= RoleLevel.Owner;
        }

        // ============================================================
        // GET CONFIG
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetProcessesConfig() {
            try {
                if (!TryGetOwner(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                return new {
                    success                    = true,
                    confirmacionCierreEnabled   = ProcessesService.ConfirmacionCierreEnabled,
                    confirmacionCierreRecipient = ProcessesService.ConfirmacionCierreRecipient
                };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SET PROCESS ENABLED
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SetProcessEnabled(string processKey, bool enabled) {
            try {
                if (!TryGetOwner(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                if (processKey == "ConfirmacionCierre") {
                    ProcessesService.SetConfirmacionCierreEnabled(enabled);
                    return new { success = true };
                }

                return new { success = false, message = "Proceso no reconocido." };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SET PROCESS RECIPIENT
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SetProcessRecipient(string processKey, string recipient) {
            try {
                if (!TryGetOwner(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                if (processKey == "ConfirmacionCierre") {
                    ProcessesService.SetConfirmacionCierreRecipient(recipient);
                    return new { success = true };
                }

                return new { success = false, message = "Proceso no reconocido." };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // TEST PROCESS — fuerza ejecución con fecha actual
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object TestProcess(string processKey) {
            try {
                if (!TryGetOwner(out _))
                    return new { success = false, message = "Acceso no autorizado." };

                if (processKey == "ConfirmacionCierre") {
                    if (string.IsNullOrWhiteSpace(ProcessesService.ConfirmacionCierreRecipient))
                        return new { success = false, message = "Configura un correo destinatario antes de probar." };

                    ProcessesService.ForceConfirmacionCierre(DateTime.Now);
                    return new { success = true };
                }

                return new { success = false, message = "Proceso no reconocido." };
            } catch (Exception ex) {
                return new { success = false, message = ex.Message };
            }
        }
    }
}
