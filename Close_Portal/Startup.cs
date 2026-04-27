using Close_Portal.Core;
using Microsoft.Owin;
using Owin;
using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

[assembly: OwinStartup(typeof(Close_Portal.Startup))]

namespace Close_Portal {
    public class Startup {

        // Timers estáticos para que no sean recolectados por el GC
        private static Timer _reminderTimer;
        private static Timer _autoAssignTimer;
        private static Timer _preciseAssignTimer;
        private static readonly object _preciseLock = new object();

        // ── CORS: orígenes permitidos desde web.config ────────────────────────
        // Agrega en web.config → appSettings:
        //   <add key="AllowedCorsOrigins" value="https://apps.novamex.com" />
        // Múltiples orígenes separados por coma para entornos dev/staging.
        private static string[] GetAllowedOrigins() {
            string cfg = ConfigurationManager.AppSettings["AllowedCorsOrigins"] ?? "";
            return cfg.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(o => o.Trim())
                      .Where(o => !string.IsNullOrEmpty(o))
                      .ToArray();
        }

        public void Configuration(IAppBuilder app) {
            // CORS debe ir ANTES de MapSignalR para que aplique a las
            // negociaciones de transporte de SignalR (/signalr/negotiate, etc.)
            app.Use(async (ctx, next) => {
                string   origin  = ctx.Request.Headers.Get("Origin");
                string[] allowed = GetAllowedOrigins();

                if (!string.IsNullOrEmpty(origin) &&
                    allowed.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase))) {
                    ctx.Response.Headers.Set("Access-Control-Allow-Origin",      origin);
                    ctx.Response.Headers.Set("Access-Control-Allow-Credentials", "true");
                    ctx.Response.Headers.Set("Access-Control-Allow-Headers",     "Content-Type, X-Requested-With");
                    ctx.Response.Headers.Set("Access-Control-Allow-Methods",     "GET, POST, OPTIONS");
                    ctx.Response.Headers.Set("Vary",                             "Origin");
                }

                if (ctx.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase)) {
                    ctx.Response.StatusCode = 200;
                    return;
                }

                await next();
            });

            app.MapSignalR();

            Services.EmailService.LoadConfig();

            _reminderTimer = new Timer(15 * 60 * 1000);
            _reminderTimer.Elapsed += (s, e) => Services.EmailService.CheckDraftReminders();
            _reminderTimer.AutoReset = true;
            _reminderTimer.Start();

            _autoAssignTimer = new Timer(2 * 60 * 1000);
            _autoAssignTimer.Elapsed += (s, e) => RunAutoAssign();
            _autoAssignTimer.AutoReset = true;
            _autoAssignTimer.Start();

            ScheduleNextPreciseAssign();
        }

        // ── Ejecuta la asignación y notifica via SignalR ──────────────────
        private static void RunAutoAssign() {
            try {
                var affected = DataAccess.GuardDataAccess.AutoAssignDefaultSpots();
                foreach (var guardId in affected)
                    Hubs.LocationHub.NotifySpotChanged(guardId);
            } catch (Exception ex) {
                AppLogger.Error("Startup.RunAutoAssign", ex);
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

                    if (next == null) return;

                    // +2 s de margen para que GETDATE() en SQL ya haya pasado el Start_Time
                    double delayMs = (next.Value - DateTime.Now).TotalMilliseconds + 2000;
                    if (delayMs < 100) delayMs = 100;

                    _preciseAssignTimer = new Timer(delayMs) { AutoReset = false };
                    _preciseAssignTimer.Elapsed += (s, e) => {
                        RunAutoAssign();
                        ScheduleNextPreciseAssign();
                    };
                    _preciseAssignTimer.Start();
                }
            } catch (Exception ex) {
                AppLogger.Error("Startup.ScheduleNextPreciseAssign", ex);
            }
        }
    }
}
