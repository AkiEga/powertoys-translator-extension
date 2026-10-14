# Command Palette JP ↔ EN Dictionary

PowerToys **Command Palette** 向けのオフライン日英辞書です。コマンド名は互換性のため「JP ↔ EN Translator」のままです。日本語または英語の単語を入力すると、JMdict の見出し・読み・英語の意味を最大 20 件表示します。Enter で、日本語からの検索では英語の意味、英語からの検索では日本語の見出しをコピーします。

**機能は単語・辞書にある表現の検索のみです。文章翻訳・活用形の解析・ローマ字入力・未知語の推測には対応しません。** 日本語は見出し・読みの完全一致、英語は定義に含まれる単語を検索します。複数の英単語はすべてを含む定義を検索します。全角英数字・半角カナは正規化します。初回検索では辞書の読み込みに数秒かかる場合があります。

| 入力 | 翻訳先 |
| --- | --- |
| `cat` | 日本語 |
| `猫` / `ねこ` | 英語 |
| `ja:cat` | 日本語 |
| `en:猫` | 英語 |

**LLM・Copilot SDK・Node.js・アカウント・API キーは不要です。** 検索文字列を外部送信せず、辞書の取得後はオフラインで動作します。旧版のために導入した Node.js やアカウント認証は使用しませんが、他のアプリで使用する可能性があるため自動削除しません。

## ビルドと配置

### ソースからインストール（署名証明書の信頼登録なし）

この方法は、各ユーザーが自分の PC でビルドし、Developer Mode でビルド出力を登録する開発用インストールです。配布者の証明書 (`CN=AkiEga`) を信頼ストアに追加する必要はありません。署名済み MSIX の通常インストールとは異なります。

必要な準備:

