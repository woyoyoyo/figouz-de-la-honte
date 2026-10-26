using System.Text.Json;
using Microsoft.JSInterop;
using WarhammerFigurines.Models;

namespace WarhammerFigurines.Services;

/// <summary>Provenance des données actuellement affichées.</summary>
public enum DataSource
{
    None,
    GitHub,
    Cache
}

/// <summary>Copie locale d'un fichier du repo (cache hors-ligne, réutilisée tant que le SHA ne change pas).</summary>
public record CachedFile(string Sha, string Content);

/// <summary>
/// Source unique des données pour l'UI, en lecture seule : un fichier JSON par recette (recettes/)
/// et par figurine (figurines/). Le nom du fichier est l'identifiant.
/// GitHub → cache localStorage ; hors-ligne → cache.
/// </summary>
public class CollectionStore(RepoFileService repo, LocalStorageService storage, IJSRuntime js)
{
    private const string CacheKey = "wh.files";
    private const string CacheDateKey = "wh.filesDate";

    private Dictionary<string, CachedFile> _files = [];

    public List<Recipe> Recipes { get; private set; } = [];
    public List<Miniature> Miniatures { get; private set; } = [];
    public bool Loaded { get; private set; }

    public string? LoadError { get; private set; }

    /// <summary>Fichiers illisibles (JSON invalide).</summary>
    public List<string> BrokenFiles { get; } = [];

    public DataSource Source { get; private set; } = DataSource.None;
    public DateTimeOffset? LastSync { get; private set; }

    public event Action? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (!Loaded)
            await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        LoadError = null;

        if (await IsOnlineAsync())
        {
            try
            {
                await LoadFromGitHubAsync();
                Source = DataSource.GitHub;
                Done();
                return;
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
            }
        }

        if (await LoadFromCacheAsync())
            Source = DataSource.Cache;
        Done();

        void Done()
        {
            Loaded = true;
            Changed?.Invoke();
        }
    }

    private async Task LoadFromGitHubAsync()
    {
        var head = await repo.GetHeadAsync();
        var entries = (await repo.ListAsync(head)).Where(IsDataFile).ToList();

        var old = _files.Count > 0 ? _files : await ReadCacheAsync();
        var contents = await Task.WhenAll(entries.Select(async e =>
            old.TryGetValue(e.Path, out var c) && c.Sha == e.Sha
                ? c.Content
                : await repo.ReadAsync(head, e)));

        _files = entries.Zip(contents).ToDictionary(x => x.First.Path, x => new CachedFile(x.First.Sha, x.Second));
        Parse();

        LastSync = DateTimeOffset.Now;
        await storage.SetAsync(CacheKey, JsonSerializer.Serialize(_files));
        await storage.SetAsync(CacheDateKey, LastSync.Value.ToString("O"));
    }

    private static bool IsDataFile(RepoEntry e) =>
        e.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        && (e.Path.StartsWith(Recipe.Folder + "/") || e.Path.StartsWith(Miniature.Folder + "/"))
        && e.Path.Count(c => c == '/') == 1;

    private void Parse()
    {
        BrokenFiles.Clear();
        Recipes = ParseFolder<Recipe>(Recipe.Folder).OrderBy(r => r.Name).ToList();
        Miniatures = ParseFolder<Miniature>(Miniature.Folder).OrderBy(m => m.Name).ToList();
    }

    private IEnumerable<T> ParseFolder<T>(string folder) where T : RepoItem
    {
        foreach (var (path, file) in _files.Where(f => f.Key.StartsWith(folder + "/")))
        {
            T? item = null;
            try { item = JsonSerializer.Deserialize<T>(file.Content, RepoFileService.JsonOpts); }
            catch (JsonException) { }

            if (item is null)
            {
                BrokenFiles.Add(path);
                continue;
            }
            item.Slug = Path.GetFileNameWithoutExtension(path);
            yield return item;
        }
    }

    private async Task<Dictionary<string, CachedFile>> ReadCacheAsync()
    {
        try
        {
            var raw = await storage.GetAsync(CacheKey);
            return string.IsNullOrWhiteSpace(raw) ? [] : JsonSerializer.Deserialize<Dictionary<string, CachedFile>>(raw) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<bool> LoadFromCacheAsync()
    {
        _files = await ReadCacheAsync();
        if (_files.Count == 0)
            return false;
        Parse();
        var cachedDate = await storage.GetAsync(CacheDateKey);
        LastSync = DateTimeOffset.TryParse(cachedDate, out var d) ? d : null;
        return true;
    }

    private async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await js.InvokeAsync<bool>("hobby.isOnline");
        }
        catch
        {
            return true; // au bénéfice du doute, on tentera le fetch
        }
    }

    // ── Requêtes pour l'UI ───────────────────────────────────────────────

    public Recipe? RecipeBySlug(string? slug) => Recipes.FirstOrDefault(r => r.Slug == slug);

    public Miniature? MiniatureBySlug(string? slug) => Miniatures.FirstOrDefault(m => m.Slug == slug);

    public IEnumerable<Miniature> MiniaturesUsing(string recipeSlug) =>
        Miniatures.Where(m => m.Recipes.Contains(recipeSlug));

    /// <summary>Toutes les peintures citées dans les recettes, avec les recettes qui les utilisent.</summary>
    public IEnumerable<(string Paint, List<Recipe> Recipes)> PaintsUsed =>
        Recipes.SelectMany(r => r.Steps.Select(s => (Paint: s.Paint.Trim(), Recipe: r)))
            .Where(x => x.Paint.Length > 0)
            .GroupBy(x => x.Paint, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.First().Paint, g.Select(x => x.Recipe).DistinctBy(r => r.Slug).ToList()))
            .OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> Factions =>
        Miniatures.Select(m => m.Faction)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order();

    /// <summary>Nombre total de figurines (en tenant compte des quantités).</summary>
    public int CountModels(Func<Miniature, bool>? filter = null) =>
        Miniatures.Where(filter ?? (_ => true)).Sum(m => m.Quantity);
}
