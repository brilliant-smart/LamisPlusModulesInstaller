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

        //module upload
        public async Task<string> UploadModuleAsync(string filePath)
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
            Console.WriteLine($"[UPLOAD RESPONSE] {body}"); // 👈 ADD THIS
            return body;

        }

        //module install
        public async Task InstallModuleAsync(string uploadResponseJson)
        {
            using var doc = JsonDocument.Parse(uploadResponseJson);
            var moduleElement = doc.RootElement;

            var payload = new
            {
                install = true,
                module = moduleElement
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(uploadResponseJson, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("/api/v1/modules/install?install=true", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[INSTALL ERROR] {response.StatusCode}: {body}");
                response.EnsureSuccessStatusCode();
            }

            Console.WriteLine($"[INSTALL OK] {body}");

        }


    }
}