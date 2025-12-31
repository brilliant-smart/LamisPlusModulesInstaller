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

        public MainViewModel()
        {
            _client = new ModuleClient(BaseUrl, "");

            EnsureModulesFolderExists(); //Method that checks if default modules folder exists else create it
        }

        // ----------------------------------------------------
        // Central Normalize function (single canonical helper)
        // ----------------------------------------------------
        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

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

            // 5. Trim trailing digits that represent jar suffixes (optional)
            cleaned = new string(cleaned.TakeWhile(c => !char.IsDigit(c)).ToArray());

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
                    throw new Exception("Invalid Username or Password.");
                }

                _client = new ModuleClient(BaseUrl, token);

                AuthStatus = "✅ Authenticated";
                IsAuthenticated = true;
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
                IsAuthenticated = false;
                AppendLog(AuthStatus);
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
            try
            {
                var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                TotalModules = dependencies.Count;
                CompletedModules = 0;

                foreach (var kvp in dependencies)
                {
                    var moduleKeyRaw = kvp.Key;
                    var moduleKey = Normalize(moduleKeyRaw);
                    string[] deps = kvp.Value;

                    // normalize deps
                    var normalizedDeps = deps.Select(Normalize).ToArray();

                    // Skip if dependencies not ready
                    if (!normalizedDeps.All(d => installed.Contains(d)))
                    {
                        AppendLog($"⏩ Skipping {moduleKeyRaw}, dependencies not satisfied: {string.Join(", ", kvp.Value)}");
                        CompletedModules++;
                        UpdateProgress();
                        continue;
                    }

                    // find local module by normalized name
                    var module = Modules.FirstOrDefault(m => Normalize(m.Name).Contains(moduleKey, StringComparison.OrdinalIgnoreCase));

                    if (module == null)
                    {
                        AppendLog($"❌ No local JAR found for {moduleKeyRaw}");
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
                UpdateProgress(true);
            }
        }

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
                        IsInstalling = false;
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

                // count as skipped

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
        }

        private async Task UpdateModulesAsync(List<ModuleViewModel> targetModules)
        {
            IsInstalling = true;

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
                            continue;
                        }

                        var cleanName = moduleVm.Name.Replace("-", " ").Replace("_", " ").Trim();

                        var remote = installedOnServer.FirstOrDefault(m => Normalize(m.Name) == normName);

                        var remoteVersion = remote?.Version ?? "(not installed)";
                        var localVersion = moduleVm.LocalVersion ?? "(unknown)";

                        if (!string.Equals(remoteVersion, localVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            AppendLog($"⬆️ Updating {cleanName} from {remoteVersion} → {localVersion}");
                            moduleVm.Status = "Installing";

                            try
                            {
                                await InstallModuleAsync(moduleVm);

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
                        }
                        else
                        {
                            AppendLog($"✅ {cleanName} already up to date (v{remoteVersion}).");
                            moduleVm.Status = "Up to date";
                            skippedCount++;
                            willBeInstalled.Add(normName);
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
