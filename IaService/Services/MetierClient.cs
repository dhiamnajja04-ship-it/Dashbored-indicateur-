using System.Text.Json;
using IaService.Models;

namespace IaService.Services;

/// <summary>
/// Seul point de contact du service IA avec les données.
/// Règle d'architecture (semaine 5) : l'IA n'ouvre JAMAIS de connexion PostgreSQL,
/// elle passe par le service métier en HTTP interne Kubernetes.
/// </summary>
public class MetierClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ILogger<MetierClient> _logger;

    public MetierClient(HttpClient http, ILogger<MetierClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Récupère les indicateurs accompagnés de leurs SEULES valeurs validées.
    /// Le filtrage est fait côté métier (source de vérité), pas ici.
    /// </summary>
    public async Task<List<IndicateurValide>> GetIndicateursValidesAsync(
        int? indicateurId,
        CancellationToken ct = default
    )
    {
        var url = "validated";
        if (indicateurId.HasValue)
        {
            url += $"?indicateurId={indicateurId.Value}";
        }

        _logger.LogInformation("Appel du service métier : {BaseAddress}{Url}", _http.BaseAddress, url);

        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            var corps = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Le service métier a répondu {StatusCode} : {Corps}",
                (int)response.StatusCode,
                corps
            );
            throw new MetierIndisponibleException(
                $"Le service métier a répondu {(int)response.StatusCode}."
            );
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var indicateurs = JsonSerializer.Deserialize<List<IndicateurValide>>(json, JsonOptions);
        return indicateurs ?? new List<IndicateurValide>();
    }
}

public class MetierIndisponibleException : Exception
{
    public MetierIndisponibleException(string message) : base(message) { }
}
