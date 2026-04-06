using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
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
        [ObservableProperty] private string password = string.Empty;
        [ObservableProperty] private string authStatus = "🔘 Not logged in";
        [ObservableProperty] private string authStatusColor = "Gray";
        [ObservableProperty] private string logs = "";
        [ObservableProperty] private bool isAuthenticated = false;
        [ObservableProperty] private bool isInstalling = false;
        [ObservableProperty] private string modulesFolder = @"C:\lamismodules";
        //progress bar
        [ObservableProperty] private int totalModules = 0;
        [ObservableProperty] private int completedModules = 0;
        [ObservableProperty] private int installProgress = 0;
        [ObservableProperty] private string progressText = "";

        // Dynamic current year for copyright footer
        public string CurrentYear => DateTime.Now.Year.ToString();

        public ObservableCollection<ModuleViewModel> Modules { get; } = new();

        // Event to notify when a module status changes to Installing
        public event Action<ModuleViewModel>? ModuleInstalling;

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
                { "Sync", Array.Empty<string>() },
                { "DQR", Array.Empty<string>() },
                { "Client-sync", Array.Empty<string>() }
            };

        public MainViewModel()
        {
            _client = new ModuleClient(BaseUrl, "");
            password = string.Empty; // Initialize to satisfy nullable warning

            EnsureModulesFolderExists(); //Method that checks if default modules folder exists else create it
        }

        // ----------------------------------------------------
        // Central Normalize function (single canonical helper)
        // ----------------------------------------------------
        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            // 0. Strip leading number prefix (e.g., "19 - " from "19 - sync-2.1.0.jar")
            // Find first letter and take substring from there
            var firstLetterIndex = s.IndexOfAny("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray());
            if (firstLetterIndex > 0)
                s = s.Substring(firstLetterIndex);

            // 1. Lowercase, trim
            s = s.Trim().ToLowerInvariant();

            // 2. Remove common words and separators used in filenames & module names
            s = s.Replace("-", "")
                 .Replace("_", "")
                 .Replace("module", "")
                 .Replace("mod", "")
                 .Replace("lamis", "")
                 .Replace("plus", "")
                 .Replace(" ", "");

            // 3. Keep letters and digits (remove other symbols)
            var cleaned = new string(s.Where(char.IsLetterOrDigit).ToArray());

            // 4. If cleaned is empty (rare), fallback to removing only separators
            if (string.IsNullOrWhiteSpace(cleaned))
                cleaned = s.Replace(" ", "").Replace("-", "").Replace("_", "");

            // 5. Trim trailing digits that represent version numbers (e.g., "sync210" -> "sync")
            // Find last non-digit and trim everything after it
            int lastNonDigit = -1;
            for (int i = cleaned.Length - 1; i >= 0; i--)
            {
                if (!char.IsDigit(cleaned[i]))
                {
                    lastNonDigit = i;
                    break;
                }
            }
            if (lastNonDigit >= 0)
                cleaned = cleaned.Substring(0, lastNonDigit + 1);

            return string.IsNullOrWhiteSpace(cleaned) ? s : cleaned;

        }

        // -------------------------
        // Folder and module loading
        // -------------------------
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

                        LoadLocalModules();
                    }
                    else
                    {
                        AppendLog("⚠️ Modules folder missing — please use the '📂 Select Modules Folder' button to choose a location for .jar files.");
                    }
                }
                else
                {
                    AppendLog($"📁 Found existing modules folder: {ModulesFolder}");
                    LoadLocalModules();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️ Error verifying modules folder: {ex.Message}");
            }
        }

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

        private void LoadLocalModules()
        {
            try
            {
                Modules.Clear();

                if (Directory.Exists(ModulesFolder))
                {
                    var moduleFiles = Directory.GetFiles(ModulesFolder, "*.jar");

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

                    // Sort based on normalized dependency dictionary order
                    var orderedModules = unsortedModules
                        .OrderBy(m =>
                        {
                            var normalized = Normalize(m.Name);

                            // First try exact match (e.g., "sync" vs "Sync", "clientsync" vs "Client-sync")
                            var exactKey = dependencies.Keys
                                .FirstOrDefault(k => normalized.Equals(Normalize(k), StringComparison.OrdinalIgnoreCase));

                            if (exactKey != null)
                                return dependencies.Keys.ToList().IndexOf(exactKey);

                            // Fall back to contains match for other cases
                            var key = dependencies.Keys
                                .FirstOrDefault(k => normalized.Contains(Normalize(k), StringComparison.OrdinalIgnoreCase));

                            return key != null ? dependencies.Keys.ToList().IndexOf(key) : int.MaxValue;
                        })
                        .ToList();

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

        // -------------------------
        // Authentication
        // -------------------------
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
                    throw new Exception("Invalid credentials");
                }

                _client = new ModuleClient(BaseUrl, token);

                AuthStatus = "🟢 Logged in";
                AuthStatusColor = "#22C55E"; // Green
                IsAuthenticated = true;
                AppendLog("✅ Login successful.");

                EnsureModulesFolderExists();
                await RefreshInstalledVersionsAsync();
            }
            catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
            {
                // Connection refused - LamisPlus is not running
                AuthStatus = "🔴 LamisPlus not running";
                AuthStatusColor = "#EF4444"; // Red
                IsAuthenticated = false;
                AppendLog("❌ LamisPlus is not running. Kindly start LamisPlus first.");
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
            {
                // Wrong username/password
                AuthStatus = "🔴 Wrong username/password";
                AuthStatusColor = "#EF4444"; // Red
                IsAuthenticated = false;
                AppendLog("❌ Login failed: Wrong username or password.");
            }
            catch (HttpRequestException ex)
            {
                // Other HTTP errors
                AuthStatus = "🔴 Connection failed";
                AuthStatusColor = "#EF4444"; // Red
                IsAuthenticated = false;
                AppendLog($"❌ Login failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Other errors (including "Invalid credentials" from empty token)
                if (ex.Message.Contains("Invalid credentials"))
                {
                    AuthStatus = "🔴 Wrong username/password";
                    AuthStatusColor = "#EF4444"; // Red
                    IsAuthenticated = false;
                    AppendLog("❌ Login failed: Wrong username or password.");
                }
                else
                {
                    AuthStatus = "🔴 Login failed";
                    AuthStatusColor = "#EF4444"; // Red
                    IsAuthenticated = false;
                    AppendLog($"❌ Login failed: {ex.Message}");
                }
            }
        }

        // ------------------------------------
        // Refresh installed versions on server
        // ------------------------------------
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
                    var localName = Normalize(module.Name);

                    var match = installed.FirstOrDefault(m =>
                    {
                        if (string.IsNullOrWhiteSpace(m.Name))
                            return false;

                        var remoteName = Normalize(m.Name);

                        return localName == remoteName || remoteName.Contains(localName) || localName.Contains(remoteName);
                    });

                    if (match != null)
                    {
                        module.InstalledVersion = string.IsNullOrWhiteSpace(match.Version) ? "(unknown)" : match.Version;
                        module.Status = "SUCCESS";
                        AppendLog($"✅ Found {module.Name} installed on server (v{module.InstalledVersion})");
                    }
                    else
                    {
                        module.InstalledVersion = "Not Installed";
                        module.Status = "Not Installed";
                    }

                }

                AppendLog("🔁 Installed module versions updated successfully.");
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️ Failed to fetch installed modules: {ex.Message}");
            }
        }

        // ------------------------------
        // Install All (dependency-aware)
        // ------------------------------
        [RelayCommand]
        private async Task InstallAllAsync()
        {
            IsInstalling = true;
            var successCount = 0;
            var failedCount = 0;
            var skippedCount = 0;

            try
            {
                var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                TotalModules = dependencies.Count;
                CompletedModules = 0;

                // Preload already-installed modules from server
                try
                {
                    AppendLog("🔍 Checking which modules are already installed on server...");
                    var serverInstalled = await _client.GetInstalledModulesAsync();
                    foreach (var m in serverInstalled)
                    {
                        if (!string.IsNullOrWhiteSpace(m.Name))
                        {
                            var normalized = Normalize(m.Name);
                            installed.Add(normalized);
                            AppendLog($"   ✓ {m.Name} already installed (v{m.Version ?? "unknown"})");
                        }
                    }
                    AppendLog($"📋 Found {installed.Count} modules already installed");
                }
                catch (Exception ex)
                {
                    AppendLog($"⚠️ Could not preload installed modules: {ex.Message}");
                }

                AppendLog($"📦 Starting installation of {TotalModules} modules...");

                foreach (var kvp in dependencies)
                {
                    var moduleKeyRaw = kvp.Key;
                    var moduleKey = Normalize(moduleKeyRaw);
                    string[] deps = kvp.Value;

                    // normalize deps
                    var normalizedDeps = deps.Select(Normalize).ToArray();

                    // Check if already installed - skip installation
                    if (installed.Contains(moduleKey))
                    {
                        AppendLog($"✓ {moduleKeyRaw} already installed - skipping");
                        skippedCount++;
                        CompletedModules++;
                        UpdateProgress(operation: "Install All");
                        continue;
                    }

                    // Skip if dependencies not ready
                    if (!normalizedDeps.All(d => installed.Contains(d)))
                    {
                        var missingDeps = normalizedDeps.Where(d => !installed.Contains(d)).ToList();
                        AppendLog($"⏩ Skipping {moduleKeyRaw}, missing dependencies: {string.Join(", ", missingDeps)}");
                        skippedCount++;
                        CompletedModules++;
                        UpdateProgress(operation: "Install All");
                        continue;
                    }

                    // find local module by normalized name
                    var module = Modules.FirstOrDefault(m => 
                    {
                        var normalizedModuleName = Normalize(m.Name);
                        return normalizedModuleName == moduleKey || 
                               normalizedModuleName.Contains(moduleKey, StringComparison.OrdinalIgnoreCase);
                    });

                    if (module == null)
                    {
                        AppendLog($"❌ No local JAR found for {moduleKeyRaw}");
                        skippedCount++;
                        CompletedModules++;
                        UpdateProgress(operation: "Install All");
                        continue;
                    }

                    await InstallModuleAsync(module);

                    if (module.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        installed.Add(moduleKey);
                        successCount++;
                    }
                    else if (module.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase) || 
                             module.Status.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        failedCount++;
                    }

                    CompletedModules++;
                    UpdateProgress(operation: "Install All");
                }

                await RefreshInstalledVersionsAsync();

                AppendLog("────────────────────────────");
                AppendLog($"📋 Installation Summary:");
                AppendLog($"   ✅ Success: {successCount}");
                AppendLog($"   ❌ Failed: {failedCount}");
                AppendLog($"   ⏭️ Skipped: {skippedCount}");
                AppendLog($"   📦 Total processed: {TotalModules}");
                AppendLog("────────────────────────────");
                AppendLog("🔁 Install All completed.");
            }
            finally
            {
                IsInstalling = false;
                UpdateProgress(operation: "Install All", finished: true);
            }
        }

        private void UpdateProgress(string operation = "Processing", bool finished = false)
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
                ProgressText = $"✅ {operation} completed: {CompletedModules}/{TotalModules} modules";
            }
            else
            {
                InstallProgress = (int)((double)CompletedModules / TotalModules * 100);
                ProgressText = $"{operation}: {CompletedModules}/{TotalModules} ({InstallProgress}%)";
            }
        }

        // -------------------------------------------------------------------------
        // Install Selected module (multi-select but with dependency-aware ordering)
        // -------------------------------------------------------------------------
        [RelayCommand]
        private async Task InstallSelectedAsync()
        {
            var selectedModules = Modules.Where(m => m.IsSelected).ToList();

            if (!selectedModules.Any())
            {
                AppendLog("⚠️ No modules selected for installation.");
                return;
            }

            IsInstalling = true;
            TotalModules = selectedModules.Count;
            CompletedModules = 0;
            UpdateProgress();

            AppendLog($"🧩 Starting installation for {selectedModules.Count} selected module(s)...");

            // Normalize dependency dictionary safely
            var normalizedDeps = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dependencies)
            {
                var key = Normalize(kvp.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!normalizedDeps.ContainsKey(key))
                    normalizedDeps[key] = kvp.Value.Select(Normalize)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
            }

            // fetch installed modules from the server at start
            var installed = (await _client.GetInstalledModulesAsync())
                .Select(m => Normalize(m.Name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            AppendLog($"🔍 Installed modules (normalized): {string.Join(", ", installed)}");

            var newlyInstalled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var successCount = 0;
            var failedCount = 0;
            var skippedCount = 0;

            // process in dependency-aware order by repeatedly trying modules whose deps are satisfied
            var pending = selectedModules.Select(m => Normalize(m.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int safety = 0;
            while (processed.Count < pending.Count && safety++ < 100)
            {
                bool progress = false;
                var toTry = pending.Except(processed).ToList();
                foreach (var normName in toTry)
                {
                    var moduleVm = selectedModules.FirstOrDefault(m => Normalize(m.Name) == normName);

                    normalizedDeps.TryGetValue(normName, out var depsForThis);
                    depsForThis ??= Array.Empty<string>();

                    var missing = depsForThis.Where(d => !installed.Contains(d) && !newlyInstalled.Contains(d)).ToList();
                    if (missing.Any())
                        continue; // wait until deps satisfied

                    // deps OK -> process
                    progress = true;
                    processed.Add(normName);

                    if (moduleVm == null)
                    {
                        AppendLog($"❌ Skipping {normName} — no local JAR found.");
                        skippedCount++;
                        CompletedModules++;
                        UpdateProgress();
                        continue;
                    }

                    var cleanName = moduleVm.Name.Replace("-", " ").Replace("_", " ").Trim();

                    try
                    {
                        IsInstalling = true;
                        AppendLog($"📦 Uploading {moduleVm.Name}...");
                        moduleVm.Status = "Installing";

                        var uploadResp = await _client.UploadModuleAsync(moduleVm.LocalPath);
                        var result = await _client.InstallModuleAsync(uploadResp);

                        var serverType = result?.Type;
                        var message = result?.UnifiedMessage ?? result?.Message ?? "(no message)";

                        if (serverType?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            moduleVm.Status = "SUCCESS";
                            moduleVm.InstalledVersion = result?.Module?.Version ?? uploadResp.Version ?? moduleVm.LocalVersion;
                            newlyInstalled.Add(Normalize(uploadResp.Name));
                            AppendLog($"✅ {cleanName} installed successfully (v{moduleVm.InstalledVersion})");
                            successCount++;
                        }
                        else
                        {
                            AppendLog($"🔍 Verifying {moduleVm.Name} installation status from server...");

                            var installedModules = await _client.GetInstalledModulesAsync();
                            var found = installedModules.FirstOrDefault(m =>
                                string.Equals(m.Name, uploadResp.Name, StringComparison.OrdinalIgnoreCase) ||
                                Normalize(m.Name) == Normalize(uploadResp.Name));

                            if (found != null)
                            {
                                moduleVm.Status = "SUCCESS";
                                moduleVm.InstalledVersion = found.Version ?? uploadResp.Version ?? moduleVm.LocalVersion;
                                newlyInstalled.Add(Normalize(uploadResp.Name));
                                AppendLog($"✅ {cleanName} installed successfully (v{moduleVm.InstalledVersion})");
                                successCount++;
                            }
                            else
                            {
                                moduleVm.Status = "Failed";

                                if (message.Contains("Unsatisfied module requirement", StringComparison.OrdinalIgnoreCase))
                                    AppendLog($"⚠️ Cannot install {moduleVm.Name}: dependency not met → {message}");
                                else if (message.Contains("rollback-only", StringComparison.OrdinalIgnoreCase) ||
                                         message.Contains("Transaction silently rolled back", StringComparison.OrdinalIgnoreCase))
                                    AppendLog($"⚠️ Server rolled back the transaction for {moduleVm.Name}: {message}");
                                else
                                    AppendLog($"❌ Installation failed for {moduleVm.Name}: {message}");

                                failedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        moduleVm.Status = "Failed";
                        AppendLog($"💥 {moduleVm.Name} failed due to unexpected error: {ex.Message}");
                        failedCount++;
                    }
                    finally
                    {
                        CompletedModules++;
                        UpdateProgress();
                    }
                }

                if (!progress) break;
            }

            // any remaining pending that couldn't be processed
            var unprocessed = pending.Except(processed).ToList();
            foreach (var np in unprocessed)
            {
                normalizedDeps.TryGetValue(np, out var deps);
                deps ??= Array.Empty<string>();
                var missing = deps.Where(d => !installed.Contains(d) && !newlyInstalled.Contains(d)).ToList();
                var displayName = selectedModules.FirstOrDefault(m => Normalize(m.Name) == np)?.Name ?? np;
                if (missing.Any())
                    AppendLog($"❌ Skipping {displayName} — missing dependencies: {string.Join(", ", missing)}");
                else
                    AppendLog($"❌ Skipping {displayName} — could not resolve ordering (possible circular dependency).");

                skippedCount++;

            }

            await RefreshInstalledVersionsAsync();

            AppendLog("────────────────────────────");
            AppendLog($"📋 Installation Summary:");
            AppendLog($"   ✅ Success: {successCount}");
            AppendLog($"   ❌ Failed: {failedCount}");
            AppendLog($"   ⏭️ Skipped: {skippedCount}");
            AppendLog($"   📦 Total processed: {selectedModules.Count}");
            AppendLog("────────────────────────────");
            AppendLog("🔁 Selected modules installation completed.");
            
            IsInstalling = false;
            UpdateProgress(operation: "Install Selected", finished: true);
        }

        private async Task UpdateModulesAsync(List<ModuleViewModel> targetModules)
        {
            IsInstalling = true;
            TotalModules = targetModules.Count;
            CompletedModules = 0;
            UpdateProgress(operation: "Update");

            int successCount = 0, failedCount = 0, skippedCount = 0;
            try
            {
                AppendLog("🔄 Fetching currently installed modules from server...");
                var installedOnServer = await _client.GetInstalledModulesAsync();
                var installedNormalized = installedOnServer
                    .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                    .Select(m => Normalize(m.Name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                AppendLog($"🔍 Found {installedOnServer.Count} module(s) on server.");

                // Safe normalized deps
                var normalizedDeps = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in dependencies)
                {
                    var key = Normalize(kvp.Key);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (!normalizedDeps.ContainsKey(key))
                        normalizedDeps[key] = kvp.Value.Select(Normalize)
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                }

                // Safe local module map
                var localModuleMap = new Dictionary<string, ModuleViewModel>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in Modules)
                {
                    var key = Normalize(m.Name);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (!localModuleMap.ContainsKey(key))
                        localModuleMap[key] = m;
                }

                var willBeInstalled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // dependency-aware order for the requested target modules
                var pending = targetModules.Select(m => Normalize(m.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var processedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                AppendLog($"🔍 Preparing to update {pending.Count} module(s) (dependency-aware ordering)...");

                bool progress;
                int safety = 0;
                do
                {
                    progress = false;
                    safety++;

                    var toTry = pending.Except(processedThisRun).ToList();
                    foreach (var normName in toTry)
                    {
                        localModuleMap.TryGetValue(normName, out var moduleVm);

                        normalizedDeps.TryGetValue(normName, out var depsForThis);
                        depsForThis ??= Array.Empty<string>();

                        var missingDeps = depsForThis
                            .Where(d => !installedNormalized.Contains(d) && !willBeInstalled.Contains(d))
                            .ToList();

                        if (missingDeps.Any())
                        {
                            continue;
                        }

                        progress = true;
                        processedThisRun.Add(normName);

                        if (moduleVm == null)
                        {
                            AppendLog($"❌ Skipping {normName} — no local JAR found for update.");
                            skippedCount++;
                            CompletedModules++;
                            UpdateProgress(operation: "Update");
                            continue;
                        }

                        var cleanName = moduleVm.Name.Replace("-", " ").Replace("_", " ").Trim();

                        var remote = installedOnServer.FirstOrDefault(m => Normalize(m.Name) == normName);

                        var remoteVersion = remote?.Version ?? "(not installed)";
                        var localVersion = moduleVm.LocalVersion ?? "(unknown)";

                        // Update always processes modules, even if versions match (allows force-reinstall for crashed modules)
                        AppendLog($"⬆️ Updating {cleanName} from {remoteVersion} → {localVersion}");
                        moduleVm.Status = "Installing";

                        try
                        {
                            // Always use the update endpoint for force-reinstall capability
                            await InstallModuleAsync(moduleVm, isUpdate: true);

                            if (moduleVm.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                            {
                                successCount++;
                                willBeInstalled.Add(normName);
                                AppendLog($"✅ {cleanName} updated successfully (v{moduleVm.InstalledVersion})");
                            }
                            else
                            {
                                failedCount++;
                                AppendLog($"❌ {cleanName} update failed (status: {moduleVm.Status})");
                            }
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            moduleVm.Status = "Failed";
                            AppendLog($"❌ Update failed for {cleanName}: {ex.Message}");
                        }
                        finally
                        {
                            CompletedModules++;
                            UpdateProgress(operation: "Update");
                        }
                    }

                    if (!progress) break;
                } while (processedThisRun.Count < pending.Count && safety < 50);

                var unprocessed = pending.Except(processedThisRun).ToList();
                foreach (var np in unprocessed)
                {
                    normalizedDeps.TryGetValue(np, out var deps);
                    deps ??= Array.Empty<string>();

                    var missing = deps.Where(d => !installedNormalized.Contains(d) && !willBeInstalled.Contains(d)).ToList();
                    var displayName = localModuleMap.ContainsKey(np) ? localModuleMap[np].Name : np;

                    if (missing.Any())
                    {
                        AppendLog($"❌ Skipping {displayName} — missing dependencies on server: {string.Join(", ", missing)}");
                    }
                    else
                    {
                        AppendLog($"❌ Skipping {displayName} — could not resolve ordering (possible circular dependency).");
                    }

                    skippedCount++;
                }

                await RefreshInstalledVersionsAsync();

                AppendLog("────────────────────────────");
                AppendLog("📋 Update Summary:");
                AppendLog($"   ✅ Success: {successCount}");
                AppendLog($"   ❌ Failed: {failedCount}");
                AppendLog($"   ⏭️ Skipped: {skippedCount}");
                AppendLog($"   📦 Total processed: {targetModules.Count}");
                AppendLog("────────────────────────────");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Update process encountered an error: {ex.Message}");
            }
            finally
            {
                IsInstalling = false;
                UpdateProgress(operation: "Update", finished: true);
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

        [RelayCommand]
        private async Task RestartLamisAsync()
        {
            try
            {
                AppendLog("🔄 Initiating LAMISPlus restart...");
                var (success, message) = await _client.RestartLamisAsync();
                
                if (success)
                {
                    AppendLog($"✅ {message}");
                    AppendLog("⚠️ Please wait for LAMISPlus to restart. This may take 1-2 minutes.");
                    AppendLog("💡 Opening LAMISPlus in browser...");
                    
                    // Open LAMISPlus in default browser
                    try
                    {
                        var url = BaseUrl.TrimEnd('/');
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                        AppendLog($"🌐 Browser launched: {url}");
                    }
                    catch (Exception browserEx)
                    {
                        AppendLog($"⚠️ Could not open browser automatically: {browserEx.Message}");
                    }
                }
                else
                {
                    AppendLog($"❌ {message}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error restarting LAMISPlus: {ex.Message}");
            }
        }

        private async Task InstallModuleAsync(ModuleViewModel module, bool isUpdate = false)
        {
            try
            {
                var cleanName = module.Name.Replace("-", " ").Replace("_", " ").Trim();

                AppendLog($"🧩 Preparing to {(isUpdate ? "update" : "install")}: {cleanName} (v{module.LocalVersion})");
                module.Status = "Installing";
                
                // Notify UI to scroll to this module
                ModuleInstalling?.Invoke(module);
                
                AppendLog("📤 Uploading module to server...");

                var uploadResp = await _client.UploadModuleAsync(module.LocalPath);

                AppendLog($"⚙️ {(isUpdate ? "Updating" : "Installing")} module on server...");
                var result = await _client.InstallModuleAsync(uploadResp, isUpdate);

                var detailedMessage =
                    result?.UnifiedMessage ??
                    result?.Message ??
                    result?.DebugMessage ??
                    result?.Error ??
                    "No message returned from server.";

                // --- CASE 1: Explicit SUCCESS ---
                if (result?.Type?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Wait for module to be fully registered on server
                    var serverName = uploadResp.Name ?? module.Name;
                    AppendLog($"⏳ Waiting for {cleanName} to be fully registered (timeout 60s)...");
                    var registered = await _client.WaitForModuleRegisteredAsync(serverName, 60);
                    
                    if (registered)
                    {
                        module.Status = "SUCCESS";
                        module.InstalledVersion = result.Module?.Version ?? module.LocalVersion;
                        AppendLog($"✅ Installation complete: {cleanName} (v{module.InstalledVersion})");
                        return;
                    }
                    else
                    {
                        AppendLog($"⚠️ {cleanName} reported success but did not appear in registered modules within timeout");
                        // Fall through to verification
                    }
                }

                // --- CASE 2: Possible false negative or dependency issue ---
                AppendLog($"⏳ Verifying installation status for {cleanName}...");

                var installedModules = await _client.GetInstalledModulesAsync();
                var found = installedModules.FirstOrDefault(m =>
                    string.Equals(m.Name, uploadResp.Name, StringComparison.OrdinalIgnoreCase));

                if (found != null)
                {
                    module.Status = "SUCCESS";
                    module.InstalledVersion = found.Version ?? module.LocalVersion;
                    AppendLog($"✅ {cleanName} installed successfully after verification (v{module.InstalledVersion})");
                }
                else
                {
                    module.Status = "Failed";

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
