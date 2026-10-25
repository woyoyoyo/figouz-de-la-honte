using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace WarhammerFigurines.Services;

/// <summary>Contenu d'un fichier du repo + son SHA (requis pour le réécrire).</summary>
public record RepoFile(string Content, string Sha);

/// <summary>
/// Lecture/écriture d'un fichier JSON dans le repo GitHub via l'API Contents.
/// Chaque sauvegarde est un commit : l'historique git sert de journal et de sauvegarde.
/// Le SHA envoyé au PUT protège contre l'écrasement d'une version plus récente (409).
/// </summary>
public class RepoFileService(HttpClient http)
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<RepoFile?> LoadAsync(RepoTarget target, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(target) + $"?ref={target.Branch}");
        AddHeaders(request, token);

        using var response = await http.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureOkAsync(response);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var base64 = root.GetProperty("content").GetString() ?? "";
        var sha = root.GetProperty("sha").GetString() ?? "";
        var content = Encoding.UTF8.GetString(Convert.FromBase64String(base64.Replace("\n", "")));
        return new RepoFile(content, sha);
    }

    /// <summary>Écrit le fichier (un commit). Retourne le nouveau SHA.
    /// Lève <see cref="ConflictException"/> si le fichier a changé entre-temps.</summary>
    public async Task<string> SaveAsync(RepoTarget target, string token, string content, string? sha, string message)
    {
        var payload = new Dictionary<string, string>
        {
            ["message"] = message,
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            ["branch"] = target.Branch
        };
        if (sha is not null)
            payload["sha"] = sha;

        using var request = new HttpRequestMessage(HttpMethod.Put, Url(target))
        {
            Content = JsonContent.Create(payload)
        };
        AddHeaders(request, token);

        using var response = await http.SendAsync(request);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
            throw new ConflictException();
        await EnsureOkAsync(response);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("content").GetProperty("sha").GetString() ?? "";
    }

    private static string Url(RepoTarget t) =>
        $"https://api.github.com/repos/{t.Owner}/{t.Repo}/contents/{t.Path}";

    private static void AddHeaders(HttpRequestMessage request, string? token)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        // Évite le cache navigateur : on veut toujours la dernière version après un commit.
        // (option fetch et non en-tête Cache-Control, que le CORS de GitHub refuse)
        request.SetBrowserRequestCache(BrowserRequestCache.NoCache);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task EnsureOkAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var detail = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "token GitHub invalide ou expiré",
            HttpStatusCode.Forbidden => "accès refusé (droits du token ou limite d'appels atteinte)",
            _ => await response.Content.ReadAsStringAsync()
        };
        throw new HttpRequestException($"GitHub {(int)response.StatusCode} : {detail}");
    }
}

public class ConflictException() : Exception("Le fichier a été modifié ailleurs entre-temps.");
