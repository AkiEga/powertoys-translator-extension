using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Translator;

internal static class TranslationClient
{
    private static readonly object Sync = new();
    private static Process? _worker;
    private static TranslationRequest? _cachedRequest;
    private static string? _cachedTranslation;

    public static string Translate(TranslationRequest request)
    {
        lock (Sync)
        {
            if (request == _cachedRequest && _cachedTranslation is not null)
            {
                return _cachedTranslation;
            }

            try
            {
                EnsureWorker();
                _worker!.StandardInput.WriteLine(JsonSerializer.Serialize(request, WorkerRequestJsonContext.Default.TranslationRequest));
                _worker.StandardInput.Flush();

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(55));
                var line = _worker.StandardOutput.ReadLineAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
                if (line is null)
                {
                    throw new InvalidOperationException("The Copilot SDK worker stopped unexpectedly.");
                }

                using var response = JsonDocument.Parse(line);
                if (response.RootElement.TryGetProperty("error", out var error))
                {
                    throw new InvalidOperationException(error.GetString() ?? "Copilot SDK request failed.");
                }

                if (!response.RootElement.TryGetProperty("translation", out var result)
                    || result.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(result.GetString()))
                {
                    throw new InvalidOperationException("Copilot returned no translation.");
                }

                var translation = result.GetString()!.Trim();
                _cachedRequest = request;
                _cachedTranslation = translation;
                return translation;
            }
            catch
            {
                StopWorker();
                throw;
            }
        }

    }

    public static void Dispose()
    {
        lock (Sync)
        {
            StopWorker();
        }
    }

    private static void EnsureWorker()
    {
        if (_worker is { HasExited: false })
        {
            return;
        }

        StopWorker();
        var workerPath = Path.Combine(AppContext.BaseDirectory, "worker", "worker.mjs");
        if (!File.Exists(workerPath))
        {
            throw new FileNotFoundException("Copilot SDK worker is not installed.", workerPath);
        }

        var start = new ProcessStartInfo
        {
            FileName = "node.exe",
            WorkingDirectory = Path.GetDirectoryName(workerPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add(workerPath);
        _worker = Process.Start(start) ?? throw new InvalidOperationException("Could not start the Copilot SDK worker.");
    }

    private static void StopWorker()
    {
        if (_worker is null)
        {
            return;
        }

        try
        {
            if (!_worker.HasExited)
            {
                _worker.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) when (_worker.HasExited)
        {
        }
        finally
        {
            _worker.Dispose();
            _worker = null;
        }
    }
}
