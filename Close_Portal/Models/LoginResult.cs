using System;

namespace Close_Portal.Models {
    /// <summary>
    /// Resultado de cualquier operación de login (tradicional o Google)
    /// </summary>
    public class LoginResult {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? UserId { get; set; }
        public int? RoleId { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string RoleName { get; set; }
    }
}