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
            // this is the endpoint that works "/api/v1/modules/install" failed
            var url = "/api/v1/modules/install?install=true";

            // the payload that matches the expected module object is camelCase, therefore it is changed
            var payload = new
            {
                active = uploaded.Active,
                artifact = uploaded.Artifact,
                basePackage = uploaded.BasePackage,
                description = uploaded.Description,
                name = uploaded.Name,
                version = uploaded.Version,
                @new = uploaded.New, // will serialize to "new" because of camelCase naming policy
                installOnBoot = uploaded.InstallOnBoot ?? false,
                priority = uploaded.Priority
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[INSTALL ERROR] {resp.StatusCode}: {body}");
                // Try to deserialize error payload into ModuleInstallResponse if possible
                try
                {
                    return JsonSerializer.Deserialize<ModuleInstallResponse>(body, _jsonOptions);
                }
                catch
                {
                    return null;
                }
            }

            Console.WriteLine($"[INSTALL OK] {body}");
            return JsonSerializer.Deserialize<ModuleInstallResponse>(body, _jsonOptions);
        }

        public async Task<List<ModuleUploadResponse>> GetInstalledModulesAsync()
        {
            var resp = await _http.GetAsync("/api/v1/modules/installed");
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[GET INSTALLED ERROR] {resp.StatusCode}: {body}");
                return new List<ModuleUploadResponse>();
            }

            return JsonSerializer.Deserialize<List<ModuleUploadResponse>>(body, _jsonOptions)
                   ?? new List<ModuleUploadResponse>();
        }

        public async Task<bool> WaitForModuleRegisteredAsync(string moduleName, int timeoutSeconds = 60, int pollMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var installed = await GetInstalledModulesAsync();
                var found = installed.FirstOrDefault(m =>
                    string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));

                if (found != null && found.InError != true)
                    return true;

                await Task.Delay(pollMs);
            }

            return false;
        }
    }
}
