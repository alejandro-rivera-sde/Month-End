using Close_Portal.Core;
using Close_Portal.DataAccess;
using System.Web.Services;
using System.Web.UI;

namespace Close_Portal.Pages.Main {
    public partial class UserPrefs : Page {

        [WebMethod(EnableSession = true)]
        public static void SaveLanguage(string lang) {
            SecurePage.CheckAccess(RoleLevel.Regular);
            if (lang != "es" && lang != "en") return;
            int userId = (int)System.Web.HttpContext.Current.Session["UserId"];
            new UserDataAccess().SavePreferredLang(userId, lang);
        }
    }
}
