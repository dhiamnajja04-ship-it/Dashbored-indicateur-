using IaService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// --- Client du service métier (jamais de connexion directe à PostgreSQL depuis l'IA) ---
// URL interne Kubernetes surchargeable : MetierServiceUrl / MetierService__BaseUrl.
var metierUrl = builder.Configuration["MetierServiceUrl"] ?? "http://localhost:5039/api/indicators/";
if (!metierUrl.EndsWith('/'))
{
    metierUrl += "/"; // sinon HttpClient écrase le dernier segment de BaseAddress
}
builder.Services.AddHttpClient<MetierClient>(client =>
{
    client.BaseAddress = new Uri(metierUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// --- Client du modèle local (Ollama) ---
var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
builder.Services.AddHttpClient<OllamaClient>(client =>
{
    client.BaseAddress = new Uri(ollamaUrl);
    // La génération sur CPU est lente : on laisse largement le temps au modèle.
    var timeout = int.TryParse(builder.Configuration["Ollama:TimeoutSeconds"], out var t) ? t : 180;
    client.Timeout = TimeSpan.FromSeconds(timeout);
});

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Liveness : le conteneur tourne.
app.MapMethods(
    "/health",
    new[] { "GET", "HEAD" },
    () => Results.Ok(new
    {
        status = "OK",
        service = "IaService",
        // Nom du conteneur ou du pod : rend la répartition observable.
        instance = Environment.MachineName,
        timestamp = DateTime.UtcNow,
    })
);

// Readiness : le modèle local répond-il réellement ?
app.MapMethods(
    "/health/ready",
    new[] { "GET", "HEAD" },
    async (OllamaClient ollama, CancellationToken ct) =>
    {
        var ollamaOk = await ollama.EstJoignableAsync(ct);
        var corps = new
        {
            status = ollamaOk ? "OK" : "DEGRADED",
            service = "IaService",
            instance = Environment.MachineName,
            modele = ollama.Modele,
            ollama = ollamaOk ? "joignable" : "injoignable",
            timestamp = DateTime.UtcNow,
        };
        return ollamaOk ? Results.Ok(corps) : Results.Json(corps, statusCode: 503);
    }
);

app.MapControllers();

app.Run();
