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

var fixture = """
    <?xml version="1.0"?>
    <!DOCTYPE JMdict [<!ENTITY noun "noun">]>
    <JMdict>
      <entry><k_ele><keb>猫</keb><ke_pri>ichi1</ke_pri></k_ele><r_ele><reb>ねこ</reb></r_ele>
        <sense><pos>&noun;</pos><gloss>cat</gloss><gloss xml:lang="ger">Katze</gloss></sense></entry>
      <entry><r_ele><reb>アイスクリーム</reb><re_nokanji/></r_ele>
        <sense><gloss>ice cream</gloss></sense></entry>
      <entry><k_ele><keb>犬</keb></k_ele><r_ele><reb>いぬ</reb></r_ele>
        <sense><gloss xml:lang="eng">dog</gloss></sense></entry>
      <entry><k_ele><keb>生</keb></k_ele><k_ele><keb>性</keb></k_ele>
        <r_ele><reb>なま</reb><re_restr>生</re_restr></r_ele>
        <r_ele><reb>せい</reb><re_restr>性</re_restr></r_ele>
        <sense><stagk>生</stagk><stagr>なま</stagr><gloss>raw</gloss></sense>
        <sense><stagk>性</stagk><stagr>せい</stagr><gloss>nature</gloss></sense></entry>
    </JMdict>
    """;
using var xml = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fixture));
var index = DictionaryIndex.Load(xml);
var lookups = new (string Query, string Expected)[]
{
    ("猫", "cat"),
    ("ねこ", "cat"),
    (" CAT ", "cat"),
    ("ｃａｔ", "cat"),
    ("ice cream", "ice cream"),
    ("ｱｲｽｸﾘｰﾑ", "ice cream"),
    ("dog", "dog"),
    ("生", "raw"),
    ("せい", "nature"),
    ("en:猫", "cat"),
    ("ja:cat", "cat"),
};
foreach (var (query, expected) in lookups)
{
    var results = index.Lookup(TranslationRequest.Parse(query));
    if (results.Count != 1 || results[0].Meaning != expected)
    {
        throw new Exception($"Unexpected dictionary lookup for '{query}'.");
    }
}

foreach (var query in new[] { "", "!!!", "catastrophe", "Katze", "unknownword", "猫が走っています", "ice dog" })
{
    if (index.Lookup(TranslationRequest.Parse(query)).Count != 0)
    {
        throw new Exception($"Expected no match for '{query}'.");
    }
}

if (index.Lookup(TranslationRequest.Parse("nature"))[0].Headword != "性")
{
    throw new Exception("Reading/sense restrictions were not respected.");
}

foreach (var badXml in new[] { "<JMdict />", "<JMdict><entry>" })
{
    try
    {
        using var invalid = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(badXml));
        DictionaryIndex.Load(invalid);
        throw new Exception("Invalid dictionary was accepted.");
    }
    catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
    {
    }
}

var many = "<JMdict>" + string.Concat(Enumerable.Range(0, 30).Select(i =>
    $"<entry><r_ele><reb>word{i}</reb></r_ele><sense><gloss>shared</gloss></sense></entry>")) + "</JMdict>";
using var manyXml = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(many));
if (DictionaryIndex.Load(manyXml).Lookup(TranslationRequest.Parse("shared")).Count != 20)
{
    throw new Exception("Dictionary results must be capped at 20.");
}

Console.WriteLine("Dictionary fixture lookups, no-match cases, restrictions, invalid data, and result limit passed.");

var rankedXml = """
    <JMdict>
      <entry><r_ele><reb>にゃあん</reb></r_ele><sense><gloss>cat</gloss></sense></entry>
      <entry><k_ele><keb>猫</keb><ke_pri>ichi1</ke_pri></k_ele><r_ele><reb>ねこ</reb></r_ele>
        <sense><gloss>cat (domestic (small) animal)</gloss></sense><sense><gloss>geisha</gloss></sense></entry>
      <entry><k_ele><keb>子猫</keb><ke_pri>ichi1</ke_pri></k_ele><r_ele><reb>こねこ</reb></r_ele>
        <sense><gloss>small cat</gloss></sense></entry>
    </JMdict>
    """;
using var rankedStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rankedXml));
var ranked = DictionaryIndex.Load(rankedStream);
if (ranked.Lookup(TranslationRequest.Parse("猫"))[0].Meaning != "cat (domestic (small) animal)" ||
    ranked.Lookup(TranslationRequest.Parse("cat"))[0].Headword != "猫")
{
    throw new Exception("Common entries and primary senses must be ranked first.");
}

Console.WriteLine("Common-entry and primary-sense ranking passed.");

if (args.Length == 1)
{
    var timer = System.Diagnostics.Stopwatch.StartNew();
    using var file = File.OpenRead(args[0]);
    using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress);
    var real = DictionaryIndex.Load(gzip);
    foreach (var query in new[] { "猫", "ねこ", "cat", "dog", "ice cream" })
    {
        var results = real.Lookup(TranslationRequest.Parse(query));
        if (results.Count == 0 || results.Count > 20)
        {
            throw new Exception($"Real JMdict lookup failed: {query}");
        }

        Console.WriteLine($"Real JMdict: {query} -> {results[0].Headword} / {results[0].Meaning}");
        var expectedHeadword = query switch { "cat" => "猫", "dog" => "犬", _ => null };
        if (expectedHeadword is not null && !results[0].Headword.Split(" / ").Contains(expectedHeadword))
        {
            throw new Exception($"Common exact definition must rank first: {query}");
        }
    }

    Console.WriteLine($"Real dictionary loaded and queried in {timer.Elapsed.TotalSeconds:F2}s.");
}
