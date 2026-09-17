using System.Net;
using System.Net.Http.Json;
using Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Xunit;

namespace BackendTests;

/// <summary>
/// Prépare une vraie base MySQL avant les tests : crée la base si besoin et lui
/// applique le schéma du dépôt, le même que celui de la migration de la pile.
/// La chaîne fournit MySQL comme service ; en local, un conteneur suffit.
/// </summary>
public sealed class BaseDeTest : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var complete = new MySqlConnectionStringBuilder(ChaineConnexion.Depuis(configuration));
        var nomBase = complete.Database;

        var sansBase = new MySqlConnectionStringBuilder(complete.ConnectionString) { Database = "" };
        await using (var connexion = new MySqlConnection(sansBase.ConnectionString))
        {
            await connexion.OpenAsync();
            await using var creation = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{nomBase}`", connexion);
            await creation.ExecuteNonQueryAsync();
        }

        await using (var connexion = new MySqlConnection(complete.ConnectionString))
        {
            await connexion.OpenAsync();
            await using var schema = new MySqlCommand(await File.ReadAllTextAsync(TrouverSchema()), connexion);
            await schema.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Remonte depuis le dossier des binaires jusqu'à la racine du dépôt.</summary>
    private static string TrouverSchema()
    {
        for (var dossier = new DirectoryInfo(AppContext.BaseDirectory); dossier is not null; dossier = dossier.Parent)
        {
            var chemin = Path.Combine(dossier.FullName, "mysql", "schema.sql");
            if (File.Exists(chemin)) return chemin;
        }
        throw new FileNotFoundException("mysql/schema.sql introuvable depuis " + AppContext.BaseDirectory);
    }
}

/// <summary>L'API démarrée en mémoire, parlant à la vraie base : ce que le client verra.</summary>
// BaseDeTest n'est pas injectée : xUnit la crée et attend sa préparation avant
// le premier test, ce qui suffit ici.
public class ApiMySqlTests(WebApplicationFactory<Program> fabrique)
    : IClassFixture<BaseDeTest>, IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client = fabrique.CreateClient();

    [Fact]
    public async Task La_sonde_de_sante_interroge_la_base()
    {
        var reponse = await client.GetAsync("/api/sante");
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        // « indisponible » contient « disponible » : on compare la paire complète.
        Assert.Contains("\"base\":\"disponible\"", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Une_tache_creee_se_retrouve_dans_la_liste()
    {
        var titre = $"Tâche de test {Guid.NewGuid():N}";

        var creation = await client.PostAsJsonAsync("/api/taches", new { titre = $"  {titre}  " });
        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        var creee = await creation.Content.ReadFromJsonAsync<Tache>();
        Assert.NotNull(creee);
        Assert.Equal(titre, creee.Titre);
        Assert.False(creee.Faite);

        var liste = await client.GetFromJsonAsync<List<Tache>>("/api/taches");
        Assert.Contains(liste!, t => t.Id == creee.Id && t.Titre == titre);
    }

    [Fact]
    public async Task Le_serveur_refuse_un_titre_vide_meme_si_le_formulaire_est_contourne()
    {
        var reponse = await client.PostAsJsonAsync("/api/taches", new { titre = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("obligatoire", await reponse.Content.ReadAsStringAsync());
    }
}
