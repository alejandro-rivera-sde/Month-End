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
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<GuardSpotViewModel> Spots { get; set; } = new List<GuardSpotViewModel>();

        // Estado derivado de los spots y tiempos
        public bool AllSpotsFilled => Spots.TrueForAll(s => s.IsFilled);
        public bool IsStarted => StartTime.HasValue;
        public bool IsFinished => EndTime.HasValue;

        public string StartTimeFmt => StartTime.HasValue ? StartTime.Value.ToString("dd/MM/yyyy HH:mm") : "—";
        public string EndTimeFmt => EndTime.HasValue ? EndTime.Value.ToString("dd/MM/yyyy HH:mm") : "—";
        public string StartTimeIso => StartTime.HasValue ? StartTime.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;
        public string EndTimeIso => EndTime.HasValue ? EndTime.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;
    }

    public class GuardResult {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int GuardId { get; set; }
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
                    FROM Departments
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
        // GET USERS BY DEPARTMENT — usuarios activos de un departamento
        // Para el picker de asignación de spot
        // ────────────────────────────────────────────────────────────────
        public List<GuardUserViewModel> GetUsersByDepartment(int departmentId) {
            var list = new List<GuardUserViewModel>();
            try {
                string sql = @"
                    SELECT u.User_Id, u.Username, u.Email, d.Department_Code, d.Department_Id
                    FROM Users u
                    INNER JOIN Departments d ON d.Department_Id = u.Department_Id
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
                        gs.Created_At,
                        cb.Username AS CreatedBy
                    FROM Guard_Schedule gs
                    LEFT JOIN Users cb ON cb.User_Id = gs.Created_By
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
                                CreatedBy = r["CreatedBy"]?.ToString(),
                                CreatedAt = (DateTime)r["Created_At"]
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
                        gs.Created_At,
                        cb.Username AS CreatedBy
                    FROM Guard_Schedule gs
                    LEFT JOIN Users cb ON cb.User_Id = gs.Created_By
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
                FROM Guard_Spots sl
                INNER JOIN Departments d ON d.Department_Id = sl.Department_Id
                LEFT  JOIN Users u  ON u.User_Id  = sl.User_Id
                LEFT  JOIN Users ab ON ab.User_Id = sl.Assigned_By
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
        // CREATE GUARD — crea Guard_Schedule + un spot por cada depto activo
        // ────────────────────────────────────────────────────────────────
        public GuardResult CreateGuard(int createdBy) {
            Debug.WriteLine($"[CreateGuard] CreatedBy={createdBy}");
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // Verificar que no haya guardia activa/pendiente
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM Guard_Schedule WHERE End_Time IS NULL", conn)) {
                        int open = (int)cmd.ExecuteScalar();
                        if (open > 0)
                            return new GuardResult { Success = false, Message = "Ya existe una guardia abierta. Ciérrala antes de crear una nueva." };
                    }

                    using (var tx = conn.BeginTransaction()) {
                        try {
                            // Insertar guardia (Start/End null — no ha iniciado)
                            int guardId;
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO Guard_Schedule (Created_By, Created_At)
                                OUTPUT INSERTED.Guard_Id
                                VALUES (@CreatedBy, GETDATE())", conn, tx)) {
                                cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                                guardId = (int)cmd.ExecuteScalar();
                            }

                            // Insertar un spot por cada departamento activo
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO Guard_Spots (Guard_Id, Department_Id)
                                SELECT @GuardId, Department_Id
                                FROM Departments
                                WHERE Active = 1", conn, tx)) {
                                cmd.Parameters.AddWithValue("@GuardId", guardId);
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            Debug.WriteLine($"[CreateGuard] Guard_Id={guardId} creado ✓");
                            return new GuardResult { Success = true, GuardId = guardId };

                        } catch {
                            tx.Rollback();
                            throw;
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[CreateGuard] ERROR: {ex.Message}");
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
                        FROM Guard_Spots sl
                        INNER JOIN Users u ON u.User_Id = @UserId
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
                        UPDATE Guard_Spots
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
                        UPDATE Guard_Spots
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
        // START GUARD — setea Start_Time = NOW()
        // Solo permitido si todos los spots tienen usuario asignado
        // ────────────────────────────────────────────────────────────────
        public GuardResult StartGuard(int guardId) {
            Debug.WriteLine($"[StartGuard] GuardId={guardId}");
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // Verificar que todos los spots están llenos
                    string sqlCheck = @"
                        SELECT COUNT(*) FROM Guard_Spots
                        WHERE Guard_Id = @GuardId AND User_Id IS NULL";
                    using (var cmd = new SqlCommand(sqlCheck, conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        int empty = (int)cmd.ExecuteScalar();
                        if (empty > 0)
                            return new GuardResult { Success = false, Message = "Todos los departamentos deben tener un responsable asignado antes de iniciar la guardia." };
                    }

                    // Verificar que no esté ya iniciada
                    string sqlStarted = "SELECT Start_Time FROM Guard_Schedule WHERE Guard_Id = @GuardId";
                    using (var cmd = new SqlCommand(sqlStarted, conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value)
                            return new GuardResult { Success = false, Message = "La guardia ya fue iniciada." };
                    }

                    using (var cmd = new SqlCommand(@"
                        UPDATE Guard_Schedule SET Start_Time = GETDATE()
                        WHERE Guard_Id = @GuardId", conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        cmd.ExecuteNonQuery();
                    }

                    Debug.WriteLine($"[StartGuard] Guard {guardId} iniciada ✓");
                    return new GuardResult { Success = true, GuardId = guardId };
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[StartGuard] ERROR: {ex.Message}");
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
                        UPDATE Guard_Schedule SET End_Time = GETDATE()
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

                    // Solo se puede eliminar si no ha iniciado
                    string sqlCheck = "SELECT Start_Time FROM Guard_Schedule WHERE Guard_Id = @GuardId";
                    using (var cmd = new SqlCommand(sqlCheck, conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value)
                            return new GuardResult { Success = false, Message = "No se puede eliminar una guardia que ya fue iniciada." };
                    }

                    string sql = "DELETE FROM Guard_Schedule WHERE Guard_Id = @GuardId";
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        int rows = cmd.ExecuteNonQuery();
                        if (rows == 0)
                            return new GuardResult { Success = false, Message = "Guardia no encontrada." };
                    }
                }
                return new GuardResult { Success = true };
            } catch (Exception ex) {
                Debug.WriteLine($"[RemoveGuard] ERROR: {ex.Message}");
                return new GuardResult { Success = false, Message = ex.Message };
            }
        }

        // ────────────────────────────────────────────────────────────────
        // GET ACTIVE GUARD EMAILS (STATIC) — para EmailService
        // Retorna emails separados por ; de usuarios en spots activos ahora
        // ────────────────────────────────────────────────────────────────
        public static string GetActiveGuardEmails() {
            try {
                string sql = @"
                    SELECT DISTINCT u.Email
                    FROM Guard_Schedule gs
                    INNER JOIN Guard_Spots sl ON sl.Guard_Id = gs.Guard_Id
                    INNER JOIN Users u ON u.User_Id = sl.User_Id
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
    }
}