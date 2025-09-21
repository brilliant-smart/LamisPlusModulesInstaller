using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LamisPlusModulesInstaller
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string baseUrl = "http://localhost:8383";
            string username = "guest@lamisplus.org";
            string password = "123456"; // TODO: pop up a password entry area in the gui version

            // Authenticate to get JWT which is gotten from lamisplus login details; username and password hardcoded above
            //TODO: There will be option to ask for password and even username later

            var authClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var loginPayload = new { username = username, password = password, rememberMe = true };
            var json = JsonSerializer.Serialize(loginPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await authClient.PostAsync("/api/v1/authenticate", content);
            var respBody = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[AUTH ERROR] Status: {(int)resp.StatusCode} {resp.StatusCode}");
                Console.WriteLine($"[AUTH ERROR BODY] {respBody}");
                return; // stop program early if login fails
            }

            using var doc = JsonDocument.Parse(respBody);
            string token = doc.RootElement.GetProperty("id_token").GetString();
            Console.WriteLine("[AUTH OK] Got JWT token");

            //Using ModuleClient with the token
            var client = new ModuleClient(baseUrl, token);

            string moduleFolder = @"C:\lamismodules"; // ✅ adjust path

            string[] order = { "patient", "triage", "lab", "biometric", "hiv", "client-syn" };

            foreach (var module in order)
            {
                var jar = Directory.GetFiles(moduleFolder, $"*{module}*.jar")[0];
                Console.WriteLine($"Installing {module}...");

                var uploadResponse = await client.UploadModuleAsync(jar);

                // passing an empty string for id until upload response is parsed
                await client.InstallModuleAsync(uploadResponse);
            }

            Console.WriteLine("=== All modules processed ===");
        }
    }
}
