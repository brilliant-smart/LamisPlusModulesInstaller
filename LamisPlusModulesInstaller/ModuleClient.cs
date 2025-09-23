using System;
using System.IO;
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

        public ModuleClient(string baseUrl, string jwtToken)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwtToken);
        }

        public async Task<ModuleUploadResponse> UploadModuleAsync(string filePath)
        {
            using var form = new MultipartFormDataContent();
            using var fs = File.OpenRead(filePath);
            var streamContent = new StreamContent(fs);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/java-archive");
            form.Add(streamContent, "file", Path.GetFileName(filePath));

            var response = await _http.PostAsync("/api/v1/modules/upload", form);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[UPLOAD ERROR] {response.StatusCode}: {body}");
                response.EnsureSuccessStatusCode();
            }

            Console.WriteLine($"[UPLOAD OK] {filePath}");
            Console.WriteLine($"[UPLOAD RESPONSE] {body}");

            return JsonSerializer.Deserialize<ModuleUploadResponse>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<ModuleInstallResponse?> InstallModuleAsync(ModuleUploadResponse uploaded)
        {
            var url = "/api/v1/modules/install?install=true";

            // Construct minimal payload matching swagger model
            var payload = new
            {
                active = uploaded.Active,
                artifact = uploaded.Artifact,
                basePackage = uploaded.BasePackage,
                description = uploaded.Description,
                name = uploaded.Name,
                version = uploaded.Version,
                newModule = uploaded.New,   // note: "new" is a keyword in C#
                installOnBoot = false,
                priority = uploaded.Priority
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[INSTALL ERROR] {resp.StatusCode}: {body}");
                return null;
            }

            Console.WriteLine($"[INSTALL OK] {body}");
            return JsonSerializer.Deserialize<ModuleInstallResponse>(body);
        }



    }
}