- Windows 11 と PowerToys Command Palette。PowerToys 設定で Command Palette を有効にします。
- Windows 設定で **Developer Mode** を有効にします。設定変更時に管理者の承認が必要になる場合があります。
- Git、.NET 10 SDK。`git --version`、`dotnet --list-sdks` で確認できます。
- Visual Studio / Build Tools の Windows アプリ開発用ツールと Windows SDK。ビルド環境の詳細は [Microsoft の拡張開発ドキュメント](https://learn.microsoft.com/ja-jp/windows/powertoys/command-palette/extension-development) を参照してください。
- 初回のソース・依存関係・辞書ダウンロード時のインターネット接続。

以下は **Windows x64** 向けです。通常権限の PowerShell で実行してください。作業先は例として `$HOME\source` を使用します。シンボリックリンク・ジャンクション・ネットワーク共有を経由しないローカルの実体パスを選んでください。

```powershell
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path "$HOME\source" -Force | Out-Null
Set-Location "$HOME\source"
git clone https://github.com/AkiEga/powertoys-translator-extension.git
if ($LASTEXITCODE -ne 0) { throw 'Clone failed.' }
Set-Location .\powertoys-translator-extension
$repo = (Get-Location).Path
$project = Join-Path $repo 'CommandPalette\TranslatorCmdPalExtension\TranslatorCmdPalExtension'

& .\download-dictionary.ps1

dotnet msbuild (Join-Path $project 'TranslatorCmdPalExtension.csproj') `
    '-t:Restore,Build' `
    -p:Configuration=Debug `
    -p:Platform=x64 `
    -p:RuntimeIdentifier=win-x64 `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxBundle=Never
if ($LASTEXITCODE -ne 0) { throw 'Extension build failed.' }

$manifest = Join-Path $project 'bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifest)) { throw 'Built manifest not found.' }
Add-AppxPackage -Register $manifest
Get-AppxPackage -Name TranslatorCmdPalExtension |
    Select-Object Name, Status, IsDevelopmentMode, InstallLocation
```

登録結果の `Status` が `Ok`、`IsDevelopmentMode` が `True` であることを確認します。ビルド中に生成される未署名 MSIX は、この手順ではインストールしません。ARM64 では `Platform` を `ARM64`、`RuntimeIdentifier` と出力パスの `win-x64` を `win-arm64`、出力パスの `x64` を `ARM64` に変更します（ARM64 の実機動作は未確認）。

その後、次の手順で使用します。

1. Command Palette を開き、「Reload Command Palette extensions」を実行します。
2. 「JP ↔ EN Translator」を検索して開き、ページ内に単語を入力します。結果は Enter でコピーします。

**登録後もリポジトリとビルド出力を移動・削除しないでください。** Windows は `InstallLocation` のファイルを直接使用します。更新時は Command Palette を終了してからリポジトリ内で `git pull --ff-only`、`.\download-dictionary.ps1`、ビルド、登録を実行し、再起動してください。辞書も定期的に再取得・再ビルドしてください。使用中の拡張プロセスが残ると、ビルド時にファイルのロックエラーが発生します。

旧 SDK 版から同じ出力先へ更新する場合は、Command Palette を終了した状態で、ビルド前にプロジェクトの既存 `bin` / `obj` を削除してください。旧ワーカーと依存物をパッケージに残さないためです。

よくある問題:

- `0x800701C0`（信頼されていないマウントポイント）: シンボリックリンクなどを通らない実体パスから登録します。
- `0x800B0109`（未信頼の証明書）: MSIX ファイルをインストールしていないか確認してください。この手順ではビルド出力の `AppxManifest.xml` を `-Register` で登録します。
- コマンドが見つからない: Command Palette を再読み込みし、拡張設定で有効になっているか確認します。旧 PowerToys Run の画面とは別です。
- 辞書検索が失敗する: ページに表示されるエラーを確認します。辞書・ライセンス文書がない場合や破損している場合は `.\download-dictionary.ps1` を再実行し、再ビルド・再登録後、Command Palette を再起動します。

削除する場合は Command Palette を終了し、次のコマンドで登録を解除してからソースフォルダを削除できます。このコマンドは同じ名前の MSIX 版をインストールしている場合も、そのユーザーの登録を削除します。

```powershell
Get-AppxPackage -Name TranslatorCmdPalExtension | Remove-AppxPackage
```

### Visual Studio または署名済み MSIX を使用する場合

ソースは `CommandPalette\TranslatorCmdPalExtension` にあります。Microsoft 公式の Command Palette 拡張テンプレートから生成した Windows x64 / ARM64 の MSIX プロジェクトです。必要な環境は上記のソースビルド手順と同じです。

署名済み MSIX の通常インストールには、インストール先の Windows が署名証明書を信頼している必要があります。上記の開発用登録では証明書の信頼変更は不要です。

辞書版の x64 Release パッケージは `release\TranslatorCmdPalExtension_2.0.0.0_x64_unsigned.msix` です。これは未署名のビルド成果物であり、通常のインストーラーとしては使用できません。上記のソースビルド・開発用登録を使用するか、自分の証明書で署名してから、その証明書を信頼する環境に配置してください。旧 1.0.0.0 の SDK 版パッケージは現行ツリーから削除しました。

1. リポジトリ直下で `.\download-dictionary.ps1` を実行します。
2. `CommandPalette\TranslatorCmdPalExtension\TranslatorCmdPalExtension.sln` を Visual Studio で開きます。MSIX の署名証明書を作成する場合、証明書の主体は `Package.appxmanifest` の `Publisher` (`CN=AkiEga`) に合わせます。
3. `x64` / `Debug` を選び、**Build > Deploy** でパッケージを登録します。ビルドのみでは Command Palette から見えません。署名済み MSIX を `Add-AppxPackage -Path` で配置する場合は、署名証明書が Windows に信頼されている必要があります。
4. 開発用の代替手段として、Developer Mode を有効にしたうえで、ビルド出力の `AppxManifest.xml` を `Add-AppxPackage -Register` で登録できます。例: `Add-AppxPackage -Register 'C:\work\my-home\powertoys-translator-extension\CommandPalette\TranslatorCmdPalExtension\TranslatorCmdPalExtension\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AppxManifest.xml'`。これは MSIX ファイルのインストールではなく開発用の展開です。パスにシンボリックリンクが含まれると登録に失敗するため、出力の実体パスを指定してください。
5. Command Palette の「Reload Command Palette extensions」を実行し、「JP ↔ EN Translator」を開いて単語を入力します。

## 辞書の出典とライセンス

日本語・英語の辞書データは **JMdict**、著作権者は James William Breen および **Electronic Dictionary Research and Development Group (EDRDG)** です。辞書データは **CC BY-SA 4.0** で提供され、正確性・適合性に関する保証はありません。

- [JMdict プロジェクト](https://www.edrdg.org/jmdict/j_jmdict.html)
- [EDRDG 使用条件](https://www.edrdg.org/edrdg/licence.html)
- [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)

取得した辞書・説明・ライセンス文書と `Data\NOTICE.md` はビルドに同梱されます。検索結果にも出典を表示します。辞書を改変・再配布する場合はライセンスの表示・継承条件に従ってください。

## テスト

```powershell
dotnet run --project .\tests\TranslationRequest.Tests.csproj -c Release
```

実際の辞書全体も確認する場合:

```powershell
dotnet run --project .\tests\TranslationRequest.Tests.csproj -c Release -- `
    .\CommandPalette\TranslatorCmdPalExtension\TranslatorCmdPalExtension\Data\JMdict_e.gz
```

以前の PowerToys **Run** 用プラグインとコードは削除しました。翻訳動作の確認には Command Palette 拡張を使用してください。
