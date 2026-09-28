using Translator;

var cases = new (string Input, string Text, string Source, string Target)[]
{
    ("hello world", "hello world", "en", "ja"),
    ("おはよう", "おはよう", "ja", "en"),
    ("日本語です", "日本語です", "ja", "en"),
    ("ja:Good morning", "Good morning", "en", "ja"),
    ("EN: ありがとう", "ありがとう", "ja", "en"),
    ("  ", "", "en", "ja"),
};

foreach (var (input, text, source, target) in cases)
{
    var actual = TranslationRequest.Parse(input);
    if (actual != new TranslationRequest(text, source, target))
    {
        throw new Exception($"Unexpected translation direction for '{input}': {actual}");
    }
}

Console.WriteLine($"{cases.Length} translation direction cases passed.");
