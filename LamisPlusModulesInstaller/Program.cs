using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            string password = "123456"; // TODO: secure later
            string moduleFolder = @"C:\lamismodules";

            // 1. Authenticate
            var authClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var loginPayload = new { username, password, rememberMe = true };
            var json = JsonSerializer.Serialize(loginPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await authClient.PostAsync("/api/v1/authenticate", content);
            var respBody = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[AUTH ERROR] {resp.StatusCode}: {respBody}");
                return;
            }

            using var doc = JsonDocument.Parse(respBody);
            string token = doc.RootElement.GetProperty("id_token").GetString();
            Console.WriteLine("[AUTH OK] Got JWT token");

            // 2. Init client
            var client = new ModuleClient(baseUrl, token);

            // 3. Dependency dictionary
            var dependencies = new Dictionary<string, string[]>
            {
                ["Patient"] = Array.Empty<string>(),
                ["Triage"] = new[] { "Patient" },
                ["Laboratory"] = new[] { "Patient" },
                ["Biometric"] = new[] { "Patient" },
                ["HIV"] = new[] { "Patient", "Triage", "Laboratory", "Biometric" },
                ["HTS"] = new[] { "Patient" },
                ["Prep"] = Array.Empty<string>(),
                ["PMTCT"] = new[] { "HIV" },
                ["ADR"] = Array.Empty<string>(),
                ["Hepatitis"] = new[] { "Patient" },
                ["Report"] = new[] { "HIV" },
                ["NDR"] = new[] { "Patient", "Triage", "Laboratory", "HIV" },
                ["Lims"] = new[] { "Patient", "Laboratory" },
                ["Casemanager"] = new[] { "Patient" },
                ["Immunization"] = Array.Empty<string>(),
                ["MHPSS"] = new[] { "Patient" },
                ["KP_Prev"] = Array.Empty<string>(),
                ["Backup"] = Array.Empty<string>(),
                ["Client-sync"] = Array.Empty<string>()
            };

            var installedModules = new HashSet<string>();

            // 4. Install modules dependency-aware
            foreach (var module in dependencies.Keys)
            {
                var deps = dependencies[module];

                if (!deps.All(d => installedModules.Contains(d)))
                {
                    Console.WriteLine($"⏩ Skipping {module}, dependencies not satisfied: {string.Join(", ", deps)}");
                    continue;
                }

                Console.WriteLine($"Installing {module}...");

                var jar = Directory.GetFiles(moduleFolder, $"*{module.ToLower()}*.jar")[0];
                var uploadResponse = await client.UploadModuleAsync(jar);

                //changed in order to install with proper API call
                var installResponse = await client.InstallModuleAsync(uploadResponse);

                // changed from 'installResponse.Type == "SUCCESS"' to 'installResponse != null && installResponse.Type == "SUCCESS"'
                if (installResponse != null && installResponse.Type == "SUCCESS")
                {
                    installedModules.Add(module);
                    Console.WriteLine($"✅ Installed {module}");
                }
                else
                {
                    Console.WriteLine($"Failed to install {module}");
                }
            }

            Console.WriteLine("=== All modules processed ===");
        }
    }
}
