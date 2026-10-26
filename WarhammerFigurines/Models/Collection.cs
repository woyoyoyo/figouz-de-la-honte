using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace WarhammerFigurines.Models;

/// <summary>
/// Un fichier du repo (recettes/xxx.json ou figurines/xxx.json).
/// Le nom du fichier sert d'identifiant ; le SHA est requis pour le réécrire.
/// </summary>
public abstract class RepoItem
{
    /// <summary>Nom du fichier sans extension (ex : archers-sylvains).</summary>
    [JsonIgnore] public string Slug { get; set; } = "";

    /// <summary>SHA du blob git lu — null pour un fichier pas encore créé.</summary>
    [JsonIgnore] public string? Sha { get; set; }
}

/// <summary>Recette de peinture : une suite d'étapes, éventuellement par zone.</summary>
public class Recipe : RepoItem
{
    public const string Folder = "recettes";

    public string Name { get; set; } = "";
    public string? Faction { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<RecipeStep> Steps { get; set; } = [];
}

public class RecipeStep
{
    /// <summary>Zone de la figurine (Capes, Peau, Socle…). Vide = toute la figurine.</summary>
    public string? Area { get; set; }

    /// <summary>Sous-couche, Base, Lavis, Brossage à sec, Éclaircissement…</summary>
    public string Technique { get; set; } = "Base";

    /// <summary>Peinture en texte libre, marque comprise (ex : « AK Pale Yellow »).</summary>
    public string Paint { get; set; } = "";

    public string? Notes { get; set; }
}

/// <summary>Une figurine ou une unité.</summary>
public class Miniature : RepoItem
{
    public const string Folder = "figurines";

    public string Name { get; set; } = "";

    /// <summary>Warhammer 40,000, Age of Sigmar, The Old World…</summary>
    public string? Game { get; set; }

    public string? Faction { get; set; }
    public int Quantity { get; set; } = 1;
    public MiniStatus Status { get; set; } = MiniStatus.Pile;

    /// <summary>Recettes utilisées (noms de fichiers dans recettes/).</summary>
    public List<string> Recipes { get; set; } = [];

    public string? Notes { get; set; }
    public string? PhotoUrl { get; set; }
    public DateOnly? Added { get; set; }
}

/// <summary>Avancement d'une figurine, dans l'ordre du hobby.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MiniStatus>))]
public enum MiniStatus
{
    Pile,
    Assembled,
    Primed,
    Painting,
    Painted,
    Based
}

public static class Slug
{
    /// <summary>« Archers Sylvains » → « archers-sylvains » (sans accents, utilisable comme nom de fichier).</summary>
    public static string From(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        }
        var slug = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length == 0 ? "sans-nom" : slug[..Math.Min(slug.Length, 60)];
    }

    /// <summary>Slug unique parmi ceux déjà pris (ajoute -2, -3…).</summary>
    public static string Unique(string text, IEnumerable<string> taken)
    {
        var set = taken.ToHashSet();
        var baseSlug = From(text);
        var slug = baseSlug;
        for (var i = 2; set.Contains(slug); i++)
            slug = $"{baseSlug}-{i}";
        return slug;
    }
}
