using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LamisPlusModulesInstaller
{
    public class AuthHelper
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public AuthHelper(string baseUrl)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        public async Task<string?> LoginAsync(string username, string password)
        {
            var payload = new
            {
                username = username,   // IMPORTANT: server expects "username", "password"
                password = password,
                rememberMe = true
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync("/api/v1/authenticate", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[AUTH ERROR] {resp.StatusCode}: {body}");
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("id_token", out var tk))
                    return tk.GetString();
                if (doc.RootElement.TryGetProperty("access_token", out var at))
                    return at.GetString();

                Console.WriteLine("[AUTH ERROR] No token found in response.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUTH ERROR] Failed to parse token: {ex.Message}");
                return null;
            }
        }
    }
}