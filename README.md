# Command Palette JP ↔ EN Translator

PowerToys **Command Palette** 向けの日英翻訳拡張です。検索ページに文章を入力すると翻訳結果が表示され、Enter でコピーします。`ja:` / `en:` で翻訳先を指定でき、指定がないときは漢字・かなの有無で自動判定します。

| 入力 | 翻訳先 |
| --- | --- |
| `Good morning` | 日本語 |
| `おはようございます` | 英語 |
| `ja:Good morning` | 日本語 |
| `en:ありがとう` | 英語 |

翻訳には GitHub Copilot SDK の軽量モデル **GPT-6 Luna** (`gpt-6-luna`) を使用し、入力文は Copilot に送信されます。Copilot にログイン済みのアカウントでこのモデルを利用できる必要があり、利用枠が消費される場合があります。モデルが利用できない場合、高価なモデルへ自動的に切り替えずエラーを表示します。料金は [GitHub のモデル別料金表](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing) を参照してください。翻訳セッションではツールの実行を禁止しています。

## ビルドと配置

ソースは `CommandPalette\TranslatorCmdPalExtension` にあります。Microsoft 公式の Command Palette 拡張テンプレートから生成した Windows x64 / ARM64 の MSIX プロジェクトです。**Windows 11、Developer Mode、PowerToys Command Palette、Visual Studio の Windows App SDK ワークロードと .NET 10 SDK、および Node.js** が必要です。

署名済み x64 Release ビルドは `release\TranslatorCmdPalExtension_1.0.0.0_x64.msix` にあります。自己署名証明書 (`CN=AkiEga`) による署名なので、通常の `Add-AppxPackage -Path` インストールには、インストール先の Windows でこの署名証明書を信頼する必要があります。現時点で実機には開発用の loose-manifest 登録のみを行っており、通常の MSIX インストールおよび Copilot を利用した翻訳の動作確認は完了していません。

1. `CommandPalette\TranslatorCmdPalExtension\TranslatorCmdPalExtension\worker` で `npm ci` を実行します。
2. `CommandPalette\TranslatorCmdPalExtension\TranslatorCmdPalExtension.sln` を Visual Studio で開きます。MSIX の署名証明書を作成する場合、証明書の主体は `Package.appxmanifest` の `Publisher` (`CN=AkiEga`) に合わせます。
3. `x64` / `Debug` を選び、**Build > Deploy** でパッケージを登録します。ビルドのみでは Command Palette から見えません。署名済み MSIX を `Add-AppxPackage -Path` で配置する場合は、署名証明書が Windows に信頼されている必要があります。
4. 開発用の代替手段として、Developer Mode を有効にしたうえで、ビルド出力の `AppxManifest.xml` を `Add-AppxPackage -Register` で登録できます。例: `Add-AppxPackage -Register 'C:\work\my-home\powertoys-translator-extension\CommandPalette\TranslatorCmdPalExtension\TranslatorCmdPalExtension\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AppxManifest.xml'`。これは MSIX ファイルのインストールではなく開発用の展開です。パスにシンボリックリンクが含まれると登録に失敗するため、出力の実体パスを指定してください。
5. Command Palette の「Reload Command Palette extensions」を実行し、「JP ↔ EN Translator」を開いて文章を入力します。

ビルド・配置手順は [Microsoft の開発ドキュメント](https://learn.microsoft.com/ja-jp/windows/powertoys/command-palette/extension-development) に準拠します。Copilot SDK は同梱の Node.js ワーカーで動作します。Copilot の認証・通信が失敗した場合はページにエラーを表示します。`node.exe` が PowerToys 起動時の `PATH` から見える必要があります。

以前の PowerToys **Run** 用プラグインとコードは削除しました。翻訳動作の確認には Command Palette 拡張を使用してください。
