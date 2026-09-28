namespace Translator;

public sealed record TranslationRequest(string Text, string Source, string Target)
{
    public static TranslationRequest Parse(string search)
    {
        var text = search.Trim();
        string? target = null;

        if (text.StartsWith("ja:", StringComparison.OrdinalIgnoreCase))
        {
            target = "ja";
            text = text[3..].Trim();
        }
        else if (text.StartsWith("en:", StringComparison.OrdinalIgnoreCase))
        {
            target = "en";
            text = text[3..].Trim();
        }

        target ??= text.Any(IsJapanese) ? "en" : "ja";
        return new TranslationRequest(text, target == "ja" ? "en" : "ja", target);
    }

    private static bool IsJapanese(char ch) =>
        ch is >= '\u3040' and <= '\u30ff'
            or >= '\u3400' and <= '\u9fff'
            or >= '\uf900' and <= '\ufaff'
            or '\u3005' or '\u3006';
}
