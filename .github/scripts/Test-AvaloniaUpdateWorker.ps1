param([Parameter(Mandatory=$true)][string]$ExecutablePath,[switch]$SimulateFailure)
$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetTempPath()) ('nxfv-update-worker-'+[guid]::NewGuid().ToString('N'))
$install=Join-Path $root 'installation with spaces'
$stage=Join-Path $install ('Updates/avalonia-'+[guid]::NewGuid().ToString('N'))
$exeName=[IO.Path]::GetFileName($ExecutablePath)
$worker=$null
try {
    New-Item -ItemType Directory -Path (Join-Path $stage 'payload'),(Join-Path $stage 'worker') -Force | Out-Null
    foreach($directory in @($install,(Join-Path $stage 'payload'),(Join-Path $stage 'worker'))) { Copy-Item -LiteralPath $ExecutablePath -Destination (Join-Path $directory $exeName) }
    # A GUI smoke test must never send production telemetry, polls or update checks.
    $settings=@{ CheckUpdatesOnStart=$false; IncludePreReleases=$true; SharedSettings=@{ UsageStatisticsChoiceConfirmed=$true;SendAnonymousUsageStatistics=$false;ReportEndpoint='';PollEndpoint='';CheckViewerUpdatesOnStartup=$false } } | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $install 'NxFileViewer.settings.json'),$settings)
    [IO.File]::WriteAllText((Join-Path $install 'prod.keys'),'fixture-preserve')
    $hash=(Get-FileHash -LiteralPath (Join-Path $install $exeName) -Algorithm SHA256).Hash
    if($SimulateFailure){
        $fixture=Join-Path $root 'crash-fixture'
        New-Item -ItemType Directory -Path $fixture -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $fixture 'CrashFixture.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>')
        [IO.File]::WriteAllText((Join-Path $fixture 'Program.cs'),'return 1;')
        Push-Location -LiteralPath (Join-Path $PSScriptRoot '../../src')
        try {
            dotnet publish (Join-Path $fixture 'CrashFixture.csproj') -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true --artifacts-path (Join-Path $fixture 'artifacts') -o (Join-Path $fixture 'published') --nologo -v quiet
            if($LASTEXITCODE -ne 0){throw 'Could not build crash fixture.'}
        }finally{Pop-Location}
        Copy-Item -LiteralPath (Join-Path $fixture 'published/CrashFixture.exe') -Destination (Join-Path $stage "payload/$exeName") -Force
    }
    $payloadHash=(Get-FileHash -LiteralPath (Join-Path $stage "payload/$exeName") -Algorithm SHA256).Hash
    $job=@{ParentId=[int]::MaxValue;ParentStarted=0;Executable=$exeName;PreviousExecutable=$exeName;PreviousSha256=$hash;Files=@(@{Name=$exeName;Sha256=$payloadHash})} | ConvertTo-Json -Depth 5
    $jobPath=Join-Path $stage 'job.json';[IO.File]::WriteAllText($jobPath,$job)
    $worker=Start-Process -FilePath (Join-Path $stage "worker/$exeName") -ArgumentList @('--apply-avalonia-update',('"'+$jobPath+'"')) -WindowStyle Hidden -PassThru
    if(!$worker.WaitForExit(150000)){throw 'Update worker did not finish.'}
    if(!$SimulateFailure -and $worker.ExitCode -ne 0){throw [IO.File]::ReadAllText((Join-Path $stage 'update-error.txt'))}
    if($SimulateFailure -and ($worker.ExitCode -ne 1 -or !(Test-Path -LiteralPath (Join-Path $stage 'update-error.txt')))){throw 'Expected a failed update with recovery.'}
    for($i=0;$i -lt 300 -and !(Test-Path -LiteralPath (Join-Path $stage 'ready'));$i++){Start-Sleep -Milliseconds 100}
    if((Get-FileHash -LiteralPath (Join-Path $install $exeName) -Algorithm SHA256).Hash -ne $hash){throw 'Expected intact installed executable.'}
    if(!(Test-Path -LiteralPath (Join-Path $stage 'ready'))){throw 'Updated main window did not open.'}
    if([IO.File]::ReadAllText((Join-Path $install 'prod.keys')) -ne 'fixture-preserve'){throw 'User keys changed.'}
    if(!(Test-Path -LiteralPath (Join-Path $stage "backup/$exeName"))){throw 'Previous executable backup missing.'}
    Write-Host ('Avalonia worker passed: real helper, GUI startup, backup, preserved keys, rollback test=' + [bool]$SimulateFailure)
}
finally {
    $processFile=Join-Path $stage 'started-process.json'
    if(Test-Path -LiteralPath $processFile){
        $started=Get-Content -LiteralPath $processFile -Raw | ConvertFrom-Json
        $owned=Get-Process -Id $started.Id -ErrorAction SilentlyContinue
        if($owned -and $owned.StartTime.ToUniversalTime().Ticks -eq $started.Started -and $owned.Path -eq (Join-Path $install $exeName)){
            $owned.CloseMainWindow() | Out-Null
            if(!$owned.WaitForExit(5000)){$owned.Kill();$owned.WaitForExit()}
        }
    }
    if($worker -and !$worker.HasExited){$worker.Kill();$worker.WaitForExit()}
    $resolved=[IO.Path]::GetFullPath($root)
    if([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -and [IO.Path]::GetFileName($resolved) -match '^nxfv-update-worker-[0-9a-f]{32}$'){
        if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
    }
}
