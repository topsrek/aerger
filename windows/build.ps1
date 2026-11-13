$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run --project tests/Aerger.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Regeltests fehlgeschlagen.' }
    dotnet publish src/Aerger/Aerger.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o release
    if ($LASTEXITCODE -ne 0) { throw 'Windows-Build fehlgeschlagen.' }
    # Nur Paketinhalt behalten: alte Symboldatei und ungenutzte WPF-Nativbibliotheken (*_cor3.dll) entfernen;
    # die WinForms-Anwendung läuft ohne sie. Testausgaben liegen getrennt in release/test-output.
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $PSScriptRoot 'release/Aerger.pdb')
    Remove-Item -Force (Join-Path $PSScriptRoot 'release/*_cor3.dll')
    $testOutput = Join-Path $PSScriptRoot 'release/test-output'
    New-Item -ItemType Directory -Force $testOutput | Out-Null
    $smokeLog = Join-Path $testOutput 'smoke-test-stderr.txt'
    $smoke = Start-Process -FilePath (Join-Path $PSScriptRoot 'release/Aerger.exe') -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru -Wait -RedirectStandardError $smokeLog
    if ($smoke.ExitCode -ne 0) {
        Get-Content $smokeLog
        throw 'Starttest des veröffentlichten Pakets fehlgeschlagen.'
    }
    Remove-Item -Recurse -Force $testOutput
    Write-Output 'OK: Veröffentlichtes Paket mit Originaldaten erfolgreich gestartet.'
} finally { Pop-Location }
