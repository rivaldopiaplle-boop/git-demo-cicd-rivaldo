using Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ => new DepotTaches(ChaineConnexion.Depuis(builder.Configuration)));

var app = builder.Build();

app.MapGet("/api/sante", async (DepotTaches depot) =>
    await depot.BaseRepondAsync()
        ? Results.Ok(new { etat = "operationnel", @base = "disponible" })
        : Results.Json(new { etat = "degrade", @base = "indisponible" }, statusCode: StatusCodes.Status503ServiceUnavailable));

// Le filtre se donne dans l'adresse : ?filtre=a-faire, ?filtre=faites, ou rien.
app.MapGet("/api/taches", async (DepotTaches depot, string? filtre) =>
    Results.Ok(await depot.ListerAsync(ReglesTache.LireFiltre(filtre))));

app.MapGet("/api/taches/compteurs", async (DepotTaches depot) =>
{
    var (total, faites) = await depot.CompterAsync();
    return Results.Ok(new { total, faites, aFaire = total - faites });
});

app.MapGet("/api/taches/{id:int}", async (int id, DepotTaches depot) =>
    await depot.TrouverAsync(id) is { } tache ? Results.Ok(tache) : Results.NotFound(new { erreur = "Tâche inconnue." }));

app.MapPost("/api/taches", async (NouvelleTache demande, DepotTaches depot) =>
{
    var erreur = ReglesTache.ValiderTitre(demande.Titre);
    if (erreur is not null) return Results.BadRequest(new { erreur });

    var tache = await depot.CreerAsync(demande.Titre!.Trim());
    return Results.Created($"/api/taches/{tache.Id}", tache);
});

app.MapPatch("/api/taches/{id:int}", async (int id, ChangementTache changement, DepotTaches depot) =>
{
    // Un titre absent n'est pas un titre vide : on ne valide que ce qui est envoyé.
    if (changement.Titre is not null)
    {
        var erreur = ReglesTache.ValiderTitre(changement.Titre);
        if (erreur is not null) return Results.BadRequest(new { erreur });
    }

    return await depot.ModifierAsync(id, changement) is { } tache
        ? Results.Ok(tache)
        : Results.NotFound(new { erreur = "Tâche inconnue." });
});

app.MapDelete("/api/taches/{id:int}", async (int id, DepotTaches depot) =>
    await depot.SupprimerAsync(id) ? Results.NoContent() : Results.NotFound(new { erreur = "Tâche inconnue." }));

app.Run();

/// <summary>Rendu visible pour les tests d'intégration, qui démarrent l'API en mémoire.</summary>
public partial class Program;
