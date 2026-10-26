namespace WarhammerFigurines.Services;

public static class AppInfo
{
    /// <summary>À incrémenter à chaque déploiement pour vérifier que le Service Worker
    /// a bien servi la nouvelle version.</summary>
    public const string Version = "3.0.0 🖌️";

    /// <summary>Repo public dont l'app lit les dossiers recettes/ et figurines/.</summary>
    public const string Owner = "woyoyoyo";
    public const string Repo = "figouz-de-la-honte";
    public const string Branch = "main";

    public const string RepoUrl = $"https://github.com/{Owner}/{Repo}";
}
