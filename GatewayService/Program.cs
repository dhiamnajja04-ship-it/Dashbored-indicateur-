using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Le frontend Angular appelle le Gateway : il lui faut CORS.
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowFrontend",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
    );
});

var app = builder.Build();

app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- Santé du Gateway lui-même ---
// « instance » expose le nom de machine du conteneur ou du pod qui répond.
// C'est ce qui rend la répartition de charge OBSERVABLE : dix appels
// successifs doivent renvoyer des noms différents quand plusieurs répliques
// tournent. Sans cela, on ne peut que supposer que la charge est répartie.
app.MapGet("/health", () => Results.Ok(new
{
    status = "OK",
    service = "GatewayService",
    instance = Environment.MachineName,
    timestamp = DateTime.UtcNow,
}));

// --- Santé agrégée de la plateforme : un seul appel pour vérifier toute la chaîne ---
app.MapGet(
    "/health/plateforme",
    async (IHttpClientFactory clientFactory, IConfiguration config) =>
    {
        var client = clientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        var metier = await Sonder(client, RacineDe(config["MetierServiceUrl"], "http://localhost:5039/api/indicators") + "/health/ready");
        var ia = await Sonder(client, RacineDe(config["IaServiceUrl"], "http://localhost:5210/api/ia") + "/health/ready");

        var toutOk = metier == "OK" && ia == "OK";
        var corps = new
        {
            status = toutOk ? "OK" : "DEGRADED",
            gateway = "OK",
            // Quelle réplique du Gateway a traité cet appel : utile pour
            // constater la répartition sans instrumenter le client.
            instance = Environment.MachineName,
            metier,
            ia,
            timestamp = DateTime.UtcNow,
        };
        return toutOk ? Results.Ok(corps) : Results.Json(corps, statusCode: 503);
    }
);

// --- Routage vers le service métier : tout ce qui est sous /api/indicators ---
// (couvre /api/indicators, /api/indicators/5, /api/indicators/validated,
//  /api/indicators/5/valeurs, /api/indicators/values/5/validate, tous verbes HTTP)
app.Map(
    "/api/indicators/{**catchAll}",
    (HttpContext ctx, IHttpClientFactory f, IConfiguration c, string? catchAll) =>
        Relayer(ctx, f, c["MetierServiceUrl"] ?? "http://localhost:5039/api/indicators", catchAll ?? "")
);

app.Map(
    "/api/indicators",
    (HttpContext ctx, IHttpClientFactory f, IConfiguration c) =>
        Relayer(ctx, f, c["MetierServiceUrl"] ?? "http://localhost:5039/api/indicators", "")
);

// --- Routage vers le service métier : les réclamations ---
// Même service que les indicateurs, mais préfixe distinct : on reconstruit
// donc la racine du métier à partir de l'URL configurée.
app.Map(
    "/api/reclamations/{**catchAll}",
    (HttpContext ctx, IHttpClientFactory f, IConfiguration c, string? catchAll) =>
        Relayer(ctx, f, RacineDe(c["MetierServiceUrl"], "http://localhost:5039") + "/api/reclamations", catchAll ?? "")
);

app.Map(
    "/api/reclamations",
    (HttpContext ctx, IHttpClientFactory f, IConfiguration c) =>
        Relayer(ctx, f, RacineDe(c["MetierServiceUrl"], "http://localhost:5039") + "/api/reclamations", "")
);

// --- Référentiels exposés au frontend (organisations, périodes) ---
foreach (var referentiel in new[] { "organisations", "periodes" })
{
    var chemin = referentiel;
    app.Map(
        $"/api/{chemin}/{{**catchAll}}",
        (HttpContext ctx, IHttpClientFactory f, IConfiguration c, string? catchAll) =>
            Relayer(ctx, f, RacineDe(c["MetierServiceUrl"], "http://localhost:5039/api/indicators") + $"/api/{chemin}", catchAll ?? "")
    );
    app.Map(
        $"/api/{chemin}",
        (HttpContext ctx, IHttpClientFactory f, IConfiguration c) =>
            Relayer(ctx, f, RacineDe(c["MetierServiceUrl"], "http://localhost:5039/api/indicators") + $"/api/{chemin}", "")
    );
}

