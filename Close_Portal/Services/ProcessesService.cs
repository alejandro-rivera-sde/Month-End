using Close_Portal.Core;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Close_Portal.Services {
    public static class ProcessesService {

        private static readonly string SmtpHost     = ConfigurationManager.AppSettings["Smtp_Host"];
        private static readonly int    SmtpPort     = int.Parse(ConfigurationManager.AppSettings["Smtp_Port"] ?? "587");
        private static readonly string SmtpUser     = ConfigurationManager.AppSettings["Smtp_User"];
        private static readonly string SmtpPassword = ConfigurationManager.AppSettings["Smtp_Password"];
        private static readonly string SmtpFrom     = ConfigurationManager.AppSettings["Smtp_From"];
        private static readonly bool   SmtpSsl      = bool.Parse(ConfigurationManager.AppSettings["Smtp_EnableSsl"] ?? "true");

        private static readonly string ConfigFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "App_Data", "processes-config.json");

        private static volatile bool   _confirmacionCierreEnabled   = false;
        private static          string _confirmacionCierreRecipient = "";
        private static readonly object _recipientLock  = new object();
        private static readonly object _configFileLock = new object();

        static ProcessesService() {
            LoadConfig();
        }

        public static bool   ConfirmacionCierreEnabled   => _confirmacionCierreEnabled;
        public static string ConfirmacionCierreRecipient {
            get { lock (_recipientLock) { return _confirmacionCierreRecipient; } }
        }

        public static void SetConfirmacionCierreEnabled(bool enabled) {
            _confirmacionCierreEnabled = enabled;
            SaveConfig();
        }

        public static void SetConfirmacionCierreRecipient(string email) {
            lock (_recipientLock) { _confirmacionCierreRecipient = email?.Trim() ?? ""; }
            SaveConfig();
        }

        private static void LoadConfig() {
            try {
                lock (_configFileLock) {
                    if (!File.Exists(ConfigFilePath)) return;
                    var json       = File.ReadAllText(ConfigFilePath);
                    var serializer = new JavaScriptSerializer();
                    var data       = serializer.Deserialize<ProcessConfig>(json);
                    if (data == null) return;
                    _confirmacionCierreEnabled = data.ConfirmacionCierreEnabled;
                    lock (_recipientLock) {
                        _confirmacionCierreRecipient = data.ConfirmacionCierreRecipient ?? "";
                    }
                }
            } catch (Exception ex) {
                AppLogger.Error("ProcessesService.LoadConfig", ex);
            }
        }

        private static void SaveConfig() {
            try {
                lock (_configFileLock) {
                    string recipient;
                    lock (_recipientLock) { recipient = _confirmacionCierreRecipient; }
                    var data = new ProcessConfig {
                        ConfirmacionCierreEnabled   = _confirmacionCierreEnabled,
                        ConfirmacionCierreRecipient = recipient
                    };
                    var serializer = new JavaScriptSerializer();
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath));
                    File.WriteAllText(ConfigFilePath, serializer.Serialize(data));
                }
            } catch (Exception ex) {
                AppLogger.Error("ProcessesService.SaveConfig", ex);
            }
        }

        // Invocado desde Guard.aspx.cs al cerrar una guardia exitosamente
        public static void TriggerConfirmacionCierre(DateTime guardStartTime) {
            if (!_confirmacionCierreEnabled) return;
            string recipient;
            lock (_recipientLock) { recipient = _confirmacionCierreRecipient; }
            if (string.IsNullOrWhiteSpace(recipient)) return;
            Task.Run(() => SendConfirmacionCierre(guardStartTime, recipient));
        }

        // Invocado desde Processes.aspx.cs por el botón Test
        public static void ForceConfirmacionCierre(DateTime testDate) {
            string recipient;
            lock (_recipientLock) { recipient = _confirmacionCierreRecipient; }
            if (string.IsNullOrWhiteSpace(recipient)) return;
            Task.Run(() => SendConfirmacionCierre(testDate, recipient));
        }

        private static void SendConfirmacionCierre(DateTime guardStartTime, string recipient) {
            try {
                using (var mail = new MailMessage()) {
                    mail.From       = new MailAddress(SmtpFrom, "no-reply");
                    mail.Subject    = "Confirmacion cierre";
                    mail.Body       = BuildBody(guardStartTime, recipient);
                    mail.IsBodyHtml = false;
                    mail.To.Add(recipient);

                    using (var smtp = new SmtpClient(SmtpHost, SmtpPort)) {
                        smtp.EnableSsl = SmtpSsl;
                        if (!string.IsNullOrWhiteSpace(SmtpUser) && !string.IsNullOrWhiteSpace(SmtpPassword))
                            smtp.Credentials = new NetworkCredential(SmtpUser, SmtpPassword);
                        else
                            smtp.UseDefaultCredentials = false;
                        smtp.Send(mail);
                    }
                }
            } catch (Exception ex) {
                AppLogger.Error("ProcessesService.SendConfirmacionCierre", ex);
            }
        }

        private static string BuildBody(DateTime guardStartTime, string recipient) {
            return
                $"Año a generar: {guardStartTime.Year}\r\n" +
                $"Periodo: {guardStartTime.Month}\r\n" +
                $"Correo: {recipient}";
        }
    }

    internal class ProcessConfig {
        public bool   ConfirmacionCierreEnabled   { get; set; }
        public string ConfirmacionCierreRecipient { get; set; }
    }
}
