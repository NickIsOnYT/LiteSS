using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace LiteSS;

/// <summary>
/// Runs the one-shot Python helper: JSON in on stdin, one JSON object out on stdout.
/// Models are loaded per request, which keeps the bridge simple and stateless.
/// </summary>
internal static class PythonBridge
{
    private const string HelperResource = "LiteSS.python.litess_helper.py";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(3);

    private static readonly string[] DefaultCandidates = { "python", "python3", "py" };

    private static readonly JsonSerializerOptions ResponseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<string> RecognizeAsync(
        Bitmap image,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        image.Save(memory, ImageFormat.Png);

        var payload = new Dictionary<string, object?>
        {
            ["action"] = "ocr",
            ["engine"] = "tesseract",
            ["image_base64"] = Convert.ToBase64String(memory.ToArray()),
            ["language"] = settings.OcrLanguage,
        };

        return await RunAsync(settings, payload, cancellationToken);
    }

    public static async Task<string> TranslateAsync(
        AppSettings settings,
        string engine,
        string text,
        string source,
        string target,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["action"] = "translate",
            ["engine"] = engine,
            ["text"] = text,
            ["source"] = source,
            ["target"] = target,
            ["url"] = settings.LibreTranslateUrl,
            ["api_key"] = settings.LibreTranslateApiKey,
        };

        return await RunAsync(settings, payload, cancellationToken);
    }

    /// <summary>Path of the built-in helper, extracted from the exe on first use.</summary>
    public static string EnsureHelperScript()
    {
        var folder = Path.Combine(SettingsStore.SettingsFolder, "python");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, "litess_helper.py");
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(HelperResource)
            ?? throw new InvalidOperationException("The built-in Python helper is missing from this build.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = reader.ReadToEnd();

        if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
        {
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        return path;
    }

    private static async Task<string> RunAsync(
        AppSettings settings,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        var script = ResolveScript(settings);
        var request = JsonSerializer.Serialize(payload);

        var candidates = string.IsNullOrWhiteSpace(settings.PythonExecutable)
            ? DefaultCandidates
            : new[] { settings.PythonExecutable.Trim() };

        ProcessResult? lastResult = null;

        foreach (var candidate in candidates)
        {
            ProcessResult result;
            try
            {
                result = await RunProcessAsync(candidate, script, request, cancellationToken);
            }
            catch (Win32Exception)
            {
                // Not installed under this name; try the next candidate.
                continue;
            }

            lastResult = result;
            var response = TryReadResponse(result.Output);

            if (response is null)
            {
                // The interpreter usually printed its own message (for example the
                // Microsoft Store alias). Try the next candidate before giving up.
                continue;
            }

            if (!response.Ok)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(response.Error)
                        ? "The Python helper reported an error."
                        : response.Error);
            }

            return response.Text ?? string.Empty;
        }

        if (lastResult is null)
        {
            throw new InvalidOperationException(
                "Python was not found. Install Python 3 or set the Python executable in Settings.");
        }

        var detail = string.IsNullOrWhiteSpace(lastResult.Error)
            ? lastResult.Output.Trim()
            : lastResult.Error.Trim();

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(detail)
                ? $"The Python helper exited with code {lastResult.ExitCode} without returning a response."
                : $"The Python helper failed: {detail}");
    }

    private static string ResolveScript(AppSettings settings)
    {
        var custom = settings.PythonScriptPath?.Trim() ?? string.Empty;
        if (custom.Length > 0)
        {
            if (!File.Exists(custom))
            {
                throw new FileNotFoundException($"The configured Python script was not found: {custom}");
            }

            return custom;
        }

        return EnsureHelperScript();
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        string script,
        string request,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(script) ?? Environment.CurrentDirectory,
        };

        if (IsPyLauncher(executable))
        {
            startInfo.ArgumentList.Add("-3");
        }

        startInfo.ArgumentList.Add("-X");
        startInfo.ArgumentList.Add("utf8");
        startInfo.ArgumentList.Add(script);

        // Windows Python defaults to the console code page; force UTF-8 so non-ASCII
        // OCR text survives the round trip.
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PYTHONUTF8"] = "1";

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        try
        {
            await process.StandardInput.WriteAsync(request);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The interpreter exited before reading stdin (for example a Store alias).
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException(
                $"The Python helper did not finish within {RequestTimeout.TotalMinutes:0} minutes.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var output = await outputTask;
        var error = await errorTask;
        return new ProcessResult(process.ExitCode, output, error);
    }

    private static PythonResponse? TryReadResponse(string output)
    {
        var lines = output.Trim().Split('\n');

        // The helper prints exactly one object, but libraries occasionally add lines.
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line[0] != '{')
            {
                continue;
            }

            try
            {
                return JsonSerializer.Deserialize<PythonResponse>(line, ResponseOptions);
            }
            catch (JsonException)
            {
                // Not the response line; keep looking.
            }
        }

        return null;
    }

    private static bool IsPyLauncher(string executable)
    {
        var name = Path.GetFileNameWithoutExtension(executable);
        return string.Equals(name, "py", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Best effort while unwinding an error.
        }
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);

    private sealed class PythonResponse
    {
        public bool Ok { get; set; }
        public string? Text { get; set; }
        public string? Error { get; set; }
    }
}
