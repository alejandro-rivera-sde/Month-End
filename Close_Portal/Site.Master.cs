using System;
using System.IO;
using System.Web.UI;

namespace Close_Portal {
    public partial class SiteMaster : System.Web.UI.MasterPage {

        private static readonly string _appVersion = ComputeVersion();

        private static string ComputeVersion() {
            try {
                string dll = System.Reflection.Assembly.GetExecutingAssembly().Location;
                return File.GetLastWriteTimeUtc(dll).ToString("yyyyMMddHHmm");
            } catch {
                return DateTime.UtcNow.ToString("yyyyMMddHH");
            }
        }

        public static string AppVersion => _appVersion;

        protected void Page_Load(object sender, EventArgs e) { }
    }
}