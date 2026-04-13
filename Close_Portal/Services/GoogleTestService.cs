using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Close_Portal.Services {
    public class GoogleTestService {
        public static async Task<string> TestGoogleConnection() {
            try {
                using (HttpClient client = new HttpClient()) {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var response = await client.GetAsync("https://www.googleapis.com/oauth2/v3/certs");

                    if (response.IsSuccessStatusCode) {
                        return "EXITO - Conexion a Google APIs funciona";
                    } else {
                        return $"ERROR HTTP: {response.StatusCode}";
                    }
                }
            } catch (TaskCanceledException) {
                return "TIMEOUT - Probablemente bloqueado por firewall";
            } catch (HttpRequestException ex) {
                return $"ERROR DE RED: {ex.Message}";
            } catch (Exception ex) {
                return $"ERROR: {ex.Message}";
            }
        }
    }
}