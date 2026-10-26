namespace WarhammerFigurines.Models;

/// <summary>Libellés affichés et listes de suggestions pour les formulaires.</summary>
public static class Labels
{
    public static string Of(MiniStatus status) => status switch
    {
        MiniStatus.Pile      => "Pile de la honte",
        MiniStatus.Assembled => "Montée",
        MiniStatus.Primed    => "Sous-couchée",
        MiniStatus.Painting  => "En peinture",
        MiniStatus.Painted   => "Peinte",
        MiniStatus.Based     => "Terminée",
        _ => status.ToString()
    };

    public static string Icon(MiniStatus status) => status switch
    {
        MiniStatus.Pile      => "📦",
        MiniStatus.Assembled => "🔧",
        MiniStatus.Primed    => "🎨",
        MiniStatus.Painting  => "🖌️",
        MiniStatus.Painted   => "✨",
        MiniStatus.Based     => "🏆",
        _ => "•"
    };

    /// <summary>Classe CSS de la pastille de statut.</summary>
    public static string Css(MiniStatus status) => "st-" + status.ToString().ToLowerInvariant();

    public static readonly MiniStatus[] Statuses = Enum.GetValues<MiniStatus>();

    public static readonly string[] Games =
    [
        "Warhammer 40,000", "Age of Sigmar", "Kill Team", "Horus Heresy",
        "Necromunda", "Blood Bowl", "Warcry", "The Old World", "Autre"
    ];

    public static readonly string[] Techniques =
    [
        "Sous-couche", "Base", "Lavis", "Lavis ciblé", "Assombrissement", "Brossage à sec", "Couche",
        "Éclaircissement", "Liseré", "Glacis", "Contrast", "Point de lumière",
        "Effet", "Vernis", "Socle"
    ];

    /// <summary>Marques connues, pour regrouper la page Peintures (« AK Pale Yellow » → AK).</summary>
    public static readonly string[] Brands =
    [
        "Citadel", "Vallejo", "AK", "Army Painter", "Scale75", "Pro Acryl", "Two Thin Coats"
    ];

    public static string BrandOf(string paint) =>
        Brands.FirstOrDefault(b => paint.StartsWith(b + " ", StringComparison.OrdinalIgnoreCase)) ?? "Autres";
}
