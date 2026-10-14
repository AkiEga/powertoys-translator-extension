using System.IO.Compression;

namespace Translator;

internal static class DictionaryClient
{
    private static readonly Lazy<DictionaryIndex> Index = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "JMdict_e.gz");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("JMdict is missing. Run download-dictionary.ps1, rebuild, and register the extension.", path);
        }

        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        return DictionaryIndex.Load(gzip);
    });

    internal static IReadOnlyList<DictionaryEntry> Lookup(TranslationRequest request) => Index.Value.Lookup(request);
}
