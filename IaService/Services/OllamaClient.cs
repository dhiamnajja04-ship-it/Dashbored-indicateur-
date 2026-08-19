using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IaService.Services;

/// <summary>
/// Client du modèle IA local (Ollama). Le modèle tourne sur la VM / dans le cluster,
/// aucune donnée ne sort vers un service externe payant — exigence du stage
/// (« modèle gratuit installé en local »).
/// </summary>
public class OllamaClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(HttpClient http, IConfiguration config, ILogger<OllamaClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public string Modele => _config["Ollama:Model"] ?? "mistral";

    public async Task<string> GenererAsync(string prompt, CancellationToken ct = default)
    {
        var requete = new OllamaGenerateRequest
        {
            Model = Modele,
            Prompt = prompt,
            Stream = false,
            Options = new OllamaOptions
            {
                // Température basse : on veut une synthèse factuelle des indicateurs,
                // pas de la créativité (et surtout pas de chiffres inventés).
                Temperature = 0.2,
                NumPredict = int.TryParse(_config["Ollama:MaxTokens"], out var n) ? n : 512,
            },
        };

        var payload = new StringContent(
            JsonSerializer.Serialize(requete),
            Encoding.UTF8,
            "application/json"
        );

        _logger.LogInformation("Génération via Ollama, modèle {Modele}", Modele);

        using var response = await _http.PostAsync("/api/generate", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var corps = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Ollama a répondu {StatusCode} : {Corps}", (int)response.StatusCode, corps);
            throw new ModeleIndisponibleException(
                $"Le modèle local n'a pas répondu correctement (HTTP {(int)response.StatusCode}). "
                    + $"Vérifie qu'Ollama tourne et que le modèle « {Modele} » est bien téléchargé (ollama pull {Modele})."
            );
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var resultat = JsonSerializer.Deserialize<OllamaGenerateResponse>(json, JsonOptions);
        return resultat?.Response?.Trim() ?? string.Empty;
    }

    /// <summary>Vérifie qu'Ollama répond (utilisé par /health).</summary>
    public async Task<bool> EstJoignableAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama injoignable");
            return false;
        }
    }

    private class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; set; }
    }

    private class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; }
    }

    private class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}

public class ModeleIndisponibleException : Exception
{
    public ModeleIndisponibleException(string message) : base(message) { }
}
