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

    private async Task<Tache> CreerAsync(string? titre = null)
    {
        var reponse = await client.PostAsJsonAsync("/api/taches", new { titre = titre ?? $"Tâche de test {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<Tache>())!;
    }

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
        var creee = await CreerAsync($"  {titre}  ");

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

    [Fact]
    public async Task Cocher_une_tache_ne_demande_pas_son_titre()
    {
        var creee = await CreerAsync();

        var modification = await client.PatchAsJsonAsync($"/api/taches/{creee.Id}", new { faite = true });
        Assert.Equal(HttpStatusCode.OK, modification.StatusCode);
        var modifiee = await modification.Content.ReadFromJsonAsync<Tache>();
        Assert.True(modifiee!.Faite);
        Assert.Equal(creee.Titre, modifiee.Titre);
    }

    [Fact]
    public async Task Une_tache_cochee_quitte_le_filtre_a_faire()
    {
        var creee = await CreerAsync();

        var aFaireAvant = await client.GetFromJsonAsync<List<Tache>>("/api/taches?filtre=a-faire");
        Assert.Contains(aFaireAvant!, t => t.Id == creee.Id);

        await client.PatchAsJsonAsync($"/api/taches/{creee.Id}", new { faite = true });

        var aFaireApres = await client.GetFromJsonAsync<List<Tache>>("/api/taches?filtre=a-faire");
        Assert.DoesNotContain(aFaireApres!, t => t.Id == creee.Id);

        var faites = await client.GetFromJsonAsync<List<Tache>>("/api/taches?filtre=faites");
        Assert.Contains(faites!, t => t.Id == creee.Id);
    }

    [Fact]
    public async Task Un_filtre_inconnu_montre_tout_plutot_que_d_echouer()
    {
        var creee = await CreerAsync();
        var reponse = await client.GetAsync("/api/taches?filtre=n-importe-quoi");
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var taches = await reponse.Content.ReadFromJsonAsync<List<Tache>>();
        Assert.Contains(taches!, t => t.Id == creee.Id);
    }

    [Fact]
    public async Task Les_compteurs_suivent_ce_qui_est_coche()
    {
        var avant = await client.GetFromJsonAsync<Compteurs>("/api/taches/compteurs");
        var creee = await CreerAsync();

        var apresCreation = await client.GetFromJsonAsync<Compteurs>("/api/taches/compteurs");
        Assert.Equal(avant!.Total + 1, apresCreation!.Total);
        Assert.Equal(avant.Faites, apresCreation.Faites);

        await client.PatchAsJsonAsync($"/api/taches/{creee.Id}", new { faite = true });

        var apresCoche = await client.GetFromJsonAsync<Compteurs>("/api/taches/compteurs");
        Assert.Equal(apresCreation.Total, apresCoche!.Total);
        Assert.Equal(apresCreation.Faites + 1, apresCoche.Faites);
        Assert.Equal(apresCoche.Total - apresCoche.Faites, apresCoche.AFaire);
    }

    [Fact]
    public async Task Une_tache_supprimee_disparait()
    {
        var creee = await CreerAsync();

        var suppression = await client.DeleteAsync($"/api/taches/{creee.Id}");
        Assert.Equal(HttpStatusCode.NoContent, suppression.StatusCode);

        var relecture = await client.GetAsync($"/api/taches/{creee.Id}");
        Assert.Equal(HttpStatusCode.NotFound, relecture.StatusCode);
    }

    [Fact]
    public async Task Une_tache_inconnue_repond_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/taches/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/taches/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync("/api/taches/999999", new { faite = true })).StatusCode);
    }

    [Fact]
    public async Task Un_titre_vide_est_refuse_aussi_a_la_modification()
    {
        var creee = await CreerAsync();
        var reponse = await client.PatchAsJsonAsync($"/api/taches/{creee.Id}", new { titre = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
    }

    private record Compteurs(int Total, int Faites, int AFaire);
}
