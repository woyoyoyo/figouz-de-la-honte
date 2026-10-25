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

/// <summary>
/// Source unique des données pour l'UI.
/// Lecture : API GitHub (copie en cache localStorage) → hors-ligne : cache.
/// Écriture : relit toujours la dernière version, applique la modification,
/// puis commit avec le SHA lu (rejoue une fois si le fichier a bougé entre-temps).
/// </summary>
public class CollectionStore(
    RepoFileService repo,
    SettingsService settings,
    LocalStorageService storage,
    IJSRuntime js)
{
    private const string CacheKey = "wh.cache";
    private const string CacheDateKey = "wh.cacheDate";

    public Collection? Data { get; private set; }
    public string? LoadError { get; private set; }
    public DataSource Source { get; private set; } = DataSource.None;
    public DateTimeOffset? LastSync { get; private set; }

    public event Action? Changed;

    public bool CanEdit => settings.CanSave && Source == DataSource.GitHub;

    public async Task EnsureLoadedAsync()
    {
        if (Data is null)
            await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        LoadError = null;
        await settings.EnsureLoadedAsync();

        if (await IsOnlineAsync())
        {
            try
            {
                var file = await repo.LoadAsync(settings.Target, settings.Token);
                var content = file?.Content ?? "";
                Data = Deserialize(content) ?? new Collection();
                Source = DataSource.GitHub;
                await StoreCacheAsync(content);
                Changed?.Invoke();
                return;
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
            }
        }

        if (await LoadFromCacheAsync())
        {
            Changed?.Invoke();
            return;
        }

        Data ??= new Collection();
        Changed?.Invoke();
    }

    /// <param name="applyChange">Modification à appliquer sur les données fraîches.
    /// Retourne false pour annuler (ex : élément introuvable).</param>
    /// <param name="message">Message du commit.</param>
    public async Task<(bool Ok, string? Error)> SaveAsync(Func<Collection, bool> applyChange, string message)
    {
        await settings.EnsureLoadedAsync();

        if (!settings.CanSave)
            return (false, "Token GitHub manquant — configure-le dans Paramètres.");
        if (!await IsOnlineAsync())
            return (false, "Hors ligne — modification impossible sans réseau.");

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var file = await repo.LoadAsync(settings.Target, settings.Token);
                var fresh = (file is null ? null : Deserialize(file.Content)) ?? new Collection();

                if (!applyChange(fresh))
                    return (false, "Modification impossible sur la dernière version des données.");

                fresh.LastUpdated = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(fresh, RepoFileService.JsonOpts) + "\n";
                await repo.SaveAsync(settings.Target, settings.Token!, json, file?.Sha, message);

                Data = fresh;
                Source = DataSource.GitHub;
                await StoreCacheAsync(json);
                Changed?.Invoke();
                return (true, null);
            }
            catch (ConflictException) when (attempt == 0)
            {
                // Quelqu'un a commité entre notre lecture et notre écriture : on rejoue.
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }

    private async Task StoreCacheAsync(string content)
    {
        LastSync = DateTimeOffset.Now;
        await storage.SetAsync(CacheKey, content);
        await storage.SetAsync(CacheDateKey, LastSync.Value.ToString("O"));
    }

    private async Task<bool> LoadFromCacheAsync()
    {
        try
        {
            var cached = await storage.GetAsync(CacheKey);
            if (string.IsNullOrWhiteSpace(cached))
                return false;

            Data = Deserialize(cached);
            Source = DataSource.Cache;
            var cachedDate = await storage.GetAsync(CacheDateKey);
            LastSync = DateTimeOffset.TryParse(cachedDate, out var d) ? d : null;
            return Data is not null;
        }
        catch
        {
            return false;
        }
    }

    private static Collection? Deserialize(string content) =>
        string.IsNullOrWhiteSpace(content)
            ? null
            : JsonSerializer.Deserialize<Collection>(content, RepoFileService.JsonOpts);

    public async Task<bool> IsOnlineAsync()
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

    public Paint? PaintById(string? id) =>
        id is null ? null : Data?.Paints.FirstOrDefault(p => p.Id == id);

    public Recipe? RecipeById(string? id) =>
        id is null ? null : Data?.Recipes.FirstOrDefault(r => r.Id == id);

    public Miniature? MiniatureById(string? id) =>
        id is null ? null : Data?.Miniatures.FirstOrDefault(m => m.Id == id);

    /// <summary>Nom affiché de la peinture d'une étape (catalogue ou texte libre).</summary>
    public string PaintLabel(RecipeStep step) =>
        PaintById(step.PaintId)?.Name ?? step.PaintName ?? "—";

    public IEnumerable<Recipe> RecipesUsing(string paintId) =>
        Data?.Recipes.Where(r => r.Steps.Any(s => s.PaintId == paintId)) ?? [];

    public IEnumerable<Miniature> MiniaturesUsing(string recipeId) =>
        Data?.Miniatures.Where(m => m.RecipeIds.Contains(recipeId)) ?? [];

    /// <summary>Peintures utilisées par une recette mais pas possédées.</summary>
    public IEnumerable<Paint> MissingPaints(Recipe recipe) =>
        recipe.Steps
            .Select(s => PaintById(s.PaintId))
            .Where(p => p is { Owned: false })
            .DistinctBy(p => p!.Id)!;

    /// <summary>Liste de courses : peintures non possédées utilisées par au moins une recette.</summary>
    public IEnumerable<Paint> ShoppingList =>
        (Data?.Recipes ?? []).SelectMany(MissingPaints).DistinctBy(p => p.Id).OrderBy(p => p.Name);

    public IEnumerable<string> Factions =>
        (Data?.Miniatures.Select(m => m.Faction) ?? [])
            .Concat(Data?.Recipes.Select(r => r.Faction) ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order();

    /// <summary>Nombre total de figurines (en tenant compte des quantités).</summary>
    public int CountModels(Func<Miniature, bool>? filter = null) =>
        Data?.Miniatures.Where(filter ?? (_ => true)).Sum(m => m.Quantity) ?? 0;

    /// <summary>Copie profonde, pour éditer sans toucher aux données affichées.</summary>
    public static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, RepoFileService.JsonOpts), RepoFileService.JsonOpts)!;
}
