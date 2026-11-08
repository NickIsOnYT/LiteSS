using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LiteSS;

internal interface ITranslationProvider
{
    string DisplayName { get; }

    Task<string> TranslateAsync(
        string text,
        string source,
        string target,
        CancellationToken cancellationToken);
}

internal static class TranslationService
{
    private static readonly HttpClient Client = CreateClient();

    public static string DisplayName(TranslationProviderKind kind) => kind switch
    {
        TranslationProviderKind.Google => "Google Translate",
        TranslationProviderKind.LibreTranslate => "LibreTranslate",
        TranslationProviderKind.DeepL => "DeepL",
        TranslationProviderKind.PythonArgos => "Argos Translate",
        TranslationProviderKind.PythonLibreTranslate => "LibreTranslate (Python)",
        _ => "None",
    };

    public static ITranslationProvider Create(AppSettings settings) => settings.TranslationProvider switch
    {
        TranslationProviderKind.Google => new GoogleTranslateProvider(Client),
        TranslationProviderKind.LibreTranslate => new LibreTranslateProvider(
            Client,
            settings.LibreTranslateUrl,
            settings.LibreTranslateApiKey),
        TranslationProviderKind.DeepL => new DeepLProvider(Client, settings.DeepLApiKey),
        TranslationProviderKind.PythonArgos => new PythonTranslateProvider(
            settings,
            "argos",
            DisplayName(TranslationProviderKind.PythonArgos)),
        TranslationProviderKind.PythonLibreTranslate => new PythonTranslateProvider(
            settings,
            "libretranslate",
            DisplayName(TranslationProviderKind.PythonLibreTranslate)),
        _ => throw new InvalidOperationException("No translation provider is selected."),
    };

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LiteSS/1.0");
        return client;
    }

    private static string DescribeError(string json, string service, HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String)
            {
                var message = error.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return $"{service} returned HTTP {(int)response.StatusCode}: {message}";
                }
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic message.
        }

        return $"{service} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
    }

    private sealed class GoogleTranslateProvider : ITranslationProvider
    {
        private readonly HttpClient _http;

        public GoogleTranslateProvider(HttpClient http) => _http = http;

        public string DisplayName => "Google Translate";

        public async Task<string> TranslateAsync(
            string text,
            string source,
            string target,
            CancellationToken cancellationToken)
        {
            var fields = new Dictionary<string, string>
            {
                ["client"] = "gtx",
                ["sl"] = string.IsNullOrWhiteSpace(source) ? "auto" : source,
                ["tl"] = target,
                ["dt"] = "t",
                ["q"] = text,
            };

            using var content = new FormUrlEncodedContent(fields);
            using var response = await _http.PostAsync(
                "https://translate.googleapis.com/translate_a/single",
                content,
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Google Translate returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array
                || document.RootElement.GetArrayLength() == 0
                || document.RootElement[0].ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Google Translate returned an unexpected response.");
            }

            var builder = new StringBuilder();
            foreach (var chunk in document.RootElement[0].EnumerateArray())
            {
                if (chunk.ValueKind != JsonValueKind.Array || chunk.GetArrayLength() == 0)
                {
                    continue;
                }

                var piece = chunk[0];
                if (piece.ValueKind == JsonValueKind.String)
                {
                    builder.Append(piece.GetString());
                }
            }

            var translated = builder.ToString();
            if (string.IsNullOrWhiteSpace(translated))
            {
                throw new InvalidOperationException("Google Translate returned no text.");
            }

            return translated;
        }
    }

    private sealed class LibreTranslateProvider : ITranslationProvider
    {
        private readonly HttpClient _http;
        private readonly string _url;
        private readonly string _apiKey;

        public LibreTranslateProvider(HttpClient http, string url, string apiKey)
        {
            _http = http;
            _url = url;
            _apiKey = apiKey;
        }

        public string DisplayName => "LibreTranslate";

        public async Task<string> TranslateAsync(
            string text,
            string source,
            string target,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_url))
            {
                throw new InvalidOperationException(
                    "Set the LibreTranslate server URL in Settings.");
            }

            var body = new Dictionary<string, string>
            {
                ["q"] = text,
                ["source"] = string.IsNullOrWhiteSpace(source) ? "auto" : source,
                ["target"] = target,
                ["format"] = "text",
            };

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                body["api_key"] = _apiKey;
            }

            var endpoint = _url.TrimEnd('/') + "/translate";
            using var content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            using var response = await _http.PostAsync(endpoint, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(DescribeError(json, "LibreTranslate", response));
            }

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("translatedText", out var translated)
                && translated.ValueKind == JsonValueKind.String)
            {
                return translated.GetString() ?? string.Empty;
            }

            throw new InvalidOperationException("LibreTranslate returned no text.");
        }
    }

    private sealed class DeepLProvider : ITranslationProvider
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public DeepLProvider(HttpClient http, string apiKey)
        {
            _http = http;
            _apiKey = apiKey;
        }

        public string DisplayName => "DeepL";

        public async Task<string> TranslateAsync(
            string text,
            string source,
            string target,
            CancellationToken cancellationToken)
        {
            var apiKey = _apiKey.Trim();
            if (apiKey.Length == 0)
            {
                throw new InvalidOperationException("Add a DeepL API key in Settings.");
            }

            // Free keys end with ":fx"; Pro keys do not.
            var endpoint = apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
                ? "https://api-free.deepl.com/v2/translate"
                : "https://api.deepl.com/v2/translate";

            var fields = new List<KeyValuePair<string, string>>
            {
                new("auth_key", apiKey),
                new("text", text),
                new("target_lang", ToDeepLTarget(target)),
            };

            if (!string.IsNullOrWhiteSpace(source)
                && !string.Equals(source, "auto", StringComparison.OrdinalIgnoreCase))
            {
                fields.Add(new KeyValuePair<string, string>("source_lang", source.ToUpperInvariant()));
            }

            using var content = new FormUrlEncodedContent(fields);
            using var response = await _http.PostAsync(endpoint, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(DescribeError(json, "DeepL", response));
            }

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("translations", out var translations)
                && translations.ValueKind == JsonValueKind.Array
                && translations.GetArrayLength() > 0
                && translations[0].TryGetProperty("text", out var translated)
                && translated.ValueKind == JsonValueKind.String)
            {
                return translated.GetString() ?? string.Empty;
            }

            throw new InvalidOperationException("DeepL returned no text.");
        }

        private static string ToDeepLTarget(string target) =>
            target.ToLowerInvariant() switch
            {
                "en" => "EN-US",
                "pt" => "PT-PT",
                _ => target.ToUpperInvariant(),
            };
    }

    private sealed class PythonTranslateProvider : ITranslationProvider
    {
        private readonly AppSettings _settings;
        private readonly string _engine;

        public PythonTranslateProvider(AppSettings settings, string engine, string displayName)
        {
            _settings = settings;
            _engine = engine;
            DisplayName = displayName;
        }

        public string DisplayName { get; }

        public Task<string> TranslateAsync(
            string text,
            string source,
            string target,
            CancellationToken cancellationToken) =>
            PythonBridge.TranslateAsync(_settings, _engine, text, source, target, cancellationToken);
    }
}