// --- Référentiel des agents (table utilisateurs) ---
app.Map(
    "/api/utilisateurs/{**catchAll}",
    (HttpContext ctx, IHttpClientFactory f, IConfiguration c, string? catchAll) =>
        Relayer(ctx, f, RacineDe(c["MetierServiceUrl"], "http://localhost:5039/api/indicators") + "/api/utilisateurs", catchAll ?? "")
);

app.Map(
    "/api/utilisateurs",
    (HttpContext ctx, IHttpClientFactory f, IConfiguration c) =>
        Relayer(ctx, f, RacineDe(c["MetierServiceUrl"], "http://localhost:5039/api/indicators") + "/api/utilisateurs", "")
);

// --- Routage vers le service IA : tout ce qui est sous /api/ia ---
// Le frontend n'appelle jamais le service IA directement (règle d'architecture S1).
app.Map(
    "/api/ia/{**catchAll}",
    (HttpContext ctx, IHttpClientFactory f, IConfiguration c, string? catchAll) =>
        Relayer(ctx, f, c["IaServiceUrl"] ?? "http://localhost:5210/api/ia", catchAll ?? "")
);

app.Map(
    "/api/ia",
    (HttpContext ctx, IHttpClientFactory f, IConfiguration c) =>
        Relayer(ctx, f, c["IaServiceUrl"] ?? "http://localhost:5210/api/ia", "")
);

app.Run();

// <summary>
// Relaie la requête entrante vers un service interne et recopie la réponse telle quelle.
// Une panne du service cible devient un 502 lisible, jamais une exception non gérée.
// </summary>
static async Task Relayer(
    HttpContext context,
    IHttpClientFactory clientFactory,
    string baseUrl,
    string suffix
)
{
    var client = clientFactory.CreateClient();
    // La génération IA peut être longue sur CPU : le Gateway ne doit pas couper avant elle.
    client.Timeout = TimeSpan.FromSeconds(200);

    var targetUrl = string.IsNullOrEmpty(suffix) ? baseUrl : $"{baseUrl.TrimEnd('/')}/{suffix}";

    if (context.Request.QueryString.HasValue)
    {
        targetUrl += context.Request.QueryString.Value;
    }

    var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

    if (context.Request.ContentLength is > 0)
    {
        // Le corps est relayé tel quel, en conservant son Content-Type d'origine.
        // Le forcer en application/json casserait tout envoi multipart/form-data
        // (dépôt de fichier), et la lecture en mémoire interdirait les gros corps.
        requestMessage.Content = new StreamContent(context.Request.Body);

        if (!string.IsNullOrWhiteSpace(context.Request.ContentType)
            && System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(
                context.Request.ContentType, out var typeMedia))
        {
            requestMessage.Content.Headers.ContentType = typeMedia;
        }
    }

    try
    {
        using var response = await client.SendAsync(
            requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

        context.Response.StatusCode = (int)response.StatusCode;

        if (response.Content.Headers.ContentType is not null)
        {
            context.Response.ContentType = response.Content.Headers.ContentType.ToString();
        }

        // Le nom du fichier téléchargé voyage dans cet en-tête : sans lui, le
        // navigateur enregistrerait le document sous le nom de la route.
        if (response.Content.Headers.ContentDisposition is not null)
        {
            context.Response.Headers["Content-Disposition"] =
                response.Content.Headers.ContentDisposition.ToString();
        }

        // Copie en flux : un PDF de plusieurs mégaoctets ne passe pas par une
        // chaîne de caractères.
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
    catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
    {
        context.Response.StatusCode = 504;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            "{\"message\":\"Le service interne a mis trop de temps à répondre.\"}"
        );
    }
    catch (HttpRequestException)
    {
        context.Response.StatusCode = 502;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            "{\"message\":\"Service interne injoignable. Vérifie que les pods métier et IA sont démarrés.\"}"
        );
    }
}

// <summary>Extrait « http://hote:port » d'une URL de service pour composer une route de santé.</summary>
static string RacineDe(string? url, string defaut)
{
    var brut = string.IsNullOrWhiteSpace(url) ? defaut : url;
    return Uri.TryCreate(brut, UriKind.Absolute, out var uri)
        ? $"{uri.Scheme}://{uri.Authority}"
        : brut.TrimEnd('/');
}

static async Task<string> Sonder(HttpClient client, string url)
{
    try
    {
        using var reponse = await client.GetAsync(url);
        return reponse.IsSuccessStatusCode ? "OK" : $"HTTP {(int)reponse.StatusCode}";
    }
    catch (Exception)
    {
        return "injoignable";
    }
}
