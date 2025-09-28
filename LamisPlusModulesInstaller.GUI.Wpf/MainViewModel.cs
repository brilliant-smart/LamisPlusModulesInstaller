using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;

namespace LamisPlusModulesInstaller.GUI.Wpf
{
    public partial class MainViewModel : ObservableObject
    {
        private ModuleClient _client;

        [ObservableProperty] private string baseUrl = "http://localhost:8383";
        [ObservableProperty] private string username = "guest@lamisplus.org";
        [ObservableProperty] private string password;
        [ObservableProperty] private string authStatus = "Not logged in";
        [ObservableProperty] private string logs = "";

        public ObservableCollection<ModuleViewModel> Modules { get; } = new();

        // Dependency map to enforce installation in dependency aware order
        private readonly Dictionary<string, string[]> dependencies =
            new(StringComparer.OrdinalIgnoreCase)
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

        public MainViewModel()
        {
            _client = new ModuleClient(BaseUrl, "");

            // 🔍 Load local JARs immediately
            LoadLocalModules();
        }

        private void LoadLocalModules()
        {
            try
            {
                string modulesDir = @"C:\lamismodules";
                Modules.Clear();

                if (Directory.Exists(modulesDir))
                {
                    var moduleFiles = Directory.GetFiles(modulesDir, "*.jar");
                    foreach (var file in moduleFiles)
                    {
                        Modules.Add(new ModuleViewModel
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            LocalVersion = ExtractVersionFromFilename(file),
                            InstalledVersion = "(unknown)",
                            Status = "Pending",
                            LocalPath = file
                        });
                    }
                }
                else
                {
                    AppendLog($"⚠️ Directory not found: {modulesDir}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️ Error scanning modules: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task LoginAsync(System.Windows.Controls.PasswordBox passwordBox)
        {
            try
            {
                Password = passwordBox.Password;
                var auth = new AuthHelper(BaseUrl);
                var token = await auth.LoginAsync(Username, Password);
                _client = new ModuleClient(BaseUrl, token);

                AuthStatus = "✅ Authenticated";
                AppendLog("Login successful.");

                // 🔍 After login, sync server-installed modules
                await RefreshInstalledVersionsAsync();
            }
            catch (Exception ex)
            {
                AuthStatus = $"❌ Login failed: {ex.Message}";
                AppendLog(AuthStatus);
            }
        }

        /// <summary>
        /// Calls server to refresh installed versions in the DataGrid.
        /// </summary>
        private async Task RefreshInstalledVersionsAsync()
        {
            try
            {
                var installed = await _client.GetInstalledModulesAsync();

                foreach (var module in Modules)
                {
                    var match = installed.FirstOrDefault(m =>
                        m.Name.Contains(module.Name, StringComparison.OrdinalIgnoreCase) ||
                        module.Name.Contains(m.Name, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        module.InstalledVersion = match.Version ?? "?";
                        module.Status = "Installed";
                    }
                    else
                    {
                        module.InstalledVersion = "(not installed)";
                    }
                }

                AppendLog("🔄 Installed module versions updated from server.");
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️ Failed to fetch installed modules: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task InstallAllAsync()
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in dependencies)
            {
                string moduleKey = kvp.Key;
                string[] deps = kvp.Value;

                // Check dependencies are satisfied
                if (!deps.All(d => installed.Contains(d)))
                {
                    AppendLog($"⏩ Skipping {moduleKey}, dependencies not satisfied: {string.Join(", ", deps)}");
                    continue;
                }

                // Match the module in our Modules collection
                var module = Modules.FirstOrDefault(m =>
                    m.Name.Contains(moduleKey, StringComparison.OrdinalIgnoreCase));

                if (module == null)
                {
                    AppendLog($"❌ No local JAR found for {moduleKey}");
                    continue;
                }

                await InstallModuleAsync(module);

                if (module.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    installed.Add(moduleKey);
                }
            }

            await RefreshInstalledVersionsAsync();
        }


        [RelayCommand]
        private async Task InstallSelectedAsync()
        {
            foreach (var module in Modules)
            {
                if (module.IsSelected)
                    await InstallModuleAsync(module);
            }

            await RefreshInstalledVersionsAsync();
        }

        [RelayCommand]
        private async Task RetryFailedAsync()
        {
            foreach (var module in Modules)
            {
                if (module.Status.Contains("Failed"))
                    await InstallModuleAsync(module);
            }

            await RefreshInstalledVersionsAsync();
        }

        private async Task InstallModuleAsync(ModuleViewModel module)
        {
            try
            {
                AppendLog($"Installing {module.Name}...");

                var uploadResp = await _client.UploadModuleAsync(module.LocalPath);
                var result = await _client.InstallModuleAsync(uploadResp);

                module.Status = result?.Type ?? "Failed";
                module.InstalledVersion = result?.Module?.Version ?? "";
                AppendLog($"[{module.Name}] {result?.Message}");
            }
            catch (Exception ex)
            {
                module.Status = "Failed";
                AppendLog($"[{module.Name}] Exception: {ex.Message}");
            }
        }

        private void AppendLog(string message)
        {
            Logs += $"{DateTime.Now:HH:mm:ss} {message}\n";
        }

        // 📌 Extracts "2.1.1" from "patient-2.1.1.jar"
        private string ExtractVersionFromFilename(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var parts = name.Split('-');
            if (parts.Length > 1)
                return parts[^1];
            return "?";
        }
    }
}
