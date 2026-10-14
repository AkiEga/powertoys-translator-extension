// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Translator;

namespace TranslatorCmdPalExtension;

internal sealed partial class TranslatorCmdPalExtensionPage : DynamicListPage
{
    private const string Attribution = "JMdict © EDRDG | CC BY-SA 4.0";
    private IListItem[] _items = [new ListItem(new NoOpCommand()) { Title = "日本語または英語の単語を入力してください", Subtitle = Attribution }];
    private int _queryVersion;

    public TranslatorCmdPalExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "JP ↔ EN Translator";
        Name = "Look up";
    }

    public override IListItem[] GetItems() => _items;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        var version = Interlocked.Increment(ref _queryVersion);
        var request = TranslationRequest.Parse(newSearch);
        if (request.Text.Length == 0)
        {
            IsLoading = false;
            _items = [new ListItem(new NoOpCommand()) { Title = "日本語または英語の単語を入力してください", Subtitle = $"ja: / en: で検索方向を指定 | {Attribution}" }];
        }
        else if (request.Text.Length > 500)
        {
            IsLoading = false;
            _items = [new ListItem(new NoOpCommand()) { Title = "入力が長すぎます", Subtitle = $"500 文字以内で入力してください | {Attribution}" }];
        }
        else
        {
            IsLoading = true;
            _items = [new ListItem(new NoOpCommand()) { Title = "辞書を検索中…", Subtitle = Attribution }];
            _ = LookupAsync(request, version);
        }

        RaiseItemsChanged();
    }

    private async Task LookupAsync(TranslationRequest request, int version)
    {
        await Task.Delay(350);
        if (version != Volatile.Read(ref _queryVersion))
        {
            return;
        }

        IListItem[] items;
        try
        {
            var entries = await Task.Run(() => DictionaryClient.Lookup(request));
            items = entries.Select(entry =>
            {
                var result = request.Target == "en" ? entry.Meaning : entry.Headword;
                return (IListItem)new ListItem(new CopyTextCommand(result))
                {
                    Title = $"{entry.Headword}【{entry.Reading}】",
                    Subtitle = $"{entry.Meaning} | Enter でコピー | {Attribution}",
                };
            }).ToArray();
            if (items.Length == 0)
            {
                items = [new ListItem(new NoOpCommand()) { Title = "該当する単語がありません", Subtitle = $"文章翻訳・活用形の解析には対応していません | {Attribution}" }];
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException or InvalidOperationException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceError($"Dictionary lookup failed: {ex}");
            items = [new ListItem(new NoOpCommand()) { Title = "辞書を検索できませんでした", Subtitle = $"{ex.Message} | {Attribution}" }];
        }

        if (version == Volatile.Read(ref _queryVersion))
        {
            _items = items;
            IsLoading = false;
            RaiseItemsChanged();
        }
    }
}
