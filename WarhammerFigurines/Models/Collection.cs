using System.Text.Json.Serialization;

namespace WarhammerFigurines.Models;

/// <summary>Contenu complet de data/collection.json.</summary>
public class Collection
{
    public int Version { get; set; } = 1;
    public DateTime? LastUpdated { get; set; }
    public List<Paint> Paints { get; set; } = [];
    public List<Recipe> Recipes { get; set; } = [];
    public List<Miniature> Miniatures { get; set; } = [];
}

/// <summary>Un pot de peinture (catalogue + inventaire).</summary>
public class Paint
{
    public string Id { get; set; } = NewId.For("p");
    public string Name { get; set; } = "";
    public string Brand { get; set; } = "Citadel";

    /// <summary>Base, Layer, Shade, Contrast, Dry, Technical, Air, Spray…</summary>
    public string Type { get; set; } = "Base";

    /// <summary>Couleur approximative pour la pastille (#rrggbb).</summary>
    public string Hex { get; set; } = "#808080";

    public bool Owned { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Recette de peinture : une suite d'étapes, éventuellement par zone.</summary>
public class Recipe
{
    public string Id { get; set; } = NewId.For("r");
    public string Name { get; set; } = "";
    public string? Faction { get; set; }
    public string? Description { get; set; }
    public List<RecipeStep> Steps { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

public class RecipeStep
{
    /// <summary>Zone de la figurine (Armure, Peau, Socle…). Vide = toute la figurine.</summary>
    public string? Area { get; set; }

    /// <summary>Sous-couche, Base, Lavis, Brossage à sec, Couche, Éclaircissement…</summary>
    public string Technique { get; set; } = "Base";

    /// <summary>Peinture du catalogue (prioritaire sur PaintName).</summary>
    public string? PaintId { get; set; }

    /// <summary>Peinture libre si elle n'est pas dans le catalogue.</summary>
    public string? PaintName { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Une figurine ou une unité.</summary>
public class Miniature
{
    public string Id { get; set; } = NewId.For("m");
    public string Name { get; set; } = "";

    /// <summary>Warhammer 40,000, Age of Sigmar, Kill Team, Horus Heresy…</summary>
    public string? Game { get; set; }

    public string? Faction { get; set; }
    public int Quantity { get; set; } = 1;
    public MiniStatus Status { get; set; } = MiniStatus.Pile;
    public List<string> RecipeIds { get; set; } = [];
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

public static class NewId
{
    public static string For(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];
}
