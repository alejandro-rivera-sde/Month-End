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

    public class OwnerViewModel {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Initials { get; set; }
        public bool HasActiveGuard { get; set; }

        /// <summary>Calcula iniciales a partir del Username.</summary>
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

    public class GuardScheduleViewModel {
        public int GuardId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Initials { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string AssignedBy { get; set; }   // Username de quien asignó
        public bool IsActive { get; set; }   // NOW() BETWEEN Start AND End

        // Formatos para JS/UI
        public string StartTimeIso => StartTime.ToString("yyyy-MM-ddTHH:mm:ss");
        public string EndTimeIso => EndTime.ToString("yyyy-MM-ddTHH:mm:ss");
        public string StartTimeFmt => StartTime.ToString("dd/MM/yyyy HH:mm");
        public string EndTimeFmt => EndTime.ToString("dd/MM/yyyy HH:mm");
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

        // Necesario para llamarlo desde EmailService (estático)
        private static readonly string _staticConnStr =
            ConfigurationManager.ConnectionStrings["ClosePortalDB"].ConnectionString;

        public GuardDataAccess() {
            _connStr = _staticConnStr;
        }

        // ────────────────────────────────────────────────────────────────
        // GET OWNERS — todos los usuarios con RoleId = 4
        // ────────────────────────────────────────────────────────────────
        public List<OwnerViewModel> GetOwners() {
            var list = new List<OwnerViewModel>();

            try {
                string sql = @"
                    SELECT
                        u.User_Id,
                        u.Username,
                        u.Email,
                        CASE
                            WHEN EXISTS (
                                SELECT 1 FROM Guard_Schedule gs
                                WHERE gs.User_Id = u.User_Id
                                  AND GETDATE() BETWEEN gs.Start_Time AND gs.End_Time
                            ) THEN 1 ELSE 0
                        END AS HasActiveGuard
                    FROM Users u
                    WHERE u.Role_Id = 4
                      AND u.Active  = 1
                      AND u.Locked  = 0
                    ORDER BY u.Username";

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            var username = r["Username"]?.ToString();
                            list.Add(new OwnerViewModel {
                                UserId = (int)r["User_Id"],
                                Username = username,
                                Email = r["Email"]?.ToString(),
                                Initials = OwnerViewModel.GetInitials(username),
                                HasActiveGuard = (int)r["HasActiveGuard"] == 1
                            });
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GuardDataAccess.GetOwners] ERROR: {ex.Message}");
            }

            return list;
        }

        // ────────────────────────────────────────────────────────────────
        // GET SCHEDULE — turnos activos y futuros (no pasados)
        // ────────────────────────────────────────────────────────────────
        public List<GuardScheduleViewModel> GetSchedule() {
            var list = new List<GuardScheduleViewModel>();

            try {
                string sql = @"
                    SELECT
                        gs.Guard_Id,
                        gs.User_Id,
                        u.Username,
                        u.Email,
                        gs.Start_Time,
                        gs.End_Time,
                        ab.Username AS AssignedBy,
                        CASE WHEN GETDATE() BETWEEN gs.Start_Time AND gs.End_Time THEN 1 ELSE 0 END AS IsActive
                    FROM Guard_Schedule gs
                    INNER JOIN Users u  ON gs.User_Id     = u.User_Id
                    INNER JOIN Users ab ON gs.Assigned_By = ab.User_Id
                    WHERE gs.End_Time > GETDATE()
                    ORDER BY gs.Start_Time ASC";

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn)) {
                    conn.Open();
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            var username = r["Username"]?.ToString();
                            list.Add(new GuardScheduleViewModel {
                                GuardId = (int)r["Guard_Id"],
                                UserId = (int)r["User_Id"],
                                Username = username,
                                Email = r["Email"]?.ToString(),
                                Initials = OwnerViewModel.GetInitials(username),
                                StartTime = (DateTime)r["Start_Time"],
                                EndTime = (DateTime)r["End_Time"],
                                AssignedBy = r["AssignedBy"]?.ToString(),
                                IsActive = (int)r["IsActive"] == 1
                            });
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[GuardDataAccess.GetSchedule] ERROR: {ex.Message}");
            }

            return list;
        }

        // ────────────────────────────────────────────────────────────────
        // ASSIGN GUARD — inserta turno con validaciones
        // Regla: un Owner no puede tener otro turno que se solape
        // ────────────────────────────────────────────────────────────────
        public GuardResult AssignGuard(int userId, DateTime startTime, DateTime endTime, int assignedBy) {
            Debug.WriteLine($"[AssignGuard] UserId={userId} | {startTime:G} → {endTime:G} | By={assignedBy}");

            // Validación básica de fechas
            if (endTime <= startTime)
                return new GuardResult { Success = false, Message = "La fecha de fin debe ser posterior al inicio." };

            if (startTime < DateTime.Now.AddMinutes(-5))
                return new GuardResult { Success = false, Message = "La fecha de inicio no puede estar en el pasado." };

            try {
                using (var conn = new SqlConnection(_connStr)) {
                    conn.Open();

                    // 1. Verificar solapamiento con turnos existentes del mismo Owner
                    string sqlOverlap = @"
                        SELECT COUNT(*) FROM Guard_Schedule
                        WHERE User_Id = @UserId
                          AND End_Time > GETDATE()
                          AND NOT (@EndTime <= Start_Time OR @StartTime >= End_Time)";

                    using (var cmd = new SqlCommand(sqlOverlap, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@StartTime", startTime);
                        cmd.Parameters.AddWithValue("@EndTime", endTime);

                        int overlaps = (int)cmd.ExecuteScalar();
                        if (overlaps > 0)
                            return new GuardResult { Success = false, Message = "Este Owner ya tiene un turno programado en ese rango de fechas." };
                    }

                    // 2. Insertar turno
                    string sqlInsert = @"
                        INSERT INTO Guard_Schedule (User_Id, Start_Time, End_Time, Assigned_By, Created_At)
                        OUTPUT INSERTED.Guard_Id
                        VALUES (@UserId, @StartTime, @EndTime, @AssignedBy, GETDATE())";

                    int newGuardId;
                    using (var cmd = new SqlCommand(sqlInsert, conn)) {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@StartTime", startTime);
                        cmd.Parameters.AddWithValue("@EndTime", endTime);
                        cmd.Parameters.AddWithValue("@AssignedBy", assignedBy);
                        newGuardId = (int)cmd.ExecuteScalar();
                    }

                    Debug.WriteLine($"[AssignGuard] Turno creado Guard_Id={newGuardId} ✓");
                    return new GuardResult { Success = true, GuardId = newGuardId };
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[AssignGuard] ERROR: {ex.Message}");
                return new GuardResult { Success = false, Message = ex.Message };
            }
        }

        // ────────────────────────────────────────────────────────────────
        // REMOVE GUARD — elimina un turno por Guard_Id
        // ────────────────────────────────────────────────────────────────
        public GuardResult RemoveGuard(int guardId) {
            Debug.WriteLine($"[RemoveGuard] GuardId={guardId}");
            try {
                using (var conn = new SqlConnection(_connStr)) {
                    string sql = "DELETE FROM Guard_Schedule WHERE Guard_Id = @GuardId";
                    using (var cmd = new SqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("@GuardId", guardId);
                        conn.Open();
                        int rows = cmd.ExecuteNonQuery();
                        if (rows == 0)
                            return new GuardResult { Success = false, Message = "Turno no encontrado." };
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
        // Retorna emails separados por ; de Owners activos en guardia ahora
        // ────────────────────────────────────────────────────────────────
        public static string GetActiveGuardEmails() {
            try {
                string sql = @"
                    SELECT u.Email
                    FROM Guard_Schedule gs
                    INNER JOIN Users u ON gs.User_Id = u.User_Id
                    WHERE GETDATE() BETWEEN gs.Start_Time AND gs.End_Time
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
