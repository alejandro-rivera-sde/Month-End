namespace Close_Portal.Services {
    public sealed class EmailAlertStyle {
        public string Color { get; }
        public string Icon { get; } // Se mantiene por compatibilidad, pero no se usará en el HTML

        public EmailAlertStyle(string color, string icon = "") {
            Color = color;
            Icon = icon;
        }
    }

    public static class AlertStyles {
        // Usamos una paleta de colores más sobria (Slate, Emerald, Rose, Amber, Indigo)

        // ── Usuarios ──────────────────────────────────────────────────────
        public static readonly EmailAlertStyle UserAdded = new EmailAlertStyle("#10b981"); // Success
        public static readonly EmailAlertStyle UserRemoved = new EmailAlertStyle("#e11d48"); // Danger
        public static readonly EmailAlertStyle UserUpdated = new EmailAlertStyle("#6366f1"); // Info
        public static readonly EmailAlertStyle UserBlocked = new EmailAlertStyle("#475569"); // Neutral/Dark
        public static readonly EmailAlertStyle UserUnblocked = new EmailAlertStyle("#10b981");

        // ── Guardia ───────────────────────────────────────────────────────
        public static readonly EmailAlertStyle GuardDraft = new EmailAlertStyle("#f59e0b"); // Warning
        public static readonly EmailAlertStyle GuardDraftCancelled = new EmailAlertStyle("#e11d48");
        public static readonly EmailAlertStyle GuardStarted = new EmailAlertStyle("#f59e0b");
        public static readonly EmailAlertStyle GuardConfirmed = new EmailAlertStyle("#10b981");
        public static readonly EmailAlertStyle GuardCancelled = new EmailAlertStyle("#e11d48");
        public static readonly EmailAlertStyle GuardClosed = new EmailAlertStyle("#6366f1");
        public static readonly EmailAlertStyle GuardDraftReminder = new EmailAlertStyle("#f59e0b");

        // ── Cierres ───────────────────────────────────────────────────────
        public static readonly EmailAlertStyle ClosureRequest = new EmailAlertStyle("#6366f1");
        public static readonly EmailAlertStyle LocationReverted = new EmailAlertStyle("#f59e0b"); // Warning
        public static readonly EmailAlertStyle DefaultSpotReminder = new EmailAlertStyle("#f59e0b");

        public static EmailAlertStyle ClosureResponse(bool approved) =>
            approved ? new EmailAlertStyle("#10b981") : new EmailAlertStyle("#e11d48");
    }
}