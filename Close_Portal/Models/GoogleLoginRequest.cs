namespace Close_Portal.Models {
    public class GoogleLoginRequest {
        public string IdToken { get; set; }

        /// <summary>
        /// Token de invitación pendiente, leído desde Session en el WebMethod
        /// ANTES de entrar a Task.Run donde HttpContext ya no está disponible.
        /// </summary>
        public string InvitationToken { get; set; }
    }
}