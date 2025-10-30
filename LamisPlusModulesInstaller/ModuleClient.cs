using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LamisPlusModulesInstaller
{
    public class ModuleClient
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ModuleClient(string baseUrl, string jwtToken)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            if (!string.IsNullOrEmpty(jwtToken))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
        }

        public async Task<ModuleUploadResponse> UploadModuleAsync(string jarPath)
        {
            using var form = new MultipartFormDataContent();
            using var fs = File.OpenRead(jarPath);
            var streamContent = new StreamContent(fs);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/java-archive");
            form.Add(streamContent, "file", Path.GetFileName(jarPath));

            var resp = await _http.PostAsync("/api/v1/modules/upload", form);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[UPLOAD ERROR] {resp.StatusCode}: {body}");
                resp.EnsureSuccessStatusCode();
            }

            Console.WriteLine($"[UPLOAD OK] {jarPath}");
            Console.WriteLine($"[UPLOAD RESPONSE] {body}");

            return JsonSerializer.Deserialize<ModuleUploadResponse>(body, _jsonOptions)
                   ?? throw new Exception("Failed to parse upload response");
        }

        public async Task<ModuleInstallResponse?> InstallModuleAsync(ModuleUploadResponse uploaded)
        {
            var url = "/api/v1/modules/install?install=true";

            var payload = new
            {
                active = uploaded.Active,
                artifact = uploaded.Artifact,
                basePackage = uploaded.BasePackage,
                description = uploaded.Description,
                name = uploaded.Name,
                version = uploaded.Version,
                @new = uploaded.New,
                installOnBoot = uploaded.InstallOnBoot ?? false,
                priority = uploaded.Priority
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();

            try
            {
                var parsed = JsonSerializer.Deserialize<ModuleInstallResponse>(body, _jsonOptions);

                // Handle empty/invalid responses
                if (parsed == null)
                {
                    Console.WriteLine($"[INSTALL ERROR] Invalid or empty response: {body}");
                    return new ModuleInstallResponse
                    {
                        Type = "ERROR",
                        Message = "Invalid or empty response from server."
                    };
                }

                // Detect dependency or rollback errors
                if (!string.IsNullOrEmpty(body) &&
                    body.Contains("Unsatisfied module requirement", StringComparison.OrdinalIgnoreCase))
                {
                    var msg = parsed.UnifiedMessage;
                    if (string.IsNullOrWhiteSpace(msg))
                        msg = "Missing module dependency (see LAMIS logs).";

                    Console.WriteLine($"[INSTALL ERROR] Dependency issue: {msg}");
                    return new ModuleInstallResponse
                    {
                        Type = "ERROR",
                        Message = msg,
                        Module = parsed.Module
                    };
                }

                // Detect rollback-only transactions
                if (!string.IsNullOrEmpty(body) &&
                    body.Contains("rollback-only", StringComparison.OrdinalIgnoreCase))
                {
                    var msg = parsed.UnifiedMessage;
                    Console.WriteLine($"[INSTALL ERROR] Transaction rolled back: {msg}");
                    return new ModuleInstallResponse
                    {
                        Type = "ERROR",
                        Message = "Transaction rolled back during installation. See LAMIS logs.",
                        Module = parsed.Module
                    };
                }

                // Regular LAMIS error
                if (parsed.Type?.Equals("ERROR", StringComparison.OrdinalIgnoreCase) == true ||
                    parsed.Type?.Equals("FAILED", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Console.WriteLine($"[INSTALL ERROR] {parsed.UnifiedMessage}");
                    return parsed;
                }

                // HTTP-level error (400, 500)
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[INSTALL HTTP ERROR] {resp.StatusCode}: {body}");
                    return new ModuleInstallResponse
                    {
                        Type = "ERROR",
                        Message = $"HTTP {resp.StatusCode}: {body}"
                    };
                }

                Console.WriteLine($"[INSTALL OK] {parsed.UnifiedMessage}");
                return parsed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INSTALL EXCEPTION] {ex.Message}\nResponse: {body}");
                return new ModuleInstallResponse
                {
                    Type = "ERROR",
                    Message = $"Exception: {ex.Message}"
                };
            }
        }


        public async Task<List<ModuleUploadResponse>> GetInstalledModulesAsync()
        {
            List<ModuleUploadResponse> modules = new();

            async Task<List<ModuleUploadResponse>> FetchAsync(string url)
            {
                var resp = await _http.GetAsync(url);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GET INSTALLED ERROR] {resp.StatusCode}: {body}");
                    return new List<ModuleUploadResponse>();
                }

                try
                {
                    var result = JsonSerializer.Deserialize<List<ModuleUploadResponse>>(body, _jsonOptions);
                    if (result != null && result.Any())
                        Console.WriteLine($"[GET INSTALLED OK] Fetched {result.Count} modules from {url}");
                    return result ?? new List<ModuleUploadResponse>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DESERIALIZE ERROR] {ex.Message}\nBody:\n{body}");
                    return new List<ModuleUploadResponse>();
                }
            }

            // Try the preferred endpoint first
            modules = await FetchAsync("/api/v1/modules/installed");

            // Fallback if empty
            if (modules.Count == 0)
            {
                Console.WriteLine("[INFO] /api/v1/modules/installed returned no data. Trying /api/v1/modules...");
                modules = await FetchAsync("/api/v1/modules");
            }

            return modules;
        }


        public async Task<bool> WaitForModuleRegisteredAsync(string moduleName, int timeoutSeconds = 45, int pollMs = 3000)
        {
            var normalizedTarget = moduleName.ToLowerInvariant()
                .Replace("-", "")
                .Replace("_", "")
                .Replace("module", "")
                .Replace("lamis", "")
                .Replace("plus", "")
                .Trim();

            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var installed = await GetInstalledModulesAsync();

                    var found = installed.FirstOrDefault(m =>
                        !string.IsNullOrWhiteSpace(m.Name) &&
                        (
                            m.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase) ||
                            m.Name.ToLowerInvariant()
                                .Replace("-", "")
                                .Replace("_", "")
                                .Replace("module", "")
                                .Replace("lamis", "")
                                .Replace("plus", "")
                                .Trim()
                                .Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase)
                        ));

                    if (found != null && found.InError != true)
                        return true;
                }
                catch
                {
                    // transient network issues — just retry
                }

                await Task.Delay(pollMs);
            }

            return false;
        }

    }
}
