using MySqlConnector;

namespace Api;

public record Tache(int Id, string Titre, bool Faite, DateTime CreeeLe);

public record NouvelleTache(string? Titre);

public record ChangementTache(bool? Faite, string? Titre);

/// <summary>Ce que le client peut demander de voir.</summary>
public enum Filtre
{
    Toutes,
    AFaire,
    Faites,
}

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

    /// <summary>Le filtre vient de l'adresse : une valeur inconnue ne doit pas faire une erreur 500.</summary>
    public static Filtre LireFiltre(string? valeur) =>
        valeur?.Trim().ToLowerInvariant() switch
        {
            "a-faire" => Filtre.AFaire,
            "faites" => Filtre.Faites,
            _ => Filtre.Toutes,
        };
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
    private const string Colonnes = "id, titre, faite, creee_le";

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

    public async Task<IReadOnlyList<Tache>> ListerAsync(Filtre filtre = Filtre.Toutes)
    {
        var condition = filtre switch
        {
            Filtre.AFaire => " WHERE faite = FALSE",
            Filtre.Faites => " WHERE faite = TRUE",
            _ => "",
        };

        await using var connexion = new MySqlConnection(chaineConnexion);
        await connexion.OpenAsync();
        await using var commande = new MySqlCommand(
            // Les plus récentes d'abord, cochées ou non : une tâche qu'on vient de
            // cocher doit rester sous les yeux.
            $"SELECT {Colonnes} FROM taches{condition} ORDER BY id DESC LIMIT 100", connexion);
        await using var lecteur = await commande.ExecuteReaderAsync();

        var taches = new List<Tache>();
        while (await lecteur.ReadAsync())
        {
            taches.Add(Lire(lecteur));
        }
        return taches;
    }

    /// <summary>Les compteurs sont calculés par la base : une page qui en affiche n'a pas à tout télécharger.</summary>
    public async Task<(int Total, int Faites)> CompterAsync()
    {
        await using var connexion = new MySqlConnection(chaineConnexion);
        await connexion.OpenAsync();
        await using var commande = new MySqlCommand(
            "SELECT COUNT(*), COALESCE(SUM(faite), 0) FROM taches", connexion);
        await using var lecteur = await commande.ExecuteReaderAsync();
        await lecteur.ReadAsync();
        return (lecteur.GetInt32(0), Convert.ToInt32(lecteur.GetDecimal(1)));
    }

    public async Task<Tache?> TrouverAsync(int id)
    {
        await using var connexion = new MySqlConnection(chaineConnexion);
        await connexion.OpenAsync();
        await using var commande = new MySqlCommand($"SELECT {Colonnes} FROM taches WHERE id = @id", connexion);
        commande.Parameters.AddWithValue("@id", id);
        await using var lecteur = await commande.ExecuteReaderAsync();
        return await lecteur.ReadAsync() ? Lire(lecteur) : null;
    }

    public async Task<Tache> CreerAsync(string titre)
    {
        await using var connexion = new MySqlConnection(chaineConnexion);
        await connexion.OpenAsync();
        await using var insertion = new MySqlCommand("INSERT INTO taches (titre) VALUES (@titre)", connexion);
        insertion.Parameters.AddWithValue("@titre", titre);
        await insertion.ExecuteNonQueryAsync();
        var id = (int)insertion.LastInsertedId;

        return (await TrouverAsync(id))!;
    }

    /// <summary>Ne change que ce qui est envoyé : cocher une tâche ne doit pas obliger à renvoyer son titre.</summary>
    public async Task<Tache?> ModifierAsync(int id, ChangementTache changement)
    {
        if (await TrouverAsync(id) is null) return null;

        var affectations = new List<string>();
        await using var connexion = new MySqlConnection(chaineConnexion);
        await connexion.OpenAsync();
        await using var commande = new MySqlCommand { Connection = connexion };

        if (changement.Faite is not null)
        {
            affectations.Add("faite = @faite");
            commande.Parameters.AddWithValue("@faite", changement.Faite.Value);
        }
        if (changement.Titre is not null)
        {
            affectations.Add("titre = @titre");
            commande.Parameters.AddWithValue("@titre", changement.Titre.Trim());
        }
        if (affectations.Count == 0) return await TrouverAsync(id);

        commande.CommandText = $"UPDATE taches SET {string.Join(", ", affectations)} WHERE id = @id";
        commande.Parameters.AddWithValue("@id", id);
        await commande.ExecuteNonQueryAsync();

        return await TrouverAsync(id);
    }

    public async Task<bool> SupprimerAsync(int id)
    {
        await using var connexion = new MySqlConnection(chaineConnexion);
        await connexion.OpenAsync();
        await using var commande = new MySqlCommand("DELETE FROM taches WHERE id = @id", connexion);
        commande.Parameters.AddWithValue("@id", id);
        return await commande.ExecuteNonQueryAsync() > 0;
    }

    private static Tache Lire(MySqlDataReader lecteur) =>
        new(lecteur.GetInt32(0), lecteur.GetString(1), lecteur.GetBoolean(2), lecteur.GetDateTime(3));
}
