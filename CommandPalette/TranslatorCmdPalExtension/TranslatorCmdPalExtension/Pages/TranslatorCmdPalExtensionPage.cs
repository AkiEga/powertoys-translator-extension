// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Translator;

namespace TranslatorCmdPalExtension;

internal sealed partial class TranslatorCmdPalExtensionPage : DynamicListPage
{
    private IListItem[] _items = [new ListItem(new NoOpCommand()) { Title = "日本語または英語を入力してください", Subtitle = "ja: / en: で翻訳先を指定できます" }];
    private int _queryVersion;

    public TranslatorCmdPalExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "JP ↔ EN Translator";
        Name = "Translate";
    }

    public override IListItem[] GetItems() => _items;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        var version = Interlocked.Increment(ref _queryVersion);
        var request = TranslationRequest.Parse(newSearch);
        if (request.Text.Length == 0)
        {
            IsLoading = false;
            _items = [new ListItem(new NoOpCommand()) { Title = "日本語または英語を入力してください", Subtitle = "ja: / en: で翻訳先を指定できます" }];
        }
        else if (request.Text.Length > 500)
        {
            IsLoading = false;
            _items = [new ListItem(new NoOpCommand()) { Title = "入力が長すぎます", Subtitle = "500 文字以内で入力してください" }];
        }
        else
        {
            IsLoading = true;
            _items = [new ListItem(new NoOpCommand()) { Title = "翻訳中…", Subtitle = $"{request.Source} → {request.Target}" }];
            _ = TranslateAsync(request, version);
        }

        RaiseItemsChanged();
    }

    private async Task TranslateAsync(TranslationRequest request, int version)
    {
        await Task.Delay(350);
        if (version != Volatile.Read(ref _queryVersion))
        {
            return;
        }

        IListItem[] items;
        try
        {
            var translation = await Task.Run(() => TranslationClient.Translate(request));
            items = [new ListItem(new CopyTextCommand(translation))
            {
                Title = translation,
                Subtitle = $"{request.Source} → {request.Target} | Enter でコピー",
            }];
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            System.Diagnostics.Trace.TraceError($"Translator request failed: {ex}");
            items = [new ListItem(new NoOpCommand()) { Title = "翻訳できませんでした", Subtitle = ex.Message }];
        }

        if (version == Volatile.Read(ref _queryVersion))
        {
            _items = items;
            IsLoading = false;
            RaiseItemsChanged();
        }
    }
}
