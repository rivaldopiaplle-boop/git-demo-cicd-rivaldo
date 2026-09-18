using Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ => new DepotTaches(ChaineConnexion.Depuis(builder.Configuration)));

var app = builder.Build();

// APPLIQUER_SCHEMA=1 : la base est préparée au démarrage. On l'active chez
// l'hébergeur, où aucune migration n'est lancée à la main ; ailleurs, c'est
// mysql/bootstrap-mysql.sh ou les tests qui appliquent le même fichier.
if (builder.Configuration["APPLIQUER_SCHEMA"] == "1")
{
    var depot = app.Services.GetRequiredService<DepotTaches>();
    var chemin = Path.Combine(AppContext.BaseDirectory, "schema.sql");
    for (var essai = 1; essai <= 30; essai += 1)
    {
        try
        {
            await depot.AppliquerSchemaAsync(await File.ReadAllTextAsync(chemin));
            app.Logger.LogInformation("Schéma appliqué au démarrage.");
            break;
        }
        catch (Exception erreur) when (essai < 30)
        {
            // La base met parfois du temps à accepter les connexions.
            app.Logger.LogWarning("Base pas encore prête ({Essai}/30) : {Message}", essai, erreur.Message);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

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
