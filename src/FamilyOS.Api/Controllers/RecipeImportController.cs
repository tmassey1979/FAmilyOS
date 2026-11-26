using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using FamilyOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/recipes/import-preview")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public sealed class RecipeImportController : ControllerBase
{
    private static readonly Regex JsonLdScript = new(
        "<script\\b[^>]*type\\s*=\\s*[\"']application/ld\\+json[\"'][^>]*>(.*?)</script\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    [HttpPost]
    public async Task<ActionResult<RecipeImportPreviewDto>> Preview(
        [FromBody] RecipeImportRequest body, CancellationToken ct)
    {
        if (!Uri.TryCreate(body.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || string.IsNullOrWhiteSpace(uri.Host))
            return BadRequest("Enter a valid public http or https recipe URL.");

        try
        {
            var html = await DownloadPublicPageAsync(uri, ct);
            foreach (Match match in JsonLdScript.Matches(html))
            {
                var json = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                if (string.IsNullOrWhiteSpace(json)) continue;
                try
                {
                    using var document = JsonDocument.Parse(json);
                    var recipe = FindRecipe(document.RootElement);
                    if (recipe is null) continue;
                    var preview = ParseRecipe(recipe.Value, uri);
                    if (!string.IsNullOrWhiteSpace(preview.Name))
                        return Ok(preview);
                }
                catch (JsonException) { }
            }

            return UnprocessableEntity(new { detail = "This page did not expose a supported recipe card. You can still add the recipe manually." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (HttpRequestException)
        {
            return BadRequest(new { detail = "The recipe website could not be reached. Check the URL and try again." });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(504, new { detail = "The recipe website took too long to respond." });
        }
    }

    private static async Task<string> DownloadPublicPageAsync(Uri initial, CancellationToken ct)
    {
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        handler.ConnectCallback = async (context, token) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            if (addresses.Length == 0 || addresses.Any(IsPrivateAddress))
                throw new HttpRequestException("Only public recipe websites can be imported.");
            Exception? lastError = null;
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex)
                {
                    socket.Dispose();
                    lastError = ex;
                }
            }
            throw new HttpRequestException("Could not connect to the public recipe website.", lastError);
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        var current = initial;
        for (var redirect = 0; redirect <= 4; redirect++)
        {
            await EnsurePublicHostAsync(current, ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("FamilyOS-RecipeImporter/1.0");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                if (redirect == 4 || response.Headers.Location is null)
                    throw new InvalidOperationException("The recipe page redirected too many times.");
                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                if (current.Scheme != Uri.UriSchemeHttps && current.Scheme != Uri.UriSchemeHttp)
                    throw new InvalidOperationException("The recipe page redirected to an unsupported address.");
                continue;
            }

            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length > 2_000_000)
                throw new InvalidOperationException("The recipe page is too large to import.");
            var html = await response.Content.ReadAsStringAsync(ct);
            if (html.Length > 2_000_000)
                throw new InvalidOperationException("The recipe page is too large to import.");
            return html;
        }
        throw new InvalidOperationException("The recipe page could not be loaded.");
    }

    private static async Task EnsurePublicHostAsync(Uri uri, CancellationToken ct)
    {
        var host = uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant();
        if (host == "localhost" || host.EndsWith(".localhost") || host.EndsWith(".local") || host.EndsWith(".internal"))
            throw new InvalidOperationException("Only public recipe websites can be imported.");
        if (IPAddress.TryParse(host, out var literal))
        {
            if (IsPrivateAddress(literal)) throw new InvalidOperationException("Only public recipe websites can be imported.");
            return;
        }

        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0 || addresses.Any(IsPrivateAddress))
            throw new InvalidOperationException("Only public recipe websites can be imported.");
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return true;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && (bytes[1] == 168 || bytes[1] == 0 || bytes[1] == 2))
                || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                || (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19 || bytes[1] == 51))
                || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)
                || bytes[0] >= 224;
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return true;
        return (bytes[0] & 0xe0) != 0x20
            || (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
    }

    private static JsonElement? FindRecipe(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var found = FindRecipe(child);
                if (found is not null) return found;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (IsRecipeType(element))
                return element;
            if (element.TryGetProperty("@graph", out var graph))
            {
                var found = FindRecipe(graph);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static bool IsRecipeType(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type)) return false;
        if (type.ValueKind == JsonValueKind.String)
            return IsRecipeTypeName(type.GetString());
        return type.ValueKind == JsonValueKind.Array
            && type.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String
                && IsRecipeTypeName(x.GetString()));
    }

    private static bool IsRecipeTypeName(string? value)
        => string.Equals(value, "Recipe", StringComparison.OrdinalIgnoreCase)
            || (value?.EndsWith("/Recipe", StringComparison.OrdinalIgnoreCase) ?? false)
            || (value?.EndsWith("#Recipe", StringComparison.OrdinalIgnoreCase) ?? false);

    private static RecipeImportPreviewDto ParseRecipe(JsonElement recipe, Uri source)
    {
        var name = ReadString(recipe, "name") ?? "";
        var description = ReadString(recipe, "description");
        var ingredients = ReadStringArray(recipe, "recipeIngredient");
        var steps = ReadInstructions(recipe);
        var tags = ReadStringArray(recipe, "recipeCategory")
            .Concat(ReadStringArray(recipe, "recipeCuisine"))
            .Concat(ReadStringArray(recipe, "keywords"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        var image = ReadImage(recipe);
        if (Uri.TryCreate(image, UriKind.RelativeOrAbsolute, out var imageUri))
            image = imageUri.IsAbsoluteUri ? imageUri.ToString() : new Uri(source, imageUri).ToString();
        var servings = ReadServings(recipe);
        var prep = ReadDuration(recipe, "prepTime");
        var cook = ReadDuration(recipe, "cookTime");
        return new RecipeImportPreviewDto(name, description, servings, ingredients, steps, tags,
            prep, cook, image, source.ToString());
    }

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() : null;

    private static List<string> ReadStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return new List<string>();
        if (value.ValueKind == JsonValueKind.String)
            return (value.GetString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (value.ValueKind != JsonValueKind.Array) return new List<string>();
        return value.EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();
    }

    private static List<string> ReadInstructions(JsonElement recipe)
    {
        var result = new List<string>();
        if (!recipe.TryGetProperty("recipeInstructions", out var instructions)) return result;
        AddInstruction(instructions, result);
        return result;
    }

    private static void AddInstruction(JsonElement element, List<string> target)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(text)) target.Add(text);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) AddInstruction(item, target);
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (ReadString(element, "text") is { Length: > 0 } text) target.Add(text);
            else if (element.TryGetProperty("itemListElement", out var children)) AddInstruction(children, target);
        }
    }

    private static string? ReadImage(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("image", out var image)) return null;
        if (image.ValueKind == JsonValueKind.String) return image.GetString();
        if (image.ValueKind == JsonValueKind.Array)
        {
            var first = image.EnumerateArray().FirstOrDefault();
            return first.ValueKind == JsonValueKind.String ? first.GetString() : null;
        }
        if (image.ValueKind == JsonValueKind.Object) return ReadString(image, "url");
        return null;
    }

    private static int ReadServings(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("recipeYield", out var value)) return 4;
        var text = value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().FirstOrDefault().ToString() : value.ToString();
        var match = Regex.Match(text, @"\d+");
        return match.Success && int.TryParse(match.Value, out var servings) ? Math.Clamp(servings, 1, 100) : 4;
    }

    private static int? ReadDuration(JsonElement recipe, string property)
    {
        var value = ReadString(recipe, property);
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return (int)Math.Ceiling(XmlConvert.ToTimeSpan(value).TotalMinutes); }
        catch (FormatException) { return null; }
    }
}

public sealed record RecipeImportRequest(string Url);
public sealed record RecipeImportPreviewDto(
    string Name, string? Description, int Servings, List<string> Ingredients, List<string> Steps,
    List<string> Tags, int? PrepMinutes, int? CookMinutes, string? ImageUrl, string SourceUrl);
