using Microsoft.EntityFrameworkCore;
using MetierService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// La chaîne de connexion n'est jamais écrite dans Git : en local elle vient de
// appsettings.Development.json (non versionné), en Kubernetes de la variable
// d'environnement ConnectionStrings__DefaultConnection injectée par un Secret.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chaîne de connexion PostgreSQL absente. Renseigne ConnectionStrings:DefaultConnection " +
        "(variable d'environnement ConnectionStrings__DefaultConnection, alimentée par le Secret k8s 'postgres-secret').");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Liveness : le conteneur répond.
app.MapGet("/health", () => Results.Ok(new
{
    status = "OK",
    service = "MetierService",
    timestamp = DateTime.UtcNow
}));

// Readiness : PostgreSQL est-il réellement joignable ? Utilisé par la readinessProbe
// k8s pour ne pas router de trafic vers un pod qui ne peut pas servir de données.
app.MapGet("/health/ready", async (AppDbContext db, ILogger<Program> logger) =>
{
    try
    {
        var joignable = await db.Database.CanConnectAsync();
        if (joignable)
        {
            return Results.Ok(new
            {
                status = "OK",
                service = "MetierService",
                postgres = "joignable",
                timestamp = DateTime.UtcNow
            });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "PostgreSQL injoignable");
    }

    return Results.Json(new
    {
        status = "DEGRADED",
        service = "MetierService",
        postgres = "injoignable",
        timestamp = DateTime.UtcNow
    }, statusCode: 503);
});

app.UseAuthorization();
app.MapControllers();

app.Run();
