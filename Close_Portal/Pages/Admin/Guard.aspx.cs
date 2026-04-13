using Close_Portal.Core;
using Close_Portal.DataAccess;
using Close_Portal.Services;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Services;
using System.Web.SessionState;
using System.Web.UI;

namespace Close_Portal.Pages.Admin {
    public partial class Guard : SecurePage {

        protected override int RequiredRoleId => RoleLevel.Administrador;

        protected void Page_Load(object sender, EventArgs e) {
            // UI cargada completamente por guard.js via WebMethods
        }

        // ════════════════════════════════════════════════════════════════
        // HELPER — validación de sesión sin lanzar excepción
        // ════════════════════════════════════════════════════════════════
        private static bool TryGetSession(int requiredRole, out HttpSessionState session,
                                          out int userId, out int roleId) {
            session = HttpContext.Current.Session;
            userId = 0;
            roleId = -1;
            if (session["UserId"] == null) return false;
            userId = (int)session["UserId"];
            roleId = session["RoleId"] != null ? (int)session["RoleId"] : -1;
            return roleId >= requiredRole;
        }

        // ============================================================
        // GET DEPARTMENTS
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetDepartments() {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var da = new GuardDataAccess();
                var depts = da.GetDepartments();
                var result = new List<object>();
                foreach (var d in depts)
                    result.Add(new {
                        departmentId = d.DepartmentId,
                        departmentCode = d.DepartmentCode,
                        departmentName = d.DepartmentName
                    });

                return new { success = true, data = result };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetDepartments] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET USERS BY DEPARTMENT
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetUsersByDepartment(int departmentId) {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var da = new GuardDataAccess();
                var users = da.GetUsersByDepartment(departmentId);
                var result = new List<object>();
                foreach (var u in users)
                    result.Add(new {
                        userId = u.UserId,
                        username = u.Username,
                        email = u.Email,
                        initials = u.Initials
                    });

                return new { success = true, data = result };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetUsersByDepartment] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET GUARD STATUS
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetGuardStatus() {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out int currentUserId, out int currentRoleId))
                    return new { success = false, message = "Acceso no autorizado." };

                bool isOwner = currentRoleId >= RoleLevel.Owner;

                var da = new GuardDataAccess();
                int myDepartmentId = isOwner ? -1 : da.GetUserDepartmentId(currentUserId);
                string myDeptCode = isOwner ? "OWNER" : da.GetUserDepartmentCode(currentUserId);
                bool canCreateGuard = isOwner || string.Equals(myDeptCode, "AR", StringComparison.OrdinalIgnoreCase);

                var guard = da.GetCurrentGuard();

                if (guard == null)
                    return new { success = true, guard = (object)null, myDepartmentId, isOwner, canCreateGuard };

                // Calcular si todas las locaciones están en estado terminal (Approved o Rejected)
                bool allLocationsClosed = false;
                if (guard.IsConfirmed && guard.HasLocations) {
                    var locs = da.GetActiveGuardLocations();
                    if (locs.Count > 0) {
                        allLocationsClosed = true;
                        foreach (var l in locs) {
                            if (l.Status != "Approved" && l.Status != "Rejected") {
                                allLocationsClosed = false;
                                break;
                            }
                        }
                    }
                }

