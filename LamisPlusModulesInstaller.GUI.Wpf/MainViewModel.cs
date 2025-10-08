using WinForms = System.Windows.Forms;
using WpfApp = System.Windows;
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
                        "After the folder is created, please copy all the modules(.jar) files " +
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

                        // This reload modules after folder creation
                        LoadLocalModules();
                    }
                    else
                    {
                        AppendLog($"⚠️ Modules folder missing — please use the '📂 Select Modules Folder' button to choose a location for .jar files.");
                    }
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



        //Replced hardcoded module folder method
        private void LoadLocalModules()
        {
            try
            {
                Modules.Clear();

                if (Directory.Exists(ModulesFolder))
                {
                    var moduleFiles = Directory.GetFiles(ModulesFolder, "*.jar");
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
                    AppendLog($"⚠️ Directory not found: {ModulesFolder}");
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
                IsAuthenticated = true;   // enable buttons
                AppendLog("Login successful.");

                EnsureModulesFolderExists(); //calling this method again to make sure default modules folder exists after login

                await RefreshInstalledVersionsAsync();
            }
            catch (Exception ex)
            {
                AuthStatus = $"Login failed: {ex.Message}";
                IsAuthenticated = false;  // keep buttons disabled
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

        [RelayCommand]
        private void ClearLogs()
        {
            Logs = string.Empty;
        }

        private async Task InstallModuleAsync(ModuleViewModel module)
        {
            try
            {
                module.Status = "Installing"; //show progress
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
