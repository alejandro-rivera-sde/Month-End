using Microsoft.Owin;
using Owin;
using System;
using System.Timers;

[assembly: OwinStartup(typeof(Close_Portal.Startup))]

namespace Close_Portal {
    public class Startup {

        // Timers estáticos para que no sean recolectados por el GC
        private static Timer _reminderTimer;
        private static Timer _autoAssignTimer;
        private static Timer _preciseAssignTimer;
        private static readonly object _preciseLock = new object();

        public void Configuration(IAppBuilder app) {
            app.MapSignalR();

            // Cargar configuración de email desde DB
            Services.EmailService.LoadConfig();

            // Timer de reminder: verifica guardias draft sin confirmar cada 15 min
            _reminderTimer = new Timer(15 * 60 * 1000); // 15 minutos
            _reminderTimer.Elapsed += (s, e) => Services.EmailService.CheckDraftReminders();
            _reminderTimer.AutoReset = true;
            _reminderTimer.Start();

            System.Diagnostics.Debug.WriteLine("[Startup] Reminder timer iniciado (cada 15 min).");

            // Timer de seguridad: red de respaldo cada 2 min por si el timer preciso falla
            _autoAssignTimer = new Timer(2 * 60 * 1000); // 2 minutos
            _autoAssignTimer.Elapsed += (s, e) => RunAutoAssign();
            _autoAssignTimer.AutoReset = true;
            _autoAssignTimer.Start();

            System.Diagnostics.Debug.WriteLine("[Startup] Auto-assign timer de seguridad iniciado (cada 2 min).");

            // Programar el primer timer preciso al arrancar la app
            ScheduleNextPreciseAssign();
        }

        // ── Ejecuta la asignación y notifica via SignalR ──────────────────
        private static void RunAutoAssign() {
            try {
                var affected = DataAccess.GuardDataAccess.AutoAssignDefaultSpots();
                foreach (var guardId in affected)
                    Hubs.LocationHub.NotifySpotChanged(guardId);
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[Startup] AutoAssign ERROR: {ex.Message}");
            }
        }

        // ── Timer preciso: se dispara exactamente en Start_Time ───────────
        // Público para que Guard.aspx.cs lo llame al confirmar una guardia.
        public static void ScheduleNextPreciseAssign() {
            try {
                DateTime? next = DataAccess.GuardDataAccess.GetNextPendingAutoAssignTime();

                lock (_preciseLock) {
                    _preciseAssignTimer?.Stop();
                    _preciseAssignTimer?.Dispose();
                    _preciseAssignTimer = null;

                    if (next == null) {
                        System.Diagnostics.Debug.WriteLine("[Startup] Sin guardias futuras pendientes de auto-asignación.");
                        return;
                    }

                    // +2 s de margen para que GETDATE() en SQL ya haya pasado el Start_Time
                    double delayMs = (next.Value - DateTime.Now).TotalMilliseconds + 2000;
                    if (delayMs < 100) delayMs = 100; // si ya pasó, disparar casi de inmediato

                    _preciseAssignTimer = new Timer(delayMs) { AutoReset = false };
                    _preciseAssignTimer.Elapsed += (s, e) => {
                        RunAutoAssign();
                        ScheduleNextPreciseAssign(); // reprogramar para la siguiente guardia
                    };
                    _preciseAssignTimer.Start();

                    System.Diagnostics.Debug.WriteLine(
                        $"[Startup] Timer preciso programado para {next.Value:G} (en {delayMs / 1000:F0} s).");
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[Startup] ScheduleNextPreciseAssign ERROR: {ex.Message}");
            }
        }
    }
}