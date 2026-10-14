using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Translator;

internal sealed record DictionaryEntry(string Headword, string Reading, string Meaning, int Priority);

internal sealed class DictionaryIndex
{
    private readonly Dictionary<string, List<DictionaryEntry>> _japanese = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<DictionaryEntry>> _english = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<DictionaryEntry>> _englishExact = new(StringComparer.Ordinal);

    internal static DictionaryIndex Load(Stream xml)
    {
        var index = new DictionaryIndex();
        using var reader = XmlReader.Create(xml, new XmlReaderSettings
        {
            // JMdict uses internal DTD entities for labels; never resolve external resources.
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
            MaxCharactersFromEntities = 64 * 1024 * 1024,
        });
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "entry")
            {
                continue;
            }

            using var subtree = reader.ReadSubtree();
            index.AddEntry(XElement.Load(subtree));
        }

        if (index._japanese.Count == 0)
        {
            throw new InvalidDataException("JMdict contains no usable Japanese-English entries.");
        }

        return index;
    }

    internal IReadOnlyList<DictionaryEntry> Lookup(TranslationRequest request)
    {
        var query = Normalize(request.Text);
        if (query.Length == 0)
        {
            return [];
        }

        IEnumerable<DictionaryEntry> matches;
        HashSet<DictionaryEntry> exact = [];
        if (request.Source == "ja")
        {
            matches = _japanese.TryGetValue(query, out var entries) ? entries : [];
        }
        else
        {
            var tokens = Tokens(query).Distinct().ToArray();
            if (tokens.Length == 0 || tokens.Any(token => !_english.ContainsKey(token)))
            {
                return [];
            }

            var lists = tokens.Select(token => _english[token]).OrderBy(list => list.Count).ToArray();
            matches = lists[0].Where(entry => lists.All(list => list.Contains(entry)));
            if (_englishExact.TryGetValue(string.Join(" ", Tokens(query)), out var exactEntries))
            {
                exact = exactEntries;
            }
        }

        return matches.Distinct()
            .OrderByDescending(entry => entry.Priority > 0)
            .ThenByDescending(entry => exact.Contains(entry))
            .ThenByDescending(entry => entry.Priority)
            .ThenBy(entry => request.Source == "en" ? entry.Meaning.Length : 0)
            .ThenBy(entry => entry.Headword, StringComparer.Ordinal)
            .Take(20)
            .ToArray();
    }

    private void AddEntry(XElement element)
    {
        var spellings = element.Elements("k_ele").ToArray();
        var readings = element.Elements("r_ele").ToArray();
        foreach (var sense in element.Elements("sense"))
        {
            var meanings = sense.Elements("gloss")
                .Where(gloss => (string?)gloss.Attribute(XNamespace.Xml + "lang") is null or "eng")
                .Select(gloss => gloss.Value.Trim())
                .Where(value => value.Length > 0)
                .ToArray();
            if (meanings.Length == 0)
            {
                continue;
            }

            var restrictedSpellings = sense.Elements("stagk").Select(value => value.Value).ToArray();
            var restrictedReadings = sense.Elements("stagr").Select(value => value.Value).ToArray();
            var applicableSpellings = spellings.Where(spelling =>
                restrictedSpellings.Length == 0 || restrictedSpellings.Contains((string?)spelling.Element("keb"))).ToArray();
            foreach (var reading in readings)
            {
                var kana = (string?)reading.Element("reb");
                if (string.IsNullOrWhiteSpace(kana) ||
                    (restrictedReadings.Length > 0 && !restrictedReadings.Contains(kana)))
                {
                    continue;
                }

                var readingRestrictions = reading.Elements("re_restr").Select(value => value.Value).ToArray();
                var heads = applicableSpellings.Where(spelling =>
                    reading.Element("re_nokanji") is null &&
                    (readingRestrictions.Length == 0 || readingRestrictions.Contains((string?)spelling.Element("keb")))).ToArray();
                if (heads.Length == 0 && restrictedSpellings.Length > 0)
                {
                    continue;
                }

                var headwords = heads.Length > 0
                    ? heads.Select(head => (string)head.Element("keb")!).ToArray()
                    : [kana];
                var priority = reading.Elements("re_pri").Concat(heads.SelectMany(head => head.Elements("ke_pri")))
                    .Select(value => value.Value).Distinct()
                    .Count(value => value is "ichi1" or "news1" or "spec1" or "gai1");
                var entry = new DictionaryEntry(string.Join(" / ", headwords), kana, string.Join("; ", meanings), priority);
                foreach (var key in headwords.Append(kana).Select(Normalize).Distinct())
                {
                    if (!_japanese.TryGetValue(key, out var entries))
                    {
                        _japanese[key] = entries = [];
                    }

                    entries.Add(entry);
                }

                foreach (var token in Tokens(entry.Meaning).Distinct())
                {
                    if (!_english.TryGetValue(token, out var entries))
                    {
                        _english[token] = entries = [];
                    }

                    entries.Add(entry);
                }

                foreach (var meaning in meanings)
                {
                    var key = GlossKey(meaning);
                    if (!_englishExact.TryGetValue(key, out var entries))
                    {
                        _englishExact[key] = entries = [];
                    }

                    entries.Add(entry);
                }
            }
        }
    }

    private static string Normalize(string text) => text.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();

    private static string GlossKey(string meaning)
    {
        var text = new StringBuilder();
        var depth = 0;
        foreach (var character in meaning)
        {
            if (character == '(')
            {
                depth++;
            }
            else if (character == ')' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0)
            {
                text.Append(character);
            }
        }

        return string.Join(" ", Tokens(text.ToString()));
    }

    private static IEnumerable<string> Tokens(string text) =>
        Regex.Matches(Normalize(text), @"[\p{L}\p{N}]+").Select(match => match.Value);
}
