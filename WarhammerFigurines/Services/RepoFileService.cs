using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace WarhammerFigurines.Services;

/// <summary>Un fichier du repo : chemin + SHA du blob git.</summary>
public record RepoEntry(string Path, string Sha);

/// <summary>
/// Accès aux fichiers du repo GitHub.
/// Lecture : 2 appels API (dernier commit + arborescence), puis le contenu de chaque fichier
/// via raw.githubusercontent.com (sans quota) ou l'API blobs si un token est configuré.
/// Écriture : API Contents, un commit par fichier ; le SHA envoyé protège contre
/// l'écrasement d'une version modifiée ailleurs (409).
/// </summary>
public class RepoFileService(HttpClient http)
{
    private const string Api = "https://api.github.com/repos/";

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>SHA du dernier commit de la branche.</summary>
    public async Task<string> GetHeadAsync(RepoTarget t, string? token)
    {
        using var doc = await GetJsonAsync($"{Api}{t.Owner}/{t.Repo}/git/ref/heads/{t.Branch}", token);
        return doc.RootElement.GetProperty("object").GetProperty("sha").GetString()!;
    }

    /// <summary>Tous les fichiers du repo à ce commit.</summary>
    public async Task<List<RepoEntry>> ListAsync(RepoTarget t, string commitSha, string? token)
    {
        using var doc = await GetJsonAsync($"{Api}{t.Owner}/{t.Repo}/git/trees/{commitSha}?recursive=1", token);
        return doc.RootElement.GetProperty("tree").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "blob")
            .Select(e => new RepoEntry(e.GetProperty("path").GetString()!, e.GetProperty("sha").GetString()!))
            .ToList();
    }

    public async Task<string> ReadAsync(RepoTarget t, string commitSha, RepoEntry entry, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            using var doc = await GetJsonAsync($"{Api}{t.Owner}/{t.Repo}/git/blobs/{entry.Sha}", token);
            return Decode(doc.RootElement.GetProperty("content").GetString());
        }

        // URL figée sur le commit : pas de cache périmé, et pas de quota API.
        var path = string.Join('/', entry.Path.Split('/').Select(Uri.EscapeDataString));
        return await http.GetStringAsync($"https://raw.githubusercontent.com/{t.Owner}/{t.Repo}/{commitSha}/{path}");
    }

    /// <summary>Crée ou remplace un fichier (un commit). Retourne le SHA du nouveau blob.
    /// Lève <see cref="ConflictException"/> si le fichier a changé entre-temps.</summary>
    public async Task<string> WriteAsync(RepoTarget t, string token, string path, string content, string? sha, string message)
    {
        var payload = new Dictionary<string, string>
        {
            ["message"] = message,
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            ["branch"] = t.Branch
        };
        if (sha is not null)
            payload["sha"] = sha;

        using var doc = await SendAsync(HttpMethod.Put, ContentsUrl(t, path), token, payload);
        return doc.RootElement.GetProperty("content").GetProperty("sha").GetString()!;
    }

    public async Task DeleteAsync(RepoTarget t, string token, string path, string sha, string message)
    {
        var payload = new Dictionary<string, string> { ["message"] = message, ["sha"] = sha, ["branch"] = t.Branch };
        using var _ = await SendAsync(HttpMethod.Delete, ContentsUrl(t, path), token, payload);
    }

    private static string ContentsUrl(RepoTarget t, string path) => $"{Api}{t.Owner}/{t.Repo}/contents/{path}";

    private static string Decode(string? base64) =>
        Encoding.UTF8.GetString(Convert.FromBase64String((base64 ?? "").Replace("\n", "")));

    private async Task<JsonDocument> GetJsonAsync(string url, string? token) =>
        await SendAsync(HttpMethod.Get, url, token, null);

    private async Task<JsonDocument> SendAsync(HttpMethod method, string url, string? token, object? payload)
    {
        using var request = new HttpRequestMessage(method, url);
        if (payload is not null)
            request.Content = JsonContent.Create(payload);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        // Toujours la dernière version après un commit
        // (option fetch et non en-tête Cache-Control, que le CORS de GitHub refuse).
        request.SetBrowserRequestCache(BrowserRequestCache.NoCache);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request);
        if (method != HttpMethod.Get && response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
            throw new ConflictException();
        if (!response.IsSuccessStatusCode)
        {
            var detail = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "token GitHub invalide ou expiré",
                HttpStatusCode.Forbidden => "accès refusé (droits du token ou limite d'appels atteinte)",
                HttpStatusCode.NotFound => "repo ou branche introuvable",
                _ => await response.Content.ReadAsStringAsync()
            };
            throw new HttpRequestException($"GitHub {(int)response.StatusCode} : {detail}");
        }

        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }
}

public class ConflictException() : Exception("Ce fichier a été modifié ailleurs entre-temps — recharge puis recommence.");