                return new {
                    success = true,
                    guard = BuildGuardDto(guard, allLocationsClosed),
                    myDepartmentId,
                    isOwner,
                    canCreateGuard
                };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetGuardStatus] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET ACTIVE GUARD LOCATIONS — locaciones con estado de solicitud
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetActiveGuardLocations() {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var da = new GuardDataAccess();
                var locations = da.GetActiveGuardLocations();
                var result = new List<object>();
                foreach (var l in locations)
                    result.Add(new {
                        locationId = l.LocationId,
                        locationName = l.LocationName,
                        requestId = l.RequestId,
                        status = l.Status,
                        requestedBy = l.RequestedBy,
                        requestedAt = l.RequestedAt,
                        reviewedBy = l.ReviewedBy,
                        reviewedAt = l.ReviewedAt,
                        reviewNotes = l.ReviewNotes
                    });

                return new { success = true, data = result };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetActiveGuardLocations] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // GET GUARD HISTORY
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetGuardHistory() {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

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
        // GET ALL ACTIVE LOCATIONS — catálogo para el picker
        // Incluye hasResponsible: indica si hay usuarios Admin o Regular
        // mapeados a la WMS de esa locación vía Users_WMS.
        // NOTA: LocationModel debe exponer HasResponsible (bool),
        //       calculado en GuardDataAccess.GetAllActiveLocations()
        //       con un LEFT JOIN a Users_WMS + Users.RoleId IN (1,2,3).
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object GetAllActiveLocations() {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var da = new GuardDataAccess();
                var locations = da.GetAllActiveLocations();
                var result = new List<object>();
                foreach (var l in locations)
                    result.Add(new {
                        locationId = l.LocationId,
                        locationName = l.LocationName,
                        hasAdmin = l.HasAdmin,
                        hasRegular = l.HasRegular,
                        hasResponsible = l.HasResponsible   // HasAdmin && HasRegular
                    });

                return new { success = true, data = result };
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[GetAllActiveLocations] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // RESERVE DATES — Paso 1: crea borrador con fechas
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object ReserveDates(string startTime, string estimatedEndTime) {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out int createdBy, out int roleId))
                    return new { success = false, message = "Acceso no autorizado." };

                bool isOwner = roleId >= RoleLevel.Owner;
                if (!isOwner) {
                    var daCheck = new GuardDataAccess();
                    string deptCode = daCheck.GetUserDepartmentCode(createdBy);
                    if (!string.Equals(deptCode, "AR", StringComparison.OrdinalIgnoreCase))
                        return new { success = false, message = "Solo el departamento AR puede crear guardias." };
                }

                if (!DateTime.TryParse(startTime, out DateTime start))
                    return new { success = false, message = "Fecha de inicio inválida." };

                DateTime? estEnd = null;
                if (!string.IsNullOrWhiteSpace(estimatedEndTime)) {
                    if (!DateTime.TryParse(estimatedEndTime, out DateTime parsedEstEnd))
                        return new { success = false, message = "Fecha estimada inválida." };
                    estEnd = parsedEstEnd;
                }

                var da = new GuardDataAccess();
                var result = da.ReserveDates(createdBy, start, estEnd);

                if (result.Success) {
                    int capturedGuardId = result.GuardId;
                    DateTime capturedStart = start;
                    string creatorEmail = HttpContext.Current.Session["Email"]?.ToString();
                    System.Threading.Tasks.Task.Run(() =>
                        Services.EmailService.NotifyGuardDraft(
                            capturedGuardId, capturedStart, creatorEmail));
                }

                return result.Success
                    ? (object)new { success = true, guardId = result.GuardId }
                    : new { success = false, message = result.Message };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ReserveDates] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // SAVE GUARD LOCATIONS — Paso 2
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object SaveGuardLocations(int guardId, int[] locationIds) {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var locList = locationIds != null ? new List<int>(locationIds) : new List<int>();
                var da = new GuardDataAccess();
                var result = da.SaveGuardLocations(guardId, locList);

                return result.Success
                    ? (object)new { success = true }
                    : new { success = false, message = result.Message };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[SaveGuardLocations] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // CONFIRM GUARD — Paso 3: confirma y crea spots por depto
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object ConfirmGuard(int guardId) {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out int userId, out int roleId))
                    return new { success = false, message = "Acceso no autorizado." };

                bool isOwner = roleId >= RoleLevel.Owner;
                if (!isOwner) {
                    var daCheck = new GuardDataAccess();
                    string deptCode = daCheck.GetUserDepartmentCode(userId);
                    if (!string.Equals(deptCode, "AR", StringComparison.OrdinalIgnoreCase))
                        return new { success = false, message = "Solo el departamento AR puede confirmar guardias." };
                }

                var da = new GuardDataAccess();
                var result = da.ConfirmGuard(guardId);

                if (result.Success) {
                    var guardForEmail = da.GetCurrentGuard();
                    var capturedSpots = new List<(string, string, string, string)>();
                    if (guardForEmail != null)
                        foreach (var s in guardForEmail.Spots)
                            capturedSpots.Add((s.DepartmentCode, s.DepartmentName,
                                               s.Username, s.Email));

                    DateTime? capturedStart = guardForEmail?.StartTime;
                    string confirmerEmail = HttpContext.Current.Session["Email"]?.ToString();
                    int capturedGuardId = guardId;

                    // Reprogramar el timer preciso para el próximo Start_Time con spots vacíos
                    Startup.ScheduleNextPreciseAssign();

                    System.Threading.Tasks.Task.Run(() => {
                        Services.EmailService.NotifyGuardConfirmed(
                            capturedGuardId, capturedStart, confirmerEmail, capturedSpots);
                        Services.EmailService.NotifyDefaultSpotReminders(capturedGuardId);
                    });
                }

                return result.Success
                    ? (object)new { success = true }
                    : new { success = false, message = result.Message };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[ConfirmGuard] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // CLOSE GUARD — Cierre manual cuando todas las locaciones
        //               han sido procesadas (AR confirma en nombre de AR+CS).

        //       que actualice Guards SET EndTime = GETDATE(), IsFinished = 1
        //       WHERE Guard_Id = @guardId.
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object CloseGuard(int guardId) {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out int userId, out int roleId))
                    return new { success = false, message = "Acceso no autorizado." };

                bool isOwner = roleId >= RoleLevel.Owner;
                if (!isOwner) {
                    var daCheck = new GuardDataAccess();
                    string deptCode = daCheck.GetUserDepartmentCode(userId);
                    if (!string.Equals(deptCode, "AR", StringComparison.OrdinalIgnoreCase))
                        return new { success = false, message = "Solo el departamento AR puede cerrar guardias." };
                }

                // Validar que todas las locaciones estén en estado terminal antes de cerrar
                var daValidate = new GuardDataAccess();
                var locs = daValidate.GetActiveGuardLocations();
                foreach (var l in locs) {
                    if (l.Status != "Approved" && l.Status != "Rejected")
                        return new {
                            success = false,
                            message = "Aún hay locaciones pendientes de procesar. No se puede cerrar la guardia."
                        };
                }

                var da = new GuardDataAccess();
                var result = da.CloseGuard(guardId);

                return result.Success
                    ? (object)new { success = true }
                    : new { success = false, message = result.Message };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[CloseGuard] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // ASSIGN SPOT
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object AssignSpot(int spotId, int userId) {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out int assignedBy, out int roleId))
                    return new { success = false, message = "Acceso no autorizado." };

                if (roleId < RoleLevel.Owner) {
                    var daCheck = new GuardDataAccess();
                    int myDept = daCheck.GetUserDepartmentId(assignedBy);
                    int spotDept = daCheck.GetSpotDepartmentId(spotId);
                    if (myDept == -1 || myDept != spotDept)
                        return new { success = false, message = "Solo puedes asignar responsables en el spot de tu propio departamento." };
                }

                var da = new GuardDataAccess();
                var result = da.AssignSpot(spotId, userId, assignedBy);

                if (result.Success) {
                    // Resolver guardId del spot para notificar a Live.aspx
                    int guardId = da.GetSpotGuardId(spotId);
                    if (guardId > 0)
                        Hubs.LocationHub.NotifySpotChanged(guardId);
                }

                return result.Success
                    ? (object)new { success = true }
                    : new { success = false, message = result.Message };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[AssignSpot] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // UNASSIGN SPOT
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object UnassignSpot(int spotId) {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out int currentUserId, out int roleId))
                    return new { success = false, message = "Acceso no autorizado." };

                if (roleId < RoleLevel.Owner) {
                    var daCheck = new GuardDataAccess();
                    int myDept = daCheck.GetUserDepartmentId(currentUserId);
                    int spotDept = daCheck.GetSpotDepartmentId(spotId);
                    if (myDept == -1 || myDept != spotDept)
                        return new { success = false, message = "Solo puedes limpiar el spot de tu propio departamento." };
                }

                var da = new GuardDataAccess();
                var result = da.UnassignSpot(spotId);

                if (result.Success) {
                    int guardId = da.GetSpotGuardId(spotId);
                    if (guardId > 0)
                        Hubs.LocationHub.NotifySpotChanged(guardId);
                }

                return result.Success
                    ? (object)new { success = true }
                    : new { success = false, message = result.Message };

            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[UnassignSpot] ERROR: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        // ============================================================
        // REMOVE GUARD
        // ============================================================
        [WebMethod(EnableSession = true)]
        public static object RemoveGuard(int guardId) {
            try {
                if (!TryGetSession(RoleLevel.Administrador, out _, out _, out _))
                    return new { success = false, message = "Acceso no autorizado." };

                var da = new GuardDataAccess();

                // Capturar info antes de eliminar para poder notificar después
                bool wasDraft = false;
                DateTime? guardStart = null;
                string audienceEmails = null;
                var currentGuard = da.GetCurrentGuard();
                if (currentGuard != null && currentGuard.GuardId == guardId) {
                    wasDraft = currentGuard.IsDraft;
                    guardStart = currentGuard.StartTime;
                    if (!wasDraft)
                        audienceEmails = new DataAccess.EmailDataAccess()
                                            .GetGuardAudienceEmails(guardId);
                }

                var result = da.RemoveGuard(guardId);

                if (result.Success) {
                    string callerEmail = HttpContext.Current.Session["Email"]?.ToString();
                    DateTime? capturedStart = guardStart;
                    string capturedAudience = audienceEmails;
                    bool capturedWasDraft = wasDraft;

                    System.Threading.Tasks.Task.Run(() => {
                        if (capturedWasDraft)
                            Services.EmailService.NotifyGuardDraftCancelled(capturedStart, callerEmail);
                        else
                            Services.EmailService.NotifyGuardCancelled(
                                capturedStart, callerEmail, capturedAudience);
                    });
                }

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
        private static object BuildGuardDto(GuardViewModel g, bool allLocationsClosed = false) {
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
                estimatedEndTime = g.EstimatedEndTimeIso,
                startTimeFmt = g.StartTimeFmt,
                endTimeFmt = g.EndTimeFmt,
                estimatedEndTimeFmt = g.EstimatedEndTimeFmt,
                createdBy = g.CreatedBy,
                createdAtFmt = g.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                allSpotsFilled = g.AllSpotsFilled,
                isStarted = g.IsStarted,
                isFinished = g.IsFinished,
                isConfirmed = g.IsConfirmed,
                isDraft = g.IsDraft,
                hasLocations = g.HasLocations,
                locationCount = g.LocationCount,
                // true cuando todas las locaciones están Approved o Rejected
                allLocationsClosed,
                spots
            };
        }
    }
}