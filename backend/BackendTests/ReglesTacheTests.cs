using Api;
using Xunit;

namespace BackendTests;

/// <summary>Les règles pures : aucune base, aucun réseau.</summary>
public class ReglesTacheTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_titre_vide_est_refuse(string? titre)
    {
        Assert.Equal("Le titre est obligatoire.", ReglesTache.ValiderTitre(titre));
    }

    [Fact]
    public void Un_titre_trop_long_est_refuse()
    {
        var titre = new string('a', ReglesTache.LongueurMax + 1);
        Assert.Equal($"Le titre dépasse {ReglesTache.LongueurMax} caractères.", ReglesTache.ValiderTitre(titre));
    }

    [Fact]
    public void Les_espaces_autour_ne_comptent_pas_dans_la_longueur()
    {
        var titre = "  " + new string('a', ReglesTache.LongueurMax) + "  ";
        Assert.Null(ReglesTache.ValiderTitre(titre));
    }

    [Fact]
    public void Un_titre_normal_est_accepte()
    {
        Assert.Null(ReglesTache.ValiderTitre("Relire la chaîne d'intégration"));
    }
}
