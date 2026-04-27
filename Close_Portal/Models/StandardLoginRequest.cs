namespace Close_Portal.Models {
    /// <summary>
    /// Request para login estándar (email/password)
    /// </summary>
    public class StandardLoginRequest {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
