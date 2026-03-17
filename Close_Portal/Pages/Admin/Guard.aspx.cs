using Close_Portal.Core;
using Close_Portal.DataAccess;
using Close_Portal.Services;
using System;
using System.Collections.Generic;
using System.Web.Services;
using System.Web.UI;

namespace Close_Portal.Pages.Admin {
    public partial class Guard : SecurePage {

        // Owners y Administradores pueden acceder
        protected override int RequiredRoleId => RoleLevel.Administrador;

        protected void Page_Load(object sender, EventArgs e) {
            // UI cargada completamente por guard.js via WebMethods
        }

        // ============================================================
        // GET DEPARTMENTS — catálogo de departamentos activos
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetDepartments() {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);
                var da = new GuardDataAccess();
                var depts = da.GetDepartments();

                var result = new List<object>();
                foreach (var d in depts) {
                    result.Add(new {
                        departmentId = d.DepartmentId,
                        departmentCode = d.DepartmentCode,
                        departmentName = d.DepartmentName
                    });
                }
                return new { success = true, data = result };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetDepartments] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET USERS BY DEPARTMENT — usuarios disponibles para un spot
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetUsersByDepartment(int departmentId) {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);
                var da = new GuardDataAccess();
                var users = da.GetUsersByDepartment(departmentId);

                var result = new List<object>();
                foreach (var u in users) {
                    result.Add(new {
                        userId = u.UserId,
                        username = u.Username,
                        email = u.Email,
                        initials = u.Initials
                    });
                }
                return new { success = true, data = result };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetUsersByDepartment] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET GUARD STATUS — guardia actual (activa o pendiente)
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetGuardStatus() {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);
                var da = new GuardDataAccess();
                var guard = da.GetCurrentGuard();

                if (guard == null)
                    return new { success = true, guard = (object)null };

                return new {
                    success = true,
                    guard = BuildGuardDto(guard)
                };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetGuardStatus] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET GUARD HISTORY — últimas guardias finalizadas
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetGuardHistory() {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);
                var da = new GuardDataAccess();
                var history = da.GetGuardHistory(20);

                var result = new List<object>();
                foreach (var g in history)
                    result.Add(BuildGuardDto(g));

                return new { success = true, data = result };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetGuardHistory] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // CREATE GUARD — crea guardia + spots vacíos por departamento
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object CreateGuard(string startTime) {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);

                if (!DateTime.TryParse(startTime, out DateTime start))
                    return new { success = false, message = "Fecha de inicio inválida." };

                int createdBy = (int)System.Web.HttpContext.Current.Session["UserId"];

                var da = new GuardDataAccess();
                var result = da.CreateGuard(createdBy, start);

                if (!result.Success)
                    return new { success = false, message = result.Message };

                System.Diagnostics.Debug.WriteLine($"[CreateGuard] Guard_Id={result.GuardId} ✓");
                return new { success = true, guardId = result.GuardId };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[CreateGuard] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // ASSIGN SPOT — asigna un usuario a un spot de departamento
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object AssignSpot(int spotId, int userId) {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);

                int assignedBy = (int)System.Web.HttpContext.Current.Session["UserId"];

                var da = new GuardDataAccess();
                var result = da.AssignSpot(spotId, userId, assignedBy);

                return result.Success
                    ? (object)new { success = true }
                    : new { success = false, message = result.Message };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[AssignSpot] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // UNASSIGN SPOT — limpia un spot para reasignar
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object UnassignSpot(int spotId) {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);

                var da = new GuardDataAccess();
                var result = da.UnassignSpot(spotId);

                return result.Success
                    ? (object)new { success = true }
                    : new { success = false, message = result.Message };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[UnassignSpot] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // REMOVE GUARD — elimina guardia no cerrada
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object RemoveGuard(int guardId) {
            try {
                SecurePage.CheckAccess(RoleLevel.Administrador);

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

        // ════════════════════════════════════════════════════════════════
        // DTO HELPER — serializa GuardViewModel a objeto anónimo para JS
        // ════════════════════════════════════════════════════════════════
        private static object BuildGuardDto(GuardViewModel g) {
            var spots = new List<object>();
            foreach (var s in g.Spots) {
                spots.Add(new {
                    spotId = s.SpotId,
                    guardId = s.GuardId,
                    departmentId = s.DepartmentId,
                    departmentCode = s.DepartmentCode,
                    departmentName = s.DepartmentName,
                    userId = s.UserId,
                    username = s.Username,
                    email = s.Email,
                    initials = s.Initials,
                    assignedBy = s.AssignedBy,
                    assignedAtFmt = s.AssignedAt.HasValue
                        ? s.AssignedAt.Value.ToString("dd/MM/yyyy HH:mm")
                        : null,
                    isFilled = s.IsFilled
                });
            }

            return new {
                guardId = g.GuardId,
                startTime = g.StartTimeIso,
                endTime = g.EndTimeIso,
                startTimeFmt = g.StartTimeFmt,
                endTimeFmt = g.EndTimeFmt,
                createdBy = g.CreatedBy,
                createdAtFmt = g.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                allSpotsFilled = g.AllSpotsFilled,
                isStarted = g.IsStarted,
                isFinished = g.IsFinished,
                spots
            };
        }
    }
}