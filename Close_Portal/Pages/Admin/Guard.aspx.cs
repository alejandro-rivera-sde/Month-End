using Close_Portal.Core;
using Close_Portal.DataAccess;
using Close_Portal.Services;
using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Web.Services;

namespace Close_Portal.Pages.Admin {
    public partial class Guard : SecurePage {

        // Solo Owners pueden acceder
        protected override int RequiredRoleId => RoleLevel.Owner;

        // ============================================================
        // PAGE LOAD
        // ============================================================
        protected void Page_Load(object sender, EventArgs e) {
            // Nada que hacer en Page_Load: la UI la carga guard.js via WebMethods
        }

        // ============================================================
        // GET OWNERS — lista de todos los Owners activos
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetOwners() {
            try {
                SecurePage.CheckAccess(RoleLevel.Owner);

                var da = new GuardDataAccess();
                var owners = da.GetOwners();

                var result = new List<object>();
                foreach (var o in owners) {
                    result.Add(new {
                        userId = o.UserId,
                        username = o.Username,
                        email = o.Email,
                        initials = o.Initials,
                        hasActiveGuard = o.HasActiveGuard
                    });
                }

                return new { success = true, data = result };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetOwners] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET SCHEDULE — turnos activos y futuros
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetSchedule() {
            try {
                SecurePage.CheckAccess(RoleLevel.Owner);

                var da = new GuardDataAccess();
                var schedule = da.GetSchedule();

                var result = new List<object>();
                foreach (var s in schedule) {
                    result.Add(new {
                        guardId = s.GuardId,
                        userId = s.UserId,
                        username = s.Username,
                        email = s.Email,
                        initials = s.Initials,
                        startTimeIso = s.StartTimeIso,
                        endTimeIso = s.EndTimeIso,
                        startTimeFmt = s.StartTimeFmt,
                        endTimeFmt = s.EndTimeFmt,
                        assignedBy = s.AssignedBy,
                        isActive = s.IsActive
                    });
                }

                return new { success = true, data = result };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetSchedule] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // ASSIGN GUARD — crea un turno y notifica al Owner
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object AssignGuard(int userId, string startTime, string endTime) {
            try {
                SecurePage.CheckAccess(RoleLevel.Owner);

                // Parsear fechas desde el formato ISO del input datetime-local
                if (!DateTime.TryParse(startTime, out DateTime start))
                    return new { success = false, message = "Fecha de inicio inválida." };

                if (!DateTime.TryParse(endTime, out DateTime end))
                    return new { success = false, message = "Fecha de fin inválida." };

                var session = System.Web.HttpContext.Current.Session;
                int assignedBy = (int)session["UserId"];
                string assignedByEmail = session["Email"]?.ToString();

                var da = new GuardDataAccess();
                var result = da.AssignGuard(userId, start, end, assignedBy);

                if (!result.Success)
                    return new { success = false, message = result.Message };

                // Obtener datos del Owner para el correo
                var owners = da.GetOwners();
                var owner = owners.Find(o => o.UserId == userId);

                if (owner != null) {
                    // Enviar correo de confirmación al Owner asignado (fire-and-forget)
                    System.Threading.Tasks.Task.Run(() => {
                        EmailService.NotifyGuardAssigned(
                            ownerEmail: owner.Email,
                            ownerUsername: owner.Username,
                            startTime: start,
                            endTime: end,
                            assignedByEmail: assignedByEmail
                        );
                    });
                }

                System.Diagnostics.Debug.WriteLine($"[AssignGuard] Turno asignado Guard_Id={result.GuardId} ✓");
                return new { success = true, guardId = result.GuardId };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[AssignGuard] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // REMOVE GUARD — elimina un turno
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object RemoveGuard(int guardId) {
            try {
                SecurePage.CheckAccess(RoleLevel.Owner);

                var da = new GuardDataAccess();
                var result = da.RemoveGuard(guardId);

                return result.Success
                    ? (object)new { success = true }
                    : new { success = false, message = result.Message };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[RemoveGuard] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }
    }
}
