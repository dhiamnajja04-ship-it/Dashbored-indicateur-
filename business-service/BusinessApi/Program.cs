using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5186");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ?? "Host=localhost;Database=indicateurs_db;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

app.UseCors("AllowAll");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Création garantie de la table si elle n'existe pas
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS indicateurs (
            id SERIAL PRIMARY KEY,
            nom_indicateur TEXT,
            valeur DOUBLE PRECISION,
            periode TEXT,
            organisation TEXT
        );
    ");

    if (!db.Indicateurs.Any())
    {
        db.Indicateurs.AddRange(
            new Indicateur { NomIndicateur = "Chiffre d'Affaires", Valeur = 95.5, Periode = "Année 2026", Organisation = "Ariana" },
            new Indicateur { NomIndicateur = "Taux de Performance", Valeur = 78.4, Periode = "Mois en cours", Organisation = "Ariana" }
        );
        db.SaveChanges();
    }
}

app.MapGet("/api/indicateurs", async (AppDbContext db) =>
    await db.Indicateurs.ToListAsync());

app.Run();
