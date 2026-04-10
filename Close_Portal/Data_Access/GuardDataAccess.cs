using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace Close_Portal.DataAccess {

    // ════════════════════════════════════════════════════════════════════
    // VIEW MODELS
    // ════════════════════════════════════════════════════════════════════

    public class DepartmentViewModel {
        public int DepartmentId { get; set; }
        public string DepartmentCode { get; set; }
        public string DepartmentName { get; set; }
    }

    public class GuardUserViewModel {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Initials { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentCode { get; set; }

        public static string GetInitials(string username) {
            if (string.IsNullOrWhiteSpace(username)) return "??";
            var parts = username.Trim().Split(' ');
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return username.Length >= 2
                ? username.Substring(0, 2).ToUpper()
                : username.ToUpper();
        }
    }

    public class GuardSpotViewModel {
        public int SpotId { get; set; }
        public int GuardId { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentCode { get; set; }
        public string DepartmentName { get; set; }

        // Nullable — NULL = pendiente
        public int? UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Initials { get; set; }
        public string AssignedBy { get; set; }
        public DateTime? AssignedAt { get; set; }

        public bool IsFilled => UserId.HasValue;
    }

    public class GuardViewModel {
        public int GuardId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? EstimatedEndTime { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsConfirmed { get; set; }
        public int LocationCount { get; set; }
        public List<GuardSpotViewModel> Spots { get; set; } = new List<GuardSpotViewModel>();

        public bool AllSpotsFilled => Spots.TrueForAll(s => s.IsFilled);
        public bool IsStarted => StartTime.HasValue && StartTime.Value <= DateTime.Now;
        public bool IsFinished => EndTime.HasValue;
        public bool HasLocations => LocationCount > 0;
        // Borrador = existe pero aún no confirmado (paso 1 o paso 2 en curso)
        public bool IsDraft => !IsConfirmed;

        public string StartTimeFmt => StartTime.HasValue ? StartTime.Value.ToString("dd/MM/yyyy HH:mm") : "—";
        public string EndTimeFmt => EndTime.HasValue ? EndTime.Value.ToString("dd/MM/yyyy HH:mm") : "—";
        public string EstimatedEndTimeFmt => EstimatedEndTime.HasValue ? EstimatedEndTime.Value.ToString("dd/MM/yyyy HH:mm") : null;
        public string StartTimeIso => StartTime.HasValue ? StartTime.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;
        public string EndTimeIso => EndTime.HasValue ? EndTime.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;
        public string EstimatedEndTimeIso => EstimatedEndTime.HasValue ? EstimatedEndTime.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;
    }

    public class GuardResult {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int GuardId { get; set; }
    }

    // Locación activa para el picker del modal de creación
    public class ActiveLocationViewModel {
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        // true si hay un Administrador (Role_Id = 3) activo en Users_Location
        public bool HasAdmin { get; set; }
        // true si hay un Regular (Role_Id = 1) activo en Users_Location
        public bool HasRegular { get; set; }
        // Ambas condiciones — aparece en grupo principal; si falta alguna → grupo rojo
        public bool HasResponsible => HasAdmin && HasRegular;
    }

    // Locación involucrada en la guardia actual (enriquecida con última solicitud)
    public class GuardLocationViewModel {
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public int? RequestId { get; set; }
        public string Status { get; set; }  // "NoRequest" si aún no tiene solicitud
        public string RequestedBy { get; set; }
        public string RequestedAt { get; set; }
        public string ReviewedBy { get; set; }
        public string ReviewedAt { get; set; }
        public string ReviewNotes { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    // DATA ACCESS
    // ════════════════════════════════════════════════════════════════════

    public class GuardDataAccess {

        private readonly string _connStr;

        private static readonly string _staticConnStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        public GuardDataAccess() {
            _connStr = _staticConnStr;
        }

        // ────────────────────────────────────────────────────────────────
        // GET DEPARTMENTS — catálogo activo
        // ────────────────────────────────────────────────────────────────
        public List<DepartmentViewModel> GetDepartments() {
            var list = new List<DepartmentViewModel>();
            try {
                string sql = @"
                    SELECT Department_Id, Department_Code, Department_Name
                    FROM MonthEnd_Departments
                    WHERE Active = 1
                    ORDER BY Department_Code";

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            list.Add(new DepartmentViewModel {
                                DepartmentId = (int)r["Department_Id"],
                                DepartmentCode = r["Department_Code"].ToString(),
                                DepartmentName = r["Department_Name"].ToString()
                            });
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GetDepartments] ERROR: {ex.Message}");
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────────
        // GET USER DEPARTMENT ID — departamento asignado al usuario
        // ────────────────────────────────────────────────────────────────
        public int GetUserDepartmentId(int userId) {
            try {
                string sql = "SELECT Department_Id FROM MonthEnd_Users WHERE User_Id = @UserId";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    return val != null && val != DBNull.Value ? (int)val : -1;
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GetUserDepartmentId] ERROR: {ex.Message}");
                return -1;
            }
        }

        // ────────────────────────────────────────────────────────────────
        // GET USER DEPARTMENT CODE — código del departamento del usuario
        // Usado para validar permiso de creación de guardia (solo AR)
        // ────────────────────────────────────────────────────────────────
        public string GetUserDepartmentCode(int userId) {
            try {
                string sql = @"
                    SELECT d.Department_Code
                    FROM MonthEnd_Users u
                    INNER JOIN MonthEnd_Departments d ON d.Department_Id = u.Department_Id
                    WHERE u.User_Id = @UserId";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    return val != null && val != DBNull.Value ? val.ToString() : null;
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GetUserDepartmentCode] ERROR: {ex.Message}");
                return null;
            }
        }

        // ────────────────────────────────────────────────────────────────
        // GET SPOT DEPARTMENT ID — departamento al que pertenece un spot
        // ────────────────────────────────────────────────────────────────
        public int GetSpotDepartmentId(int spotId) {
            try {
                string sql = "SELECT Department_Id FROM MonthEnd_Guard_Spots WHERE Spot_Id = @SpotId";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@SpotId", spotId);
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    return val != null && val != DBNull.Value ? (int)val : -1;
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GetSpotDepartmentId] ERROR: {ex.Message}");
                return -1;
            }
        }

        // ── Obtener Guard_Id de un spot (para notificaciones SignalR) ────
        public int GetSpotGuardId(int spotId) {
            try {
                string sql = "SELECT Guard_Id FROM MonthEnd_Guard_Spots WHERE Spot_Id = @SpotId";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@SpotId", spotId);
                    conn.Open();
                    var val = cmd.ExecuteScalar();
                    return val != null && val != DBNull.Value ? (int)val : 0;
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GetSpotGuardId] ERROR: {ex.Message}");
                return 0;
            }
        }

        // ────────────────────────────────────────────────────────────────
        // GET USERS BY DEPARTMENT — usuarios activos de un departamento
        // Para el picker de asignación de spot
        // ────────────────────────────────────────────────────────────────
        public List<GuardUserViewModel> GetUsersByDepartment(int departmentId) {
            var list = new List<GuardUserViewModel>();
            try {
                string sql = @"
                    SELECT u.User_Id, u.Username, u.Email, d.Department_Code, d.Department_Id
                    FROM MonthEnd_Users u
                    INNER JOIN MonthEnd_Departments d ON d.Department_Id = u.Department_Id
                    WHERE u.Department_Id = @DeptId
                      AND u.Active  = 1
                      AND u.Locked  = 0
                    ORDER BY u.Username";

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@DeptId", departmentId);
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            var username = r["Username"]?.ToString();
                            list.Add(new GuardUserViewModel {
                                UserId = (int)r["User_Id"],
                                Username = username,
                                Email = r["Email"].ToString(),
                                Initials = GuardUserViewModel.GetInitials(username),
                                DepartmentId = (int)r["Department_Id"],
                                DepartmentCode = r["Department_Code"].ToString()
                            });
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GetUsersByDepartment] ERROR: {ex.Message}");
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────────
        // GET CURRENT GUARD — guardia activa o pendiente (sin End_Time)
        // Retorna null si no hay ninguna en curso
        // ────────────────────────────────────────────────────────────────
        public GuardViewModel GetCurrentGuard() {
            try {
                string sql = @"
                    SELECT
                        gs.Guard_Id,
                        gs.Start_Time,
                        gs.End_Time,
                        gs.Estimated_End_Time,
                        gs.Created_At,
                        gs.Is_Confirmed,
                        cb.Username AS CreatedBy,
                        (SELECT COUNT(*) FROM MonthEnd_Guard_Locations gl WHERE gl.Guard_Id = gs.Guard_Id) AS LocationCount
                    FROM MonthEnd_Guard_Schedule gs
                    LEFT JOIN MonthEnd_Users cb ON cb.User_Id = gs.Created_By
                    WHERE gs.End_Time IS NULL
                    ORDER BY gs.Created_At DESC";

                GuardViewModel guard = null;

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader()) {
                        if (r.Read()) {
                            guard = new GuardViewModel {
                                GuardId = (int)r["Guard_Id"],
                                StartTime = r["Start_Time"] as DateTime?,
                                EndTime = r["End_Time"] as DateTime?,
                                EstimatedEndTime = r["Estimated_End_Time"] as DateTime?,
                                CreatedBy = r["CreatedBy"]?.ToString(),
                                CreatedAt = (DateTime)r["Created_At"],
                                IsConfirmed = (bool)r["Is_Confirmed"],
                                LocationCount = (int)r["LocationCount"]
                            };
                        }
                    }

                    if (guard != null)
                        guard.Spots = LoadSpots(conn, guard.GuardId);
                }

                return guard;
            } catch (Exception ex) {
                Debug.WriteLine($"[GetCurrentGuard] ERROR: {ex.Message}");
                return null;
            }
        }

        // ────────────────────────────────────────────────────────────────
        // GET GUARD HISTORY — guardias finalizadas (tienen End_Time)
        // ────────────────────────────────────────────────────────────────
        public List<GuardViewModel> GetGuardHistory(int top = 20) {
            var list = new List<GuardViewModel>();
            try {
                string sql = $@"
                    SELECT TOP {top}
                        gs.Guard_Id,
                        gs.Start_Time,
                        gs.End_Time,
                        gs.Estimated_End_Time,
                        gs.Created_At,
                        cb.Username AS CreatedBy
                    FROM MonthEnd_Guard_Schedule gs
                    LEFT JOIN MonthEnd_Users cb ON cb.User_Id = gs.Created_By
                    WHERE gs.End_Time IS NOT NULL
                    ORDER BY gs.End_Time DESC";

                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            list.Add(new GuardViewModel {
                                GuardId = (int)r["Guard_Id"],
                                StartTime = r["Start_Time"] as DateTime?,
                                EndTime = r["End_Time"] as DateTime?,
                                EstimatedEndTime = r["Estimated_End_Time"] as DateTime?,
                                CreatedBy = r["CreatedBy"]?.ToString(),
                                CreatedAt = (DateTime)r["Created_At"]
                            });
                        }
                    }

                    foreach (var g in list)
                        g.Spots = LoadSpots(conn, g.GuardId);
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GetGuardHistory] ERROR: {ex.Message}");
            }
            return list;
        }

        // ── Carga spots de un Guard_Id (reutilizable)
        private List<GuardSpotViewModel> LoadSpots(SqlConnection conn, int guardId) {
            var spots = new List<GuardSpotViewModel>();
            string sql = @"
                SELECT
                    sl.Spot_Id,
                    sl.Guard_Id,
                    sl.Department_Id,
                    d.Department_Code,
                    d.Department_Name,
                    sl.User_Id,
                    u.Username,
                    u.Email,
                    ab.Username AS AssignedBy,
                    sl.Assigned_At
                FROM MonthEnd_Guard_Spots sl
                INNER JOIN MonthEnd_Departments d ON d.Department_Id = sl.Department_Id
                LEFT  JOIN MonthEnd_Users u  ON u.User_Id  = sl.User_Id
                LEFT  JOIN MonthEnd_Users ab ON ab.User_Id = sl.Assigned_By
                WHERE sl.Guard_Id = @GuardId
                ORDER BY d.Department_Code";

            using (var cmd = new SqlCommand(sql, conn)) {
                cmd.Parameters.AddWithValue("@GuardId", guardId);
                using (var r = cmd.ExecuteReader()) {
                    while (r.Read()) {
                        var username = r["Username"] as string;
                        spots.Add(new GuardSpotViewModel {
                            SpotId = (int)r["Spot_Id"],
                            GuardId = (int)r["Guard_Id"],
                            DepartmentId = (int)r["Department_Id"],
                            DepartmentCode = r["Department_Code"].ToString(),
                            DepartmentName = r["Department_Name"].ToString(),
                            UserId = r["User_Id"] as int?,
                            Username = username,
                            Email = r["Email"] as string,
                            Initials = username != null ? GuardUserViewModel.GetInitials(username) : null,
                            AssignedBy = r["AssignedBy"] as string,
                            AssignedAt = r["Assigned_At"] as DateTime?
                        });
                    }
                }
            }
            return spots;
        }

        // ────────────────────────────────────────────────────────────────
        // RESERVE DATES — Paso 1: crea borrador de guardia (sin spots, sin locaciones)
        // ────────────────────────────────────────────────────────────────
        public GuardResult ReserveDates(int createdBy, DateTime startTime, DateTime? estimatedEndTime = null) {
            Debug.WriteLine($"[ReserveDates] CreatedBy={createdBy} StartTime={startTime:G}");
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM MonthEnd_Guard_Schedule WHERE End_Time IS NULL", conn)) {
                        if ((int)cmd.ExecuteScalar() > 0)
                            return new GuardResult { Success = false, Message = "Ya existe una guardia abierta. Ciérrala antes de reservar una nueva fecha." };
                    }

                    if (startTime < DateTime.Now.AddMinutes(-5))
                        return new GuardResult { Success = false, Message = "La fecha de inicio no puede estar en el pasado." };

                    if (estimatedEndTime.HasValue && estimatedEndTime.Value <= startTime)
                        return new GuardResult { Success = false, Message = "La fecha estimada debe ser posterior al inicio." };

                    using (var tx = conn.BeginTransaction()) {
                        try {
                            // 1. Insertar guardia como borrador
                            int guardId;
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO MonthEnd_Guard_Schedule
                                    (Start_Time, Estimated_End_Time, Created_By, Created_At, Is_Confirmed)
                                OUTPUT INSERTED.Guard_Id
                                VALUES (@StartTime, @EstEnd, @CreatedBy, GETDATE(), 0)", conn, tx)) {
                                cmd.Parameters.AddWithValue("@StartTime", startTime);
                                cmd.Parameters.AddWithValue("@EstEnd",
                                    estimatedEndTime.HasValue ? (object)estimatedEndTime.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                                guardId = (int)cmd.ExecuteScalar();
                            }

                            // 2. Crear spots por departamento activo inmediatamente
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO MonthEnd_Guard_Spots (Guard_Id, Department_Id)
                                SELECT @GuardId, Department_Id
                                FROM MonthEnd_Departments
                                WHERE Active = 1", conn, tx)) {
                                cmd.Parameters.AddWithValue("@GuardId", guardId);
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            Debug.WriteLine($"[ReserveDates] Guard_Id={guardId} (borrador con spots) ✓");
                            return new GuardResult { Success = true, GuardId = guardId };

                        } catch { tx.Rollback(); throw; }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[ReserveDates] ERROR: {ex.Message}");
                return new GuardResult { Success = false, Message = ex.Message };
            }
        }

        // ────────────────────────────────────────────────────────────────
        // SAVE GUARD LOCATIONS — Paso 2: guarda locaciones seleccionadas
        // Reemplaza cualquier selección previa del mismo borrador
        // ────────────────────────────────────────────────────────────────
        public GuardResult SaveGuardLocations(int guardId, List<int> locationIds) {
            Debug.WriteLine($"[SaveGuardLocations] GuardId={guardId} Locations={locationIds.Count}");
            if (locationIds == null || locationIds.Count == 0)
                return new GuardResult { Success = false, Message = "Debes seleccionar al menos una locación." };
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // Verificar que la guardia existe, no está confirmada y no ha cerrado
                    using (var cmd = new SqlCommand(
                        "SELECT Is_Confirmed FROM MonthEnd_Guard_Schedule WHERE Guard_Id = @GuardId AND End_Time IS NULL", conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        var val = cmd.ExecuteScalar();
                        if (val == null)
                            return new GuardResult { Success = false, Message = "Guardia no encontrada o ya cerrada." };
                        if ((bool)val)
                            return new GuardResult { Success = false, Message = "La guardia ya está confirmada." };
                    }

                    using (var tx = conn.BeginTransaction()) {
                        try {
                            // Limpiar selección previa
                            using (var cmd = new SqlCommand(
                                "DELETE FROM MonthEnd_Guard_Locations WHERE Guard_Id = @GuardId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@GuardId", guardId);
                                cmd.ExecuteNonQuery();
                            }

                            // Insertar nueva selección
                            foreach (int locId in locationIds) {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO MonthEnd_Guard_Locations (Guard_Id, Location_Id)
                                    VALUES (@GuardId, @LocationId)", conn, tx)) {
                                    cmd.Parameters.AddWithValue("@GuardId", guardId);
                                    cmd.Parameters.AddWithValue("@LocationId", locId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();
                        } catch { tx.Rollback(); throw; }
                    }

                    Debug.WriteLine($"[SaveGuardLocations] {locationIds.Count} locaciones guardadas ✓");
                    return new GuardResult { Success = true, GuardId = guardId };
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[SaveGuardLocations] ERROR: {ex.Message}");
                return new GuardResult { Success = false, Message = ex.Message };
            }
        }

        // ────────────────────────────────────────────────────────────────
        // CONFIRM GUARD — Paso 3: confirma la guardia, crea spots por depto
        // ────────────────────────────────────────────────────────────────
        public GuardResult ConfirmGuard(int guardId) {
            Debug.WriteLine($"[ConfirmGuard] GuardId={guardId}");
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // Verificar estado y que tenga locaciones seleccionadas
                    using (var cmd = new SqlCommand(@"
                        SELECT gs.Is_Confirmed,
                               (SELECT COUNT(*) FROM MonthEnd_Guard_Locations gl WHERE gl.Guard_Id = gs.Guard_Id) AS LocCount
                        FROM MonthEnd_Guard_Schedule gs
                        WHERE gs.Guard_Id = @GuardId AND gs.End_Time IS NULL", conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        using (var r = cmd.ExecuteReader()) {
                            if (!r.Read())
                                return new GuardResult { Success = false, Message = "Guardia no encontrada." };
                            if ((bool)r["Is_Confirmed"])
                                return new GuardResult { Success = false, Message = "La guardia ya está confirmada." };
                            if ((int)r["LocCount"] == 0)
                                return new GuardResult { Success = false, Message = "Confirma las locaciones antes de crear la guardia." };
                        }
                    }

                    // Solo marcar como confirmada — spots ya existen desde ReserveDates
                    using (var cmd = new SqlCommand(
                        "UPDATE MonthEnd_Guard_Schedule SET Is_Confirmed = 1 WHERE Guard_Id = @GuardId", conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        cmd.ExecuteNonQuery();
                    }

                    Debug.WriteLine($"[ConfirmGuard] Guard_Id={guardId} confirmada ✓");
                    return new GuardResult { Success = true, GuardId = guardId };
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[ConfirmGuard] ERROR: {ex.Message}");
                return new GuardResult { Success = false, Message = ex.Message };
            }
        }

        // ────────────────────────────────────────────────────────────────
        // ASSIGN SPOT — asigna un usuario a un spot
        // El usuario debe pertenecer al mismo departamento que el spot
        // ────────────────────────────────────────────────────────────────
        public GuardResult AssignSpot(int spotId, int userId, int assignedBy) {
            Debug.WriteLine($"[AssignSpot] SpotId={spotId} UserId={userId} By={assignedBy}");
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // Verificar que el usuario pertenece al departamento del spot
                    string sqlCheck = @"
                        SELECT COUNT(*)
                        FROM MonthEnd_Guard_Spots sl
                        INNER JOIN MonthEnd_Users u ON u.User_Id = @UserId
                        WHERE sl.Spot_Id = @SpotId
                          AND sl.Department_Id = u.Department_Id";

                    using (var cmd = new SqlCommand(sqlCheck, conn)) {
                        cmd.Parameters.AddWithValue("@SpotId", spotId);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        int match = (int)cmd.ExecuteScalar();
                        if (match == 0)
                            return new GuardResult { Success = false, Message = "El usuario no pertenece al departamento de este spot." };
                    }

                    using (var cmd = new SqlCommand(@"
                        UPDATE MonthEnd_Guard_Spots
                        SET User_Id     = @UserId,
                            Assigned_By = @AssignedBy,
                            Assigned_At = GETDATE()
                        WHERE Spot_Id = @SpotId", conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@AssignedBy", assignedBy);
                        cmd.Parameters.AddWithValue("@SpotId", spotId);
                        cmd.ExecuteNonQuery();
                    }

                    Debug.WriteLine($"[AssignSpot] Spot {spotId} → User {userId} ✓");
                    return new GuardResult { Success = true };
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[AssignSpot] ERROR: {ex.Message}");
                return new GuardResult { Success = false, Message = ex.Message };
            }
        }

        // ────────────────────────────────────────────────────────────────
        // UNASSIGN SPOT — limpia el usuario de un spot
        // ────────────────────────────────────────────────────────────────
        public GuardResult UnassignSpot(int spotId) {
            Debug.WriteLine($"[UnassignSpot] SpotId={spotId}");
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        UPDATE MonthEnd_Guard_Spots
                        SET User_Id = NULL, Assigned_By = NULL, Assigned_At = NULL
                        WHERE Spot_Id = @SpotId";
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@SpotId", spotId);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                return new GuardResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[UnassignSpot] ERROR: {ex.Message}");
                return new GuardResult { Success = false, Message = ex.Message };
            }
        }

        // ────────────────────────────────────────────────────────────────
        // CLOSE GUARD — setea End_Time = NOW()
        // Llamado por el sistema cuando todas las locaciones cierran
        // ────────────────────────────────────────────────────────────────
        public GuardResult CloseGuard(int guardId) {
            Debug.WriteLine($"[CloseGuard] GuardId={guardId}");
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    string sql = @"
                        UPDATE MonthEnd_Guard_Schedule SET End_Time = GETDATE()
                        WHERE Guard_Id = @GuardId AND End_Time IS NULL";
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        conn.Open();
                        int rows = cmd.ExecuteNonQuery();
                        if (rows == 0)
                            return new GuardResult { Success = false, Message = "Guardia no encontrada o ya cerrada." };
                    }
                }
                Debug.WriteLine($"[CloseGuard] Guard {guardId} cerrada ✓");
                return new GuardResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[CloseGuard] ERROR: {ex.Message}");
                return new GuardResult { Success = false, Message = ex.Message };
            }
        }

        // ────────────────────────────────────────────────────────────────
        // REMOVE GUARD — elimina una guardia y sus spots (CASCADE)
        // Solo permitido si aún no ha iniciado (Start_Time IS NULL)
        // ────────────────────────────────────────────────────────────────
        public GuardResult RemoveGuard(int guardId) {
            Debug.WriteLine($"[RemoveGuard] GuardId={guardId}");
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // Solo se puede eliminar si la guardia no ha cerrado
                    using (var cmd = new SqlCommand(
                        "SELECT End_Time FROM MonthEnd_Guard_Schedule WHERE Guard_Id = @GuardId", conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value)
                            return new GuardResult { Success = false, Message = "No se puede eliminar una guardia ya cerrada." };
                    }

                    using (var tx = conn.BeginTransaction()) {
                        try {
                            // 1. Eliminar solicitudes de cierre asociadas (FK_ClosureRequests_Guard)
                            using (var cmd = new SqlCommand(
                                "DELETE FROM MonthEnd_Closure_Requests WHERE Guard_Id = @GuardId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@GuardId", guardId);
                                cmd.ExecuteNonQuery();
                            }

                            // 2. Eliminar la guardia (CASCADE elimina Guard_Locations y Guard_Spots)
                            using (var cmd = new SqlCommand(
                                "DELETE FROM MonthEnd_Guard_Schedule WHERE Guard_Id = @GuardId", conn, tx)) {
                                cmd.Parameters.AddWithValue("@GuardId", guardId);
                                int rows = cmd.ExecuteNonQuery();
                                if (rows == 0) {
                                    tx.Rollback();
                                    return new GuardResult { Success = false, Message = "Guardia no encontrada." };
                                }
                            }

                            tx.Commit();
                        } catch {
                            tx.Rollback();
                            throw;
                        }
                    }
                }
                return new GuardResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[RemoveGuard] ERROR: {ex.Message}");
                return new GuardResult { Success = false, Message = ex.Message };
            }
        }

        // ────────────────────────────────────────────────────────────────
        // GET ALL ACTIVE LOCATIONS — catálogo para el picker del borrador
        // HasAdmin   = existe un Administrador (Role_Id=3) activo en MonthEnd_Users_Location
        // HasRegular = existe un Regular       (Role_Id=1) activo en MonthEnd_Users_Location
        // ────────────────────────────────────────────────────────────────
        public List<ActiveLocationViewModel> GetAllActiveLocations() {
            var list = new List<ActiveLocationViewModel>();
            try {
                string sql = @"
                    SELECT
                        wl.Location_Id,
                        wl.Location_Name,
                        CASE WHEN EXISTS (
                            SELECT 1 FROM MonthEnd_Users_Location ul
                            INNER JOIN MonthEnd_Users u ON u.User_Id = ul.User_Id
                            WHERE ul.Location_Id = wl.Location_Id
                              AND u.Role_Id = 3
                              AND u.Active  = 1
                              AND u.Locked  = 0
                        ) THEN 1 ELSE 0 END AS HasAdmin,
                        CASE WHEN EXISTS (
                            SELECT 1 FROM MonthEnd_Users_Location ul
                            INNER JOIN MonthEnd_Users u ON u.User_Id = ul.User_Id
                            WHERE ul.Location_Id = wl.Location_Id
                              AND u.Role_Id = 1
                              AND u.Active  = 1
                              AND u.Locked  = 0
                        ) THEN 1 ELSE 0 END AS HasRegular
                    FROM MonthEnd_Locations wl
                    WHERE wl.Active = 1
                    ORDER BY wl.Location_Name";

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            list.Add(new ActiveLocationViewModel {
                                LocationId = (int)r["Location_Id"],
                                LocationName = r["Location_Name"].ToString(),
                                HasAdmin = (int)r["HasAdmin"] == 1,
                                HasRegular = (int)r["HasRegular"] == 1
                            });
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GetAllActiveLocations] ERROR: {ex.Message}");
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────────
        // GET ACTIVE GUARD LOCATIONS — locaciones pre-definidas en la guardia
        // enriquecidas con la última solicitud de cierre de esa misma guardia
        // Scope: Guard_Id exacto, sin filtros por fecha
        // ────────────────────────────────────────────────────────────────
        public List<GuardLocationViewModel> GetActiveGuardLocations() {
            var list = new List<GuardLocationViewModel>();
            try {
                string sql = @"
                    DECLARE @GuardId INT;

                    SELECT TOP 1 @GuardId = Guard_Id
                    FROM MonthEnd_Guard_Schedule
                    WHERE End_Time IS NULL
                    ORDER BY Created_At DESC;

                    IF @GuardId IS NULL RETURN;

                    WITH LatestReq AS (
                        SELECT
                            cr.Location_Id,
                            cr.Request_Id,
                            cr.Status,
                            cr.Created_At   AS RequestedAt,
                            cr.Reviewed_At,
                            cr.Review_Notes,
                            u.Username      AS RequestedBy,
                            ru.Username     AS ReviewedBy,
                            ROW_NUMBER() OVER (
                                PARTITION BY cr.Location_Id
                                ORDER BY cr.Created_At DESC
                            ) AS rn
                        FROM  MonthEnd_Closure_Requests cr
                        INNER JOIN MonthEnd_Users u  ON cr.Requested_By = u.User_Id
                        LEFT  JOIN MonthEnd_Users ru ON cr.Reviewed_By  = ru.User_Id
                        WHERE cr.Guard_Id = @GuardId
                    )
                    SELECT
                        wl.Location_Id,
                        wl.Location_Name,
                        lr.Request_Id,
                        ISNULL(lr.Status, 'NoRequest') AS Status,
                        lr.RequestedBy,
                        lr.RequestedAt,
                        lr.ReviewedBy,
                        lr.Reviewed_At,
                        lr.Review_Notes
                    FROM  MonthEnd_Guard_Locations gl
                    INNER JOIN MonthEnd_Locations wl ON wl.Location_Id = gl.Location_Id
                    LEFT  JOIN LatestReq lr ON lr.Location_Id = gl.Location_Id AND lr.rn = 1
                    WHERE gl.Guard_Id = @GuardId
                    ORDER BY
                        CASE ISNULL(lr.Status, 'NoRequest')
                            WHEN 'Pending'   THEN 1
                            WHEN 'Rejected'  THEN 2
                            WHEN 'NoRequest' THEN 3
                            WHEN 'Approved'  THEN 4
                            ELSE 5
                        END,
                        wl.Location_Name";

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            list.Add(new GuardLocationViewModel {
                                LocationId = (int)r["Location_Id"],
                                LocationName = r["Location_Name"].ToString(),
                                RequestId = r["Request_Id"] as int?,
                                Status = r["Status"].ToString(),
                                RequestedBy = r["RequestedBy"] as string ?? "",
                                RequestedAt = r["RequestedAt"] is DateTime ra
                                                   ? ra.ToString("dd/MM HH:mm") : "",
                                ReviewedBy = r["ReviewedBy"] as string ?? "",
                                ReviewedAt = r["Reviewed_At"] is DateTime rv
                                                   ? rv.ToString("dd/MM HH:mm") : "",
                                ReviewNotes = r["Review_Notes"] as string ?? ""
                            });
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GetActiveGuardLocations] ERROR: {ex.Message}");
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────────
        // GET ACTIVE GUARD EMAILS (STATIC) — para EmailService
        // Retorna emails separados por ; de usuarios en spots activos ahora
        // ────────────────────────────────────────────────────────────────
        public static string GetActiveGuardEmails() {
            try {
                string sql = @"
                    SELECT DISTINCT u.Email
                    FROM MonthEnd_Guard_Schedule gs
                    INNER JOIN MonthEnd_Guard_Spots sl ON sl.Guard_Id = gs.Guard_Id
                    INNER JOIN MonthEnd_Users u ON u.User_Id = sl.User_Id
                    WHERE gs.Start_Time IS NOT NULL
                      AND gs.End_Time   IS NULL
                      AND u.Active = 1";

                var emails = new List<string>();

                using (var conn = new SqlConnection(_staticConnStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            var email = r["Email"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(email))
                                emails.Add(email.Trim());
                        }
                    }
                }

                Debug.WriteLine($"[GetActiveGuardEmails] Guardias activos: {string.Join(", ", emails)}");
                return emails.Count > 0 ? string.Join(";", emails) : null;

            } catch (Exception ex) {
                Debug.WriteLine($"[GetActiveGuardEmails] ERROR: {ex.Message}");
                return null;
            }
        }

        // ────────────────────────────────────────────────────────────────
        // AUTO-ASSIGN DEFAULT SPOTS — llamado por timer periódico
        // Cuando la guardia ya inició (Start_Time <= NOW) y hay spots sin
        // usuario asignado, usa MonthEnd_Departments.Default_User_Id para
        // asignarlo automáticamente.
        // Retorna la lista de Guard_Id afectados (para notificar via SignalR).
        // ────────────────────────────────────────────────────────────────
        public static List<int> AutoAssignDefaultSpots() {
            var affectedGuards = new List<int>();
            try {
                // Lee los spots vacíos cuyo departamento tiene Default_User_Id configurado
                const string sqlSpots = @"
                    SELECT gs_spot.Spot_Id, gs.Guard_Id, d.Department_Code, d.Default_User_Id
                    FROM MonthEnd_Guard_Schedule gs
                    INNER JOIN MonthEnd_Guard_Spots gs_spot ON gs_spot.Guard_Id = gs.Guard_Id
                    INNER JOIN MonthEnd_Departments d       ON d.Department_Id  = gs_spot.Department_Id
                    INNER JOIN MonthEnd_Users u             ON u.User_Id        = d.Default_User_Id
                                                           AND u.Active = 1 AND u.Locked = 0
                    WHERE gs.Is_Confirmed    = 1
                      AND gs.Start_Time     <= GETDATE()
                      AND gs.End_Time        IS NULL
                      AND gs_spot.User_Id    IS NULL
                      AND d.Default_User_Id  IS NOT NULL";

                var toAssign = new List<(int SpotId, int GuardId, string DeptCode, int DefaultUserId)>();
                using (var conn = new SqlConnection(_staticConnStr))
                using (var cmd = new SqlCommand(sqlSpots, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            toAssign.Add((
                                (int)r["Spot_Id"],
                                (int)r["Guard_Id"],
                                r["Department_Code"].ToString(),
                                (int)r["Default_User_Id"]));
                }

                if (toAssign.Count == 0) return affectedGuards;
                Debug.WriteLine($"[AutoAssignDefaultSpots] {toAssign.Count} spot(s) sin asignar — asignando defaults...");

                const string sqlAssign = @"
                    UPDATE MonthEnd_Guard_Spots
                    SET User_Id     = @UserId,
                        Assigned_By = NULL,
                        Assigned_At = GETDATE()
                    WHERE Spot_Id   = @SpotId
                      AND User_Id   IS NULL";  // doble-check: no pisar si ya fue asignado

                foreach (var (spotId, guardId, deptCode, defaultUserId) in toAssign) {
                    try {
                        using (var conn = new SqlConnection(_staticConnStr))
                        using (var cmd = new SqlCommand(sqlAssign, conn)) {
                            cmd.Parameters.AddWithValue("@UserId", defaultUserId);
                            cmd.Parameters.AddWithValue("@SpotId", spotId);
                            conn.Open();
                            int rows = cmd.ExecuteNonQuery();
                            if (rows > 0) {
                                Debug.WriteLine($"[AutoAssignDefaultSpots] Spot {spotId} (Guard {guardId}, {deptCode}) → UserId {defaultUserId} ✓");
                                if (!affectedGuards.Contains(guardId))
                                    affectedGuards.Add(guardId);
                            }
                        }
                    } catch (Exception ex) {
                        Debug.WriteLine($"[AutoAssignDefaultSpots] ERROR spot {spotId}: {ex.Message}");
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[AutoAssignDefaultSpots] ERROR: {ex.Message}");
            }
            return affectedGuards;
        }
    }
}