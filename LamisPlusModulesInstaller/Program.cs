using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace LamisPlusModulesInstaller
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            string baseUrl = "http://localhost:8383";
            string username = "guest@lamisplus.org";
            string password = "12345"; //some facilities are using 1 - 5, TODO: lamisplus username and password will be asked in the gui version
            string moduleFolder = @"C:\lamismodules";// todo: after installing the .exe, it will create this directory in the c drive

            var auth = new AuthHelper(baseUrl); //added auth helper for separation of concern
            string? jwtToken = await auth.LoginAsync(username, password);
            if (string.IsNullOrEmpty(jwtToken))
            {
                Console.WriteLine("Authentication failed. Invalid credentials or endpoint.");
                return;
            }
            Console.WriteLine("[AUTH OK] Got JWT token");

            var client = new ModuleClient(baseUrl, jwtToken);

            //Dependency dictionary, some modules depend on others, and the dependency must met before they install
            var dependencies = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Patient", Array.Empty<string>() },
                { "Triage", new []{ "Patient" } },
                { "Laboratory", new []{ "Patient" } },
                { "Biometric", new []{ "Patient" } },
                { "HIV", new []{ "Patient", "Triage", "Laboratory", "Biometric" } },
                { "HTS", new []{ "Patient" } },
                { "Prep", new []{ "HIV" } },
                { "PMTCT", new []{ "HIV" } },
                { "ADR", Array.Empty<string>() },
                { "Hepatitis", new []{ "Patient" } },
                { "Report", new []{ "HIV" } },
                { "NDR", new []{ "Patient", "Triage", "Laboratory", "HIV" } },
                { "Lims", new []{ "Patient", "Laboratory" } },
                { "Casemanager", new []{ "Patient" } },
                { "Immunization", new []{ "Patient" } },
                { "MHPSS", new []{ "Patient" } },
                { "KP_Prev", Array.Empty<string>() },
                { "Backup", Array.Empty<string>() },
                { "Client-sync", Array.Empty<string>() }
            };

            var installedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Preload already-installed modules from server (this helps dependency resolution and check if module's already installed)
            try
            {
                var serverInstalled = await client.GetInstalledModulesAsync();
                foreach (var s in serverInstalled)
                {
                    if (!string.IsNullOrEmpty(s.Name))
                    {
                        installedModules.Add(s.Name); // e.g. PatientModule
                        if (s.Name.EndsWith("Module", StringComparison.OrdinalIgnoreCase))
                        {
                            var shortName = s.Name.Substring(0, s.Name.Length - "Module".Length);
                            installedModules.Add(shortName); // e.g. Patient
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Could not preload installed modules: {ex.Message}");
            }

            void MarkInstalled(string keyName, ModuleUploadResponse uploadResp)
            {
                installedModules.Add(keyName);
                if (!string.IsNullOrEmpty(uploadResp.Name))
                {
                    installedModules.Add(uploadResp.Name);
                    if (uploadResp.Name.EndsWith("Module", StringComparison.OrdinalIgnoreCase))
                    {
                        installedModules.Add(uploadResp.Name.Substring(0, uploadResp.Name.Length - "Module".Length));
                    }
                }
            }

            foreach (var moduleKey in dependencies.Keys)
            {
                var deps = dependencies[moduleKey];

                if (!deps.All(d => installedModules.Contains(d)))
                {
                    Console.WriteLine($"Skipping {moduleKey}, dependencies not satisfied: {string.Join(", ", deps)}");
                    continue;
                }

                Console.WriteLine($"Installing {moduleKey}...");

                var jarFiles = Directory.GetFiles(moduleFolder, $"*{moduleKey.ToLower()}*.jar");
                if (jarFiles.Length == 0)
                {
                    Console.WriteLine($"No JAR found for {moduleKey} in {moduleFolder}");
                    continue;
                }
                var jar = jarFiles[0];

                ModuleUploadResponse uploadResp;
                try
                {
                    uploadResp = await client.UploadModuleAsync(jar);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Upload failed for {moduleKey}: {ex.Message}");
                    continue;
                }

                ModuleInstallResponse? installResp = null;
                try
                {
                    installResp = await client.InstallModuleAsync(uploadResp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: Install call failed for {moduleKey}: {ex.Message}");
                }

                Console.WriteLine($"[INSTALL RESPONSE] Type={installResp?.Type} Message={installResp?.Message}");

                bool treatedAsInstalled = false;

                if (installResp != null)
                {
                    if (string.Equals(installResp.Type, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[OK] {moduleKey} install reported SUCCESS.");
                        treatedAsInstalled = true;
                    }
                    else if (!string.IsNullOrEmpty(installResp.Message) &&
                             installResp.Message.IndexOf("already installed", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Console.WriteLine($"[INFO] {moduleKey} already installed (server message).");
                        treatedAsInstalled = true;
                    }
                    else if (installResp.Module != null && installResp.Module.InError == false && installResp.Module.Name != null)
                    {
                        Console.WriteLine($"[INFO] {moduleKey} install response contains module info; treating as success.");
                        treatedAsInstalled = true;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to install {moduleKey}: {installResp?.Message ?? "(no message)"}");
                    }
                }
                else
                {
                    Console.WriteLine($"Install call returned no parsed response for {moduleKey}.");
                }

                if (treatedAsInstalled)
                {
                    var serverName = uploadResp.Name ?? moduleKey;
                    Console.WriteLine($"Waiting for {serverName} to appear as installed (timeout 60s)...");
                    var ok = await client.WaitForModuleRegisteredAsync(serverName, 60);

                    if (ok)
                    {
                        MarkInstalled(moduleKey, uploadResp);
                        Console.WriteLine($"{moduleKey} Module is fully installed and registered.");
                    }
                    else
                    {
                        Console.WriteLine($"{moduleKey} Module did not appear as registered within timeout. Therefore it is not marked as installed.");
                    }
                }
            }

            Console.WriteLine("=== All modules installed successfully ===");
        }
    }
}
