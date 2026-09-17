using Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ => new DepotTaches(ChaineConnexion.Depuis(builder.Configuration)));

var app = builder.Build();

app.MapGet("/api/sante", async (DepotTaches depot) =>
    await depot.BaseRepondAsync()
        ? Results.Ok(new { etat = "operationnel", @base = "disponible" })
        : Results.Json(new { etat = "degrade", @base = "indisponible" }, statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapGet("/api/taches", async (DepotTaches depot) => Results.Ok(await depot.ListerAsync()));

app.MapPost("/api/taches", async (NouvelleTache demande, DepotTaches depot) =>
{
    var erreur = ReglesTache.ValiderTitre(demande.Titre);
    if (erreur is not null) return Results.BadRequest(new { erreur });

    var tache = await depot.CreerAsync(demande.Titre!.Trim());
    return Results.Created($"/api/taches/{tache.Id}", tache);
});

app.Run();

/// <summary>Rendu visible pour les tests d'intégration, qui démarrent l'API en mémoire.</summary>
public partial class Program;
