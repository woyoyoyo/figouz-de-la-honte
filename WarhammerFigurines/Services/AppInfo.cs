namespace WarhammerFigurines.Services;

public static class AppInfo
{
    /// <summary>À incrémenter à chaque déploiement pour vérifier que le Service Worker
    /// a bien servi la nouvelle version.</summary>
    public const string Version = "1.0.0 🖌️";

    /// <summary>Repo lu par défaut (public : lecture sans token).</summary>
    public const string DefaultOwner = "woyoyoyo";
    public const string DefaultRepo = "figouz-de-la-honte";
}
