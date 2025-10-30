using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using WinForms = System.Windows.Forms;
using WpfApp = System.Windows;



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
        [ObservableProperty] private bool isAuthenticated = false;
        [ObservableProperty] private bool isInstalling = false;
        [ObservableProperty] private string modulesFolder = @"C:\lamismodules";
        //progress bar
        [ObservableProperty] private int totalModules = 0;
        [ObservableProperty] private int completedModules = 0;
        [ObservableProperty] private int installProgress = 0;
        [ObservableProperty] private string progressText = "";


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

        //constructor updated to warn the user if the default folder does not exist
        //A method to ensure that the modules folder exists during runtime is created.
        //So, modules check control flow are moved into it, and the method is now called inside the constructor
        public MainViewModel()
        {
            _client = new ModuleClient(BaseUrl, "");

            EnsureModulesFolderExists(); //Method that checks if default modules folder exists else create it


        }

        //this method checks if default modules folder exists. It was inside the MainViewModel Contructor
        private void EnsureModulesFolderExists()
        {
            try
            {
                if (!Directory.Exists(ModulesFolder))
                {
                    var message =
                        $"The default modules folder 📁 'lamismodules' was not found at:\n\n" +
                        $"{ModulesFolder}\n\n" +
                        "Would you like to create it now?\n\n" +
                        "After the folder is created, please copy all the modules (.jar) files " +
                        "into it before installing.";

                    var result = System.Windows.MessageBox.Show(
                        message,
                        "Default Modules Folder is Missing",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question
                    );

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        Directory.CreateDirectory(ModulesFolder);
                        AppendLog($"✅ Created modules folder: {ModulesFolder}");
                        System.Windows.MessageBox.Show(
                            $"A folder named 'lamismodules' has been created in your Local Disk (C:).\n\n" +
                            $"📂 Location: {ModulesFolder}\n\n" +
                            $"Please copy the newly released module files into this folder before proceeding with the installation.\n\n" +
                            $"Idan baku gane ba, ku kira H.I",
                            "Folder Created",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information
                        );

                        // ✅ Load immediately after creation
                        LoadLocalModules();
                    }
                    else
                    {
                        AppendLog("⚠️ Modules folder missing — please use the '📂 Select Modules Folder' button to choose a location for .jar files.");
                    }
                }
                else
                {
                    // if the folder already exists and there are modules inside load them immediatly
                    //they were not loading earlier until reselected. That's why this else is added.
                    AppendLog($"📁 Found existing modules folder: {ModulesFolder}");
                    LoadLocalModules();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️ Error verifying modules folder: {ex.Message}");
            }
        }


        //Added browse folder to select modules folder
        [RelayCommand]
        private void BrowseModulesFolder()
        {
            var dialog = new WinForms.FolderBrowserDialog();
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                ModulesFolder = dialog.SelectedPath;
                AppendLog($"📂 Selected modules folder: {ModulesFolder}");
                LoadLocalModules();
            }
        }



        //This method replaces the method that loads the modules without sorting them.
        //This method sorts the modules based on the dependancy and installation hierachy 
        private void LoadLocalModules()
        {
            try
            {
                Modules.Clear();

                if (Directory.Exists(ModulesFolder))
                {
                    var moduleFiles = Directory.GetFiles(ModulesFolder, "*.jar");

                    // Create temporary list and sort
                    var unsortedModules = moduleFiles
                        .Select(file => new ModuleViewModel
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            LocalVersion = ExtractVersionFromFilename(file),
                            InstalledVersion = "(unknown)",
                            Status = "Pending",
                            LocalPath = file
                        })
                        .ToList();

                    // Sort based on dependency dictionary order
                    var orderedModules = unsortedModules
                        .OrderBy(m =>
                        {
                            // find key name in dependency dictionary
                            var key = dependencies.Keys
                                .FirstOrDefault(k => m.Name.Contains(k, StringComparison.OrdinalIgnoreCase));

                            // use index in dictionary as priority
                            return key != null ? dependencies.Keys.ToList().IndexOf(key) : int.MaxValue;
                        })
                        .ToList();

                    // add to observable collection in proper order
                    foreach (var mod in orderedModules)
                        Modules.Add(mod);
                }
                else
                {
                    AppendLog($"Error Directory not found: {ModulesFolder}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error scanning modules: {ex.Message}");
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

                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new Exception("Invalid Username or Password.");
                }

                _client = new ModuleClient(BaseUrl, token);

                AuthStatus = "✅ Authenticated";
                IsAuthenticated = true;//if authenticated, the install buttons will be enabled
                AppendLog("Login successful.");

                EnsureModulesFolderExists();
                await RefreshInstalledVersionsAsync();
            }
            catch (HttpRequestException ex)
            {
                AuthStatus = "❌ Authentication failed — invalid credentials or server unreachable.";
                IsAuthenticated = false;
                AppendLog($"Login failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                AuthStatus = $"❌ Login failed: {ex.Message}";
                IsAuthenticated = false;//if login failed, keep the install buttons disabled
                AppendLog(AuthStatus);
            }
        }


        /// <summary>
        /// Calls server to refresh installed versions in the DataGrid.
        /// Matches local JARs with server modules intelligently (e.g., patient ↔ PatientModule).
        /// </summary>
        private async Task RefreshInstalledVersionsAsync()
        {
            try
            {
                AppendLog("🔄 Fetching installed modules from server...");

                var installed = await _client.GetInstalledModulesAsync();

                if (installed == null || installed.Count == 0)
                {
                    AppendLog("⚠️ No installed modules found on server.");
                    foreach (var module in Modules)
                        module.InstalledVersion = "Not Installed";
                    return;
                }

                AppendLog($"✅ Found {installed.Count} modules from server.");

                foreach (var module in Modules)
                {
                    string localName = module.Name
                        .Replace("-", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("_", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("module", "", StringComparison.OrdinalIgnoreCase)
                        .ToLower();

                    // Smart match: try to find the most similar server module
                    var match = installed.FirstOrDefault(m =>
                    {
                        if (string.IsNullOrWhiteSpace(m.Name))
                            return false;

                        var remoteName = m.Name
                            .Replace("-", "", StringComparison.OrdinalIgnoreCase)
                            .Replace("_", "", StringComparison.OrdinalIgnoreCase)
                            .Replace("module", "", StringComparison.OrdinalIgnoreCase)
                            .ToLower();

                        return localName == remoteName || remoteName.Contains(localName) || localName.Contains(remoteName);
                    });

                    //this block displays the color of the modules in the modules grid after installed as it displays before installing any
                    if (match != null)
                    {
                        module.InstalledVersion = string.IsNullOrWhiteSpace(match.Version)
                            ? "(unknown)"
                            : match.Version;

                        // 🔹 Use SUCCESS to trigger your green color automatically
                        module.Status = "SUCCESS";

                        AppendLog($"✅ Found {module.Name} installed on server (v{module.InstalledVersion})");
                    }
                    else
                    {
                        module.InstalledVersion = "Not Installed";
                        module.Status = "Not Installed"; // separates the color of pending and not installed
                    }

                }

                AppendLog("🔁 Installed module versions updated successfully.");
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️ Failed to fetch installed modules: {ex.Message}");
            }
        }



        [RelayCommand]
        private async Task InstallAllAsync()
        {
            IsInstalling = true;
            try
            {
                var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                //added for progress bar with percentage
                TotalModules = dependencies.Count;
                CompletedModules = 0;

                foreach (var kvp in dependencies)
                {
                    string moduleKey = kvp.Key;
                    string[] deps = kvp.Value;

                    // Skip if dependencies not ready
                    if (!deps.All(d => installed.Contains(d)))
                    {
                        AppendLog($"⏩ Skipping {moduleKey}, dependencies not satisfied: {string.Join(", ", deps)}");
                        CompletedModules++;
                        UpdateProgress();
                        continue;
                    }

                    var module = Modules.FirstOrDefault(m =>
                        m.Name.Contains(moduleKey, StringComparison.OrdinalIgnoreCase));

                    if (module == null)
                    {
                        AppendLog($"❌ No local JAR found for {moduleKey}");
                        CompletedModules++;
                        UpdateProgress();
                        continue;
                    }

                    await InstallModuleAsync(module);

                    if (module.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                        installed.Add(moduleKey);

                    CompletedModules++;
                    UpdateProgress();
                }

                await RefreshInstalledVersionsAsync();
            }
            finally
            {
                IsInstalling = false;
                UpdateProgress(true); // mark complete
            }
        }

        //Helper method for install asyn method and improved progress bar
        private void UpdateProgress(bool finished = false)
        {
            if (TotalModules == 0)
            {
                InstallProgress = 0;
                ProgressText = "";
                return;
            }

            if (finished)
            {
                InstallProgress = 100;
                ProgressText = $"({CompletedModules}/{TotalModules}) Modules installed successfully";
            }
            else
            {
                InstallProgress = (int)((double)CompletedModules / TotalModules * 100);
                ProgressText = $"Installing {CompletedModules}/{TotalModules} ({InstallProgress}%)";
            }
        }


        // install selected module
        [RelayCommand]
        private async Task InstallSelectedAsync()
        {
            var selectedModules = Modules.Where(m => m.IsSelected).ToList();

            if (!selectedModules.Any())
            {
                AppendLog("⚠️ No modules selected for installation.");
                return;
            }

            AppendLog($"🧩 Starting installation for {selectedModules.Count} selected module(s)...");

            // Normalization helper
            string Normalize(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                s = s.ToLowerInvariant().Trim();
                s = s.Replace("-", "")
                    .Replace("_", "")
                    .Replace("module", "")
                    .Replace("mod", "")
                    .Replace("lamis", "")
                    .Replace("plus", "")
                    .Replace(" ", "");
                // remove numeric suffixes like "21", "204", etc.
                s = new string(s.TakeWhile(c => !char.IsDigit(c)).ToArray());
                return s;
            }

            // Normalize dependency dictionary 
            var normalizedDeps = dependencies.ToDictionary(
                d => Normalize(d.Key),
                d => d.Value.Select(Normalize).ToArray(),
                StringComparer.OrdinalIgnoreCase
            );

            // fetch installed modules from the server at start
            var installed = (await _client.GetInstalledModulesAsync())
                .Select(m => Normalize(m.Name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            AppendLog($"🔍 Installed modules (normalized): {string.Join(", ", installed)}");

            var newlyInstalled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var successCount = 0;
            var failedCount = 0;
            var skippedCount = 0;

            foreach (var module in selectedModules)
            {
                var cleanName = Normalize(module.Name);
                var key = normalizedDeps.Keys.FirstOrDefault(k => cleanName.Contains(k));
                var deps = key != null ? normalizedDeps[key] : Array.Empty<string>();

                AppendLog($"⚙️ Checking dependencies for: {module.Name}");

                var missing = deps
                    .Where(d => !installed.Contains(d) && !newlyInstalled.Contains(d))
                    .ToList();

                if (missing.Any())
                {
                    AppendLog($"❌ Skipping {module.Name} — missing dependencies: {string.Join(", ", missing)}");
                    module.Status = $"Skipped (Missing: {string.Join(", ", missing)})";
                    skippedCount++;
                    continue;
                }

                try
                {
                    IsInstalling = true;
                    AppendLog($"📦 Uploading {module.Name}...");
                    module.Status = "Installing";

                    var uploadResp = await _client.UploadModuleAsync(module.LocalPath);
                    var result = await _client.InstallModuleAsync(uploadResp);

                    var serverType = result?.Type;
                    var message = result?.UnifiedMessage ?? result?.Message ?? "(no message)";

                    if (serverType?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        module.Status = "SUCCESS";
                        module.InstalledVersion = result?.Module?.Version ?? uploadResp.Version ?? module.LocalVersion;
                        newlyInstalled.Add(Normalize(uploadResp.Name));
                        AppendLog($"✅ {module.Name} installed successfully (v{module.InstalledVersion})");
                        successCount++;
                    }
                    else
                    {
                        AppendLog($"🔍 Verifying {module.Name} installation status from server...");

                        var installedModules = await _client.GetInstalledModulesAsync();
                        var found = installedModules.FirstOrDefault(m =>
                            string.Equals(m.Name, uploadResp.Name, StringComparison.OrdinalIgnoreCase) ||
                            Normalize(m.Name) == Normalize(uploadResp.Name));

                        if (found != null)
                        {
                            module.Status = "SUCCESS";
                            module.InstalledVersion = found.Version ?? uploadResp.Version ?? module.LocalVersion;
                            newlyInstalled.Add(Normalize(uploadResp.Name));
                            AppendLog($"✅ {module.Name} installed successfully (v{module.InstalledVersion})");
                            successCount++;
                        }
                        else
                        {
                            module.Status = "Failed";

                            if (message.Contains("Unsatisfied module requirement", StringComparison.OrdinalIgnoreCase))
                                AppendLog($"⚠️ Cannot install {module.Name}: dependency not met → {message}");
                            else if (message.Contains("rollback-only", StringComparison.OrdinalIgnoreCase) ||
                                     message.Contains("Transaction silently rolled back", StringComparison.OrdinalIgnoreCase))
                                AppendLog($"⚠️ Server rolled back the transaction for {module.Name}: {message}");
                            else
                                AppendLog($"❌ Installation failed for {module.Name}: {message}");

                            failedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    module.Status = "Failed";
                    AppendLog($"💥 {module.Name} failed due to unexpected error: {ex.Message}");
                    failedCount++;
                }
                finally
                {
                    IsInstalling = false;
                }
            }

            // Refresh server state one final time and update UI
            await RefreshInstalledVersionsAsync();

            // ⚙️ Installation Summary
            AppendLog("────────────────────────────");
            AppendLog($"📋 Installation Summary:");
            AppendLog($"   ✅ Success: {successCount}");
            AppendLog($"   ❌ Failed: {failedCount}");
            AppendLog($"   ⏭️ Skipped: {skippedCount}");
            AppendLog($"   📦 Total processed: {selectedModules.Count}");
            AppendLog("────────────────────────────");
            AppendLog("🔁 Selected modules installation completed.");
        }

        private async Task UpdateModulesAsync(List<ModuleViewModel> targetModules)
        {
            IsInstalling = true;

            try
            {
                // refresh installed list first
                var installedModules = await _client.GetInstalledModulesAsync();
                AppendLog($"🔍 Found {installedModules.Count} module(s) on server.");

                foreach (var module in targetModules)
                {
                    var remote = installedModules.FirstOrDefault(m =>
                        string.Equals(m.Name, module.Name, StringComparison.OrdinalIgnoreCase));

                    if (remote == null)
                    {
                        AppendLog($"⚠️ {module.Name} is not installed on server — installing fresh...");
                        module.IsSelected = true;
                        await InstallSelectedAsync();
                        continue;
                    }

                    if (remote.Version != null && module.LocalVersion != null &&
                        !string.Equals(remote.Version, module.LocalVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        AppendLog($"⬆️ Updating {module.Name} from {remote.Version} → {module.LocalVersion}");
                        module.IsSelected = true;
                        await InstallSelectedAsync();
                    }
                    else
                    {
                        AppendLog($"✅ {module.Name} is up to date (v{remote.Version}).");
                    }
                }

                AppendLog("🔁 Module update process completed.");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Update process failed: {ex.Message}");
            }
            finally
            {
                IsInstalling = false;
            }
        }


        [RelayCommand]
        private async Task UpdateAllAsync()
        {
            AppendLog("🔄 Checking for updates for all modules...");
            await UpdateModulesAsync(Modules.ToList());
        }

        [RelayCommand]
        private async Task UpdateSelectedAsync()
        {
            var selected = Modules.Where(m => m.IsSelected).ToList();

            if (!selected.Any())
            {
                AppendLog("⚠️ No modules selected for update.");
                return;
            }

            AppendLog($"🔄 Starting update for {selected.Count} selected module(s)...");
            await UpdateModulesAsync(selected);
        }


        [RelayCommand]
        private void ClearLogs()
        {
            Logs = string.Empty;
        }


        private async Task InstallModuleAsync(ModuleViewModel module)
        {
            try
            {
                var cleanName = module.Name.Replace("-", " ").Replace("_", " ").Trim();

                AppendLog($"🧩 Preparing to install: {cleanName} (v{module.LocalVersion})");
                AppendLog("📤 Uploading module to server...");

                var uploadResp = await _client.UploadModuleAsync(module.LocalPath);

                AppendLog("⚙️ Installing module on server...");
                var result = await _client.InstallModuleAsync(uploadResp);

                // Normalized message (covers Message, DebugMessage, Error, etc.)
                var detailedMessage =
                    result?.UnifiedMessage ??
                    result?.Message ??
                    result?.DebugMessage ??
                    result?.Error ??
                    "No message returned from server.";

                // --- CASE 1: Explicit SUCCESS ---
                if (result?.Type?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
                {
                    module.Status = "SUCCESS";
                    module.InstalledVersion = result.Module?.Version ?? module.LocalVersion;
                    AppendLog($"✅ Installation complete: {cleanName} (v{module.InstalledVersion})");
                    return;
                }

                // --- CASE 2: Possible false negative or dependency issue ---
                AppendLog($"⏳ Verifying installation status for {cleanName}...");

                var installedModules = await _client.GetInstalledModulesAsync();
                var found = installedModules.FirstOrDefault(m =>
                    string.Equals(m.Name, uploadResp.Name, StringComparison.OrdinalIgnoreCase));

                if (found != null)
                {
                    // ✅ It’s actually installed — false negative from LAMIS
                    module.Status = "SUCCESS";
                    module.InstalledVersion = found.Version ?? module.LocalVersion;
                    AppendLog($"✅ {cleanName} installed successfully after verification (v{module.InstalledVersion})");
                }
                else
                {
                    // ❌ Real failure
                    module.Status = "Failed";

                    // Check if dependency issue
                    if (detailedMessage.Contains("Unsatisfied module requirement", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendLog($"⚠️ Dependency error: {detailedMessage}");
                    }
                    else if (detailedMessage.Contains("rollback-only", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendLog($"⚠️ Server rolled back the transaction for {cleanName}: {detailedMessage}");
                    }
                    else
                    {
                        AppendLog($"❌ Installation failed for {cleanName}: {detailedMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                module.Status = "Failed";
                AppendLog($"💥 {module.Name} failed due to unexpected error: {ex.Message}");
            }
        }


        private void AppendLog(string message)
        {
            Logs += $"{DateTime.Now:HH:mm:ss} {message}\n";
        }



        // e.g Extracts "2.1.1" from "patient-2.1.1.jar"
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
