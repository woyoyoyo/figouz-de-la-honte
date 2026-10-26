namespace WarhammerFigurines.Services;

/// <summary>Repo GitHub qui contient les dossiers recettes/ et figurines/.</summary>
public record RepoTarget(string Owner, string Repo, string Branch);

/// <summary>
/// Configuration locale (par appareil) : repo cible et token GitHub.
/// Stockée uniquement dans le localStorage — ne transite jamais ailleurs.
/// Sans token, l'app lit le repo public en lecture seule.
/// </summary>
public class SettingsService(LocalStorageService storage)
{
    private const string TargetKey = "wh.repo";
    private const string TokenKey  = "wh.token";

    public static readonly RepoTarget DefaultTarget = new(AppInfo.DefaultOwner, AppInfo.DefaultRepo, "main");

    private bool _loaded;

    public RepoTarget Target { get; private set; } = DefaultTarget;
    public string? Token { get; private set; }
    public bool CanSave => !string.IsNullOrWhiteSpace(Token);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        Target = Parse(await storage.GetAsync(TargetKey)) ?? DefaultTarget;
        Token = await storage.GetAsync(TokenKey);
        _loaded = true;
    }

    public async Task SaveAsync(RepoTarget target, string? token)
    {
        Target = target;
        Token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();

        if (target == DefaultTarget)
            await storage.RemoveAsync(TargetKey);
        else
            await storage.SetAsync(TargetKey, $"{target.Owner}|{target.Repo}|{target.Branch}");

        if (Token is null)
            await storage.RemoveAsync(TokenKey);
        else
            await storage.SetAsync(TokenKey, Token);

        _loaded = true;
    }

    private static RepoTarget? Parse(string? raw)
    {
        var parts = raw?.Split('|');
        return parts is { Length: 3 } ? new RepoTarget(parts[0], parts[1], parts[2]) : null;
    }
}
