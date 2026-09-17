using MySqlConnector;

namespace Api;

public record Tache(int Id, string Titre, bool Faite, DateTime CreeeLe);

public record NouvelleTache(string? Titre);

/// <summary>
/// Les règles d'une tâche. Elles vivent côté serveur : le formulaire du front les
/// reprend pour le confort, mais seul le serveur décide.
/// </summary>
public static class ReglesTache
{
    public const int LongueurMax = 200;

    /// <returns>Le message d'erreur, ou <c>null</c> si le titre est acceptable.</returns>
    public static string? ValiderTitre(string? titre)
    {
        if (string.IsNullOrWhiteSpace(titre)) return "Le titre est obligatoire.";
        if (titre.Trim().Length > LongueurMax) return $"Le titre dépasse {LongueurMax} caractères.";
        return null;
    }
}

/// <summary>
/// La connexion se construit depuis les variables d'environnement : la chaîne
/// d'intégration, docker compose et un poste de travail les posent chacun à leur
/// manière, et aucun mot de passe n'est écrit dans le dépôt.
/// </summary>
public static class ChaineConnexion
{
    public static string Depuis(IConfiguration configuration) =>
        new MySqlConnectionStringBuilder
        {
            Server = configuration["DB_URL"] ?? "localhost",
            Port = uint.TryParse(configuration["DB_PORT"], out var port) ? port : 3306,
            UserID = configuration["DB_USERNAME"] ?? "root",
            Password = configuration["DB_PASSWORD"] ?? "",
            Database = configuration["DB_DATABASE"] ?? "taches",
        }.ConnectionString;
}

public class DepotTaches(string chaineConnexion)
{
    /// <summary>La sonde de santé interroge vraiment la base : un pool configuré ne prouve pas qu'elle répond.</summary>
    public async Task<bool> BaseRepondAsync()
    {
        try
        {
            await using var connexion = new MySqlConnection(chaineConnexion);
            await connexion.OpenAsync();
            await using var commande = new MySqlCommand("SELECT 1", connexion);
            await commande.ExecuteScalarAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<Tache>> ListerAsync()
    {
        await using var connexion = new MySqlConnection(chaineConnexion);
        await connexion.OpenAsync();
        await using var commande = new MySqlCommand(
            "SELECT id, titre, faite, creee_le FROM taches ORDER BY id DESC LIMIT 100", connexion);
        await using var lecteur = await commande.ExecuteReaderAsync();

        var taches = new List<Tache>();
        while (await lecteur.ReadAsync())
        {
            taches.Add(new Tache(lecteur.GetInt32(0), lecteur.GetString(1), lecteur.GetBoolean(2), lecteur.GetDateTime(3)));
        }
        return taches;
    }

    public async Task<Tache> CreerAsync(string titre)
    {
        await using var connexion = new MySqlConnection(chaineConnexion);
        await connexion.OpenAsync();
        await using var insertion = new MySqlCommand("INSERT INTO taches (titre) VALUES (@titre)", connexion);
        insertion.Parameters.AddWithValue("@titre", titre);
        await insertion.ExecuteNonQueryAsync();
        var id = (int)insertion.LastInsertedId;

        await using var lecture = new MySqlCommand("SELECT id, titre, faite, creee_le FROM taches WHERE id = @id", connexion);
        lecture.Parameters.AddWithValue("@id", id);
        await using var lecteur = await lecture.ExecuteReaderAsync();
        await lecteur.ReadAsync();
        return new Tache(lecteur.GetInt32(0), lecteur.GetString(1), lecteur.GetBoolean(2), lecteur.GetDateTime(3));
    }
}
