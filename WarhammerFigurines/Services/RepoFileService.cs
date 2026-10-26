using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace WarhammerFigurines.Services;

/// <summary>Un fichier du repo : chemin + SHA du blob git.</summary>
public record RepoEntry(string Path, string Sha);

/// <summary>
/// Lecture seule des fichiers du repo public GitHub, sans token.
/// 2 appels API (dernier commit + arborescence), puis le contenu de chaque fichier
/// via raw.githubusercontent.com, figé sur le commit (pas de cache périmé, pas de quota).
/// </summary>
public class RepoFileService(HttpClient http)
{
    private const string Api = "https://api.github.com/repos/" + AppInfo.Owner + "/" + AppInfo.Repo;

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>SHA du dernier commit de la branche.</summary>
    public async Task<string> GetHeadAsync()
    {
        using var doc = await GetJsonAsync($"{Api}/git/ref/heads/{AppInfo.Branch}");
        return doc.RootElement.GetProperty("object").GetProperty("sha").GetString()!;
    }

    /// <summary>Tous les fichiers du repo à ce commit.</summary>
    public async Task<List<RepoEntry>> ListAsync(string commitSha)
    {
        using var doc = await GetJsonAsync($"{Api}/git/trees/{commitSha}?recursive=1");
        return doc.RootElement.GetProperty("tree").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "blob")
            .Select(e => new RepoEntry(e.GetProperty("path").GetString()!, e.GetProperty("sha").GetString()!))
            .ToList();
    }

    public Task<string> ReadAsync(string commitSha, RepoEntry entry)
    {
        var path = string.Join('/', entry.Path.Split('/').Select(Uri.EscapeDataString));
        return http.GetStringAsync($"https://raw.githubusercontent.com/{AppInfo.Owner}/{AppInfo.Repo}/{commitSha}/{path}");
    }

    private async Task<JsonDocument> GetJsonAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        // Toujours le dernier commit (option fetch et non en-tête Cache-Control, que le CORS de GitHub refuse).
        request.SetBrowserRequestCache(BrowserRequestCache.NoCache);

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var detail = response.StatusCode switch
            {
                HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests => "trop de rechargements, réessaie dans un moment",
                HttpStatusCode.NotFound => "repo ou branche introuvable",
                _ => response.ReasonPhrase
            };
            throw new HttpRequestException($"GitHub {(int)response.StatusCode} : {detail}");
        }
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
