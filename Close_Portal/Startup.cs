using Microsoft.Owin;
using Owin;
using System.Timers;

[assembly: OwinStartup(typeof(Close_Portal.Startup))]

namespace Close_Portal {
    public class Startup {

        // Timer estático para que no sea recolectado por el GC
        private static Timer _reminderTimer;

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
        }
    }
}