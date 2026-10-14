$ErrorActionPreference = 'Stop'
$data = Join-Path $PSScriptRoot 'CommandPalette\TranslatorCmdPalExtension\TranslatorCmdPalExtension\Data'
New-Item -ItemType Directory -Path $data -Force | Out-Null
$downloads = [ordered]@{
    'EDRDG-LICENCE.html' = 'https://www.edrdg.org/edrdg/licence.html'
    'JMdict-documentation.html' = 'https://www.edrdg.org/jmdict/j_jmdict.html'
    'CC-BY-SA-4.0.txt' = 'https://creativecommons.org/licenses/by-sa/4.0/legalcode.txt'
    'JMdict_e.gz' = 'https://www.edrdg.org/pub/Nihongo/JMdict_e.gz'
}
foreach ($name in $downloads.Keys) {
    $destination = Join-Path $data $name
    $temporary = "$destination.download"
    try {
        Invoke-WebRequest -Uri $downloads[$name] -OutFile $temporary
        if ((Get-Item -LiteralPath $temporary).Length -eq 0) {
            throw "Downloaded file is empty: $name"
        }
        if ($name -eq 'JMdict_e.gz') {
            $file = [IO.File]::OpenRead($temporary)
            try {
                if ($file.ReadByte() -ne 0x1f -or $file.ReadByte() -ne 0x8b) {
                    throw 'The dictionary download is not a gzip archive.'
                }
            }
            finally {
                $file.Dispose()
            }
        }
        Move-Item -LiteralPath $temporary -Destination $destination -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporary) {
            Remove-Item -LiteralPath $temporary -Force
        }
    }
}
Write-Output 'JMdict and licence documents downloaded. Rebuild and register the extension to use this version.'
