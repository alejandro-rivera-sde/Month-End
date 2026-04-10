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

            // Timer de auto-asignación: asigna usuario default a spots vacíos cada 2 min
            _autoAssignTimer = new Timer(2 * 60 * 1000); // 2 minutos
            _autoAssignTimer.Elapsed += (s, e) => {
                try {
                    var affected = DataAccess.GuardDataAccess.AutoAssignDefaultSpots();
                    foreach (var guardId in affected)
                        Hubs.LocationHub.NotifySpotChanged(guardId);
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"[Startup] AutoAssign ERROR: {ex.Message}");
                }
            };
            _autoAssignTimer.AutoReset = true;
            _autoAssignTimer.Start();

            System.Diagnostics.Debug.WriteLine("[Startup] Auto-assign timer iniciado (cada 2 min).");
        }
    }
}