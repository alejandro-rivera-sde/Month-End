namespace Close_Portal.Services {

    public sealed class EmailAlertStyle {
        public string Color { get; }
        public string Icon  { get; }

        public EmailAlertStyle(string color, string icon) {
            Color = color;
            Icon  = icon;
        }
    }

    public static class AlertStyles {

        // ── Usuarios ──────────────────────────────────────────────────────
        public static readonly EmailAlertStyle UserAdded     = new EmailAlertStyle("#10b981", "&#10010;");
        public static readonly EmailAlertStyle UserRemoved   = new EmailAlertStyle("#ef4444", "&#10006;");
        public static readonly EmailAlertStyle UserUpdated   = new EmailAlertStyle("#6366f1", "&#9998;" );
        public static readonly EmailAlertStyle UserBlocked   = new EmailAlertStyle("#ef4444", "&#128274;");
        public static readonly EmailAlertStyle UserUnblocked = new EmailAlertStyle("#10b981", "&#128275;");

        // ── Guardia ───────────────────────────────────────────────────────
        public static readonly EmailAlertStyle GuardDraft          = new EmailAlertStyle("#f59e0b", "&#128197;");
        public static readonly EmailAlertStyle GuardDraftCancelled = new EmailAlertStyle("#ef4444", "&#10006;" );
        public static readonly EmailAlertStyle GuardStarted        = new EmailAlertStyle("#f59e0b", "&#128737;");
        public static readonly EmailAlertStyle GuardConfirmed      = new EmailAlertStyle("#10b981", "&#128737;");
        public static readonly EmailAlertStyle GuardCancelled      = new EmailAlertStyle("#ef4444", "&#10006;" );
        public static readonly EmailAlertStyle GuardClosed         = new EmailAlertStyle("#6366f1", "&#128274;");
        public static readonly EmailAlertStyle GuardDraftReminder  = new EmailAlertStyle("#f59e0b", "&#9201;" );

        // ── Cierres ───────────────────────────────────────────────────────
        public static readonly EmailAlertStyle ClosureRequest      = new EmailAlertStyle("#6366f1", "&#128274;");
        public static readonly EmailAlertStyle DefaultSpotReminder = new EmailAlertStyle("#f59e0b", "&#9888;" );

        // ── Dinámico (depende del resultado) ─────────────────────────────
        public static EmailAlertStyle ClosureResponse(bool approved) =>
            approved
                ? new EmailAlertStyle("#10b981", "&#10003;")
                : new EmailAlertStyle("#ef4444", "&#10006;");
    }
}
