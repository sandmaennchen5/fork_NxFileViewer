param([Parameter(Mandatory=$true)][string]$PlanPath, [switch]$NoRestart, [ValidateRange(1,30)][int]$LockTimeoutSeconds = 30)
$ErrorActionPreference = 'Stop'
$plan = Get-Content -LiteralPath $PlanPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($plan.SchemaVersion -eq 2) {
    $stage = [IO.Path]::GetFullPath((Split-Path -Parent $PlanPath))
    $root = Split-Path -Parent (Split-Path -Parent $stage)
    $ready = Join-Path $stage 'ready'
    $failure = Join-Path $stage 'error.txt'
    $journalPath = Join-Path $stage 'journal.json'
    $oldProcess = $null
    $updateLock = $null
    $journal = [pscustomobject]@{ SchemaVersion=1; ApplicationId='NxFileViewerFork'; PreviousExecutable=''; Executable=''; Entries=@(); CreatedDirectories=@(); RemovedDirectories=@() }
    function Assert-NoLinks([string]$path) {
        for ($cursor=[IO.Path]::GetFullPath($path); $cursor; $cursor=[IO.Path]::GetDirectoryName($cursor)) {
            if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Update path contains a link or junction.' }
        }
    }
    function Relative-Path([string]$path) {
        $value=$path.Replace('\','/')
        if (!$value -or $value.Length -gt 1024) { throw 'Invalid relative update path.' }
        $parts=$value.Split('/')
        if ($parts.Count -gt 32) { throw 'Update path is too deep.' }
        foreach ($part in $parts) {
            if (!$part -or $part -eq '.' -or $part -eq '..' -or $part.EndsWith('.') -or $part.EndsWith(' ') -or
                $part -match '[<>:"|?*\x00-\x1f]' -or $part.Split('.')[0] -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$') { throw 'Unsafe update path.' }
        }
        if ($parts[0] -in @('Updates','.git','.codex') -or $value -match '(?i)\.keys$|\.settings\.json$') { throw 'Update targets a protected data path.' }
        return $value
    }
    function Within([string]$base, [string]$relative) {
        $result=[IO.Path]::GetFullPath((Join-Path $base (Relative-Path $relative)))
        if (!$result.StartsWith($base.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Update path escapes its root.' }
        Assert-NoLinks $result
        return $result
    }
    function Hash([string]$path) {
        $sha=[Security.Cryptography.SHA256]::Create(); $stream=[IO.File]::OpenRead($path)
        try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
        finally { $stream.Dispose(); $sha.Dispose() }
    }
    function Save-Journal {
        $temporary=$journalPath+'.tmp'
        [IO.File]::WriteAllText($temporary,($journal | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        if ([IO.File]::Exists($journalPath)) { [IO.File]::Replace($temporary,$journalPath,[NullString]::Value) } else { [IO.File]::Move($temporary,$journalPath) }
    }
    function Ensure-Directory([string]$path) {
        Assert-NoLinks $path
        $missing=@(); $cursor=$path
        while (![IO.Directory]::Exists($cursor)) {
            if ([IO.File]::Exists($cursor)) { throw 'A file blocks an update directory.' }
            $missing=@($cursor)+$missing; $cursor=[IO.Path]::GetDirectoryName($cursor)
        }
        foreach ($item in $missing) {
            $relative=$item.Substring($root.TrimEnd('\').Length+1).Replace('\','/')
            [void](Relative-Path $relative)
            $journal.CreatedDirectories+=@($relative); Save-Journal
            [IO.Directory]::CreateDirectory($item) | Out-Null
        }
    }
    function Retry-FileOperation([scriptblock]$operation) {
        $deadline=[DateTime]::UtcNow.AddSeconds($LockTimeoutSeconds)
        while ($true) {
            try { & $operation; return }
            catch [IO.IOException] { if ([DateTime]::UtcNow -ge $deadline) { throw }; Start-Sleep -Milliseconds 100 }
        }
    }
    function Restore-Changes {
        $recoveryErrors=[Collections.Generic.List[string]]::new()
        foreach ($relative in @($journal.RemovedDirectories | Sort-Object Length)) {
            try { [IO.Directory]::CreateDirectory((Within $root $relative)) | Out-Null }
            catch { $recoveryErrors.Add($_.Exception.Message) }
        }
        $entries=@($journal.Entries); [array]::Reverse($entries)
        foreach ($entry in $entries) {
            try {
                $destination=Within $root $entry.Path
                $saved=Within (Join-Path $stage 'backup') $entry.Path
                if ($entry.Existed -and [IO.File]::Exists($saved)) {
                    if ((Hash $saved) -ne $entry.BeforeHash) { throw 'Recovery backup hash mismatch.' }
                    if ([IO.File]::Exists($destination) -and (Hash $destination) -eq $entry.BeforeHash) { continue }
                    $temporary=$destination+'.recovery-'+[Guid]::NewGuid().ToString('N')
                    try {
                        [IO.File]::Copy($saved,$temporary,$false)
                        Retry-FileOperation { if ([IO.File]::Exists($destination)) { [IO.File]::Replace($temporary,$destination,[NullString]::Value) } else { [IO.File]::Move($temporary,$destination) } }
                    } finally { if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) } }
                } elseif (!$entry.Existed -and [IO.File]::Exists($destination) -and (Hash $destination) -eq $entry.AfterHash) {
                    Retry-FileOperation { [IO.File]::Delete($destination) }
                }
            } catch { $recoveryErrors.Add($entry.Path+': '+$_.Exception.Message) }
        }
        foreach ($relative in @($journal.CreatedDirectories | Sort-Object Length -Descending)) {
            try {
                $directory=Within $root $relative
                if ([IO.Directory]::Exists($directory) -and [IO.Directory]::GetFileSystemEntries($directory).Count -eq 0) { [IO.Directory]::Delete($directory) }
            } catch { $recoveryErrors.Add($_.Exception.Message) }
        }
        if ($recoveryErrors.Count -gt 0) { throw ($recoveryErrors -join "`r`n") }
    }
    try {
        if ((Split-Path -Leaf (Split-Path -Parent $stage)) -ne 'Updates' -or (Split-Path -Leaf $stage) -notmatch '^[0-9a-fA-F]{32}$' -or
            $plan.ApplicationId -cne 'NxFileViewerFork' -or [IO.Path]::GetFullPath($plan.TargetRoot).TrimEnd('\') -ine $root.TrimEnd('\')) { throw 'Unexpected update root or application identity.' }
        Assert-NoLinks $stage
        $target=[IO.Path]::GetFullPath($plan.Target)
        $previousTarget=[IO.Path]::GetFullPath($plan.PreviousTarget)
        foreach ($executable in @($target,$previousTarget)) {
            if ((Split-Path -Parent $executable).TrimEnd('\') -ine $root.TrimEnd('\') -or
                [IO.Path]::GetExtension($executable) -ine '.exe' -or (Split-Path -Leaf $executable) -in @('dotnet.exe','NxFileViewerFork.Open.exe')) { throw 'Unexpected application executable.' }
            Assert-NoLinks $executable
        }
        $journal.PreviousExecutable=Split-Path -Leaf $previousTarget
        $journal.Executable=Split-Path -Leaf $target
        $files=@($plan.Files)
        if ($files.Count -eq 0 -or $files.Count -gt 10000) { throw 'Invalid update file list.' }
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $hasMain=$false
        foreach ($file in $files) {
            $relative=Relative-Path $file.Path
            if (!$seen.Add($relative) -or $file.Sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Duplicate path or invalid file hash.' }
            $destination=Within $root $relative
            if ($file.ExpectedAbsent -and ([IO.File]::Exists($destination) -or [IO.Directory]::Exists($destination))) { throw 'Migration target appeared after preparation; retaining it.' }
            if ($file.ExpectedPresent -and (![IO.File]::Exists($destination) -or (Hash $destination) -ne $file.Sha256)) { throw 'Migration source changed after preparation; retaining it.' }
            if (!$file.Remove) {
                $source=Within (Join-Path $stage 'payload') $relative
                if ((Hash $source) -ne $file.Sha256) { throw 'Prepared update hash mismatch.' }
                if ($destination -ieq $target) { $hasMain=$true }
            }
        }
        if (!$hasMain) { throw 'Main executable is not present in update file list.' }
        foreach ($relative in @($plan.Directories)+@($plan.RemoveDirectories)) { if ($relative) { [void](Within $root $relative) } }
        if ($plan.OriginalExecutableHash -and (Hash $previousTarget) -ne $plan.OriginalExecutableHash) { throw 'Original application changed before installation.' }
        $updateLock=[IO.File]::Open((Join-Path (Split-Path -Parent $stage) 'install.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        try { $oldProcess=[Diagnostics.Process]::GetProcessById($plan.ProcessId) } catch [ArgumentException] { }
        if ($oldProcess -and $plan.ProcessStartTicks -and $oldProcess.StartTime.ToUniversalTime().Ticks -ne $plan.ProcessStartTicks) { throw 'Application process identity changed.' }
        [IO.File]::WriteAllText($ready,'ready')
        if ($oldProcess -and !$oldProcess.WaitForExit(120000)) { throw 'Application did not exit; update cancelled.' }
        if ($plan.OriginalExecutableHash -and (Hash $previousTarget) -ne $plan.OriginalExecutableHash) { throw 'Original application changed while waiting.' }
        if ($previousTarget -ine $target -and !$seen.Contains((Split-Path -Leaf $previousTarget))) {
            $files+=@([pscustomobject]@{ Path=(Split-Path -Leaf $previousTarget); Sha256=(Hash $previousTarget); Remove=$true })
        }
        Save-Journal
        foreach ($relative in @($plan.Directories)) { if ($relative) { Ensure-Directory (Within $root $relative) } }
        foreach ($file in $files) {
            $relative=Relative-Path $file.Path
            $destination=Within $root $relative
            if ($file.ExpectedAbsent -and ([IO.File]::Exists($destination) -or [IO.Directory]::Exists($destination))) { throw 'Migration target appeared after preparation; retaining it.' }
            if ($file.ExpectedPresent -and (![IO.File]::Exists($destination) -or (Hash $destination) -ne $file.Sha256)) { throw 'Migration source changed after preparation; retaining it.' }
            $source=Within (Join-Path $stage 'payload') $relative
            if ($file.Remove) {
                if (![IO.File]::Exists($destination)) { continue }
                if ((Hash $destination) -ne $file.Sha256) { throw 'A file scheduled for removal was changed; retaining it.' }
            } elseif ((Hash $source) -ne $file.Sha256) { throw 'Prepared update changed while waiting.' }
            $existed=[IO.File]::Exists($destination)
            if ([IO.Directory]::Exists($destination)) { throw 'Directory blocks an update file.' }
            $saved=Within (Join-Path $stage 'backup') $relative
            $beforeHash=$null
            if ($existed) {
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
                Retry-FileOperation { [IO.File]::Copy($destination,$saved,$false) }
                $beforeHash=Hash $saved
            }
            $entry=[pscustomobject]@{ Path=$relative; Existed=$existed; BeforeHash=$beforeHash; AfterHash=$(if ($file.Remove) { $null } else { $file.Sha256 }) }
            $journal.Entries+=@($entry); Save-Journal
            if ($file.Remove) { Retry-FileOperation { [IO.File]::Delete($destination) }; continue }
            Ensure-Directory ([IO.Path]::GetDirectoryName($destination))
            Retry-FileOperation { if ($existed) { [IO.File]::Replace($source,$destination,[NullString]::Value) } else { [IO.File]::Move($source,$destination) } }
        }
        foreach ($relative in @($plan.RemoveDirectories | Sort-Object Length -Descending)) {
            if (!$relative) { continue }; $directory=Within $root $relative
            if ([IO.Directory]::Exists($directory) -and [IO.Directory]::GetFileSystemEntries($directory).Count -eq 0) {
                $journal.RemovedDirectories+=@($relative); Save-Journal
                [IO.Directory]::Delete($directory)
            }
        }
        if (!$NoRestart) {
            $newProcess=Start-Process -FilePath $target -WorkingDirectory $root -WindowStyle Hidden -PassThru
            Start-Sleep -Seconds 2
            if ($newProcess.HasExited) { throw 'Updated application exited immediately; restoring the previous package.' }
        }
        [IO.File]::WriteAllText((Join-Path $stage 'completed'),'installed')
    } catch {
        $message=$_.Exception.Message
        try { Restore-Changes } catch { $message+="`r`nRollback: $($_.Exception.Message)" }
        [IO.File]::WriteAllText($failure,$message)
        if (!$NoRestart -and (!$oldProcess -or $oldProcess.HasExited) -and $previousTarget -and [IO.File]::Exists($previousTarget)) {
            try { Start-Process -FilePath $previousTarget -WorkingDirectory $root -WindowStyle Hidden } catch { }
        }
        if ($NoRestart) { exit 1 }
        Add-Type -AssemblyName PresentationFramework
        [System.Windows.MessageBox]::Show($message,'NxFileViewer Fork Update') | Out-Null
        exit 1
    } finally { if ($updateLock) { $updateLock.Dispose() } }
    exit 0
}
$stage = [IO.Path]::GetFullPath((Split-Path -Parent $PlanPath))
$target = [IO.Path]::GetFullPath($plan.Target)
$newExe = Join-Path $stage 'new.exe'
$backup = Join-Path $stage 'previous.exe'
$ready = Join-Path $stage 'ready'
$failure = Join-Path $stage 'error.txt'
$installed = $false
function Get-ExecutableHash([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
}

try {
    $expectedUpdates = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $target) 'Updates'))
    if ((Split-Path -Parent $stage) -ne $expectedUpdates -or (Split-Path -Leaf $target) -ne 'NxFileViewer.exe') { throw 'Unexpected update paths.' }
    $expectedHash = $plan.ExecutableHash
    if ((Get-ExecutableHash $newExe) -ne $expectedHash) { throw 'Prepared update hash mismatch.' }
    $oldProcess = $null
    try { $oldProcess = [Diagnostics.Process]::GetProcessById($plan.ProcessId) } catch [ArgumentException] { }
    Set-Content -LiteralPath $ready -Value 'ready'
    if ($oldProcess -and !$oldProcess.WaitForExit(120000)) { throw 'NxFileViewer did not exit; update cancelled.' }
    if ((Get-ExecutableHash $newExe) -ne $expectedHash) { throw 'Prepared update changed while waiting.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ($true) {
        try {
            # Atomic replacement retains the previous EXE as a recovery backup.
            [IO.File]::Replace($newExe, $target, $backup)
            $installed = $true
            break
        } catch [IO.IOException] {
            if ([DateTime]::UtcNow -ge $deadline) { throw }
            Start-Sleep -Milliseconds 250
        }
    }
    Set-Content -LiteralPath (Join-Path $stage 'completed') -Value 'installed'
    if (!$NoRestart) {
        $newProcess = Start-Process -FilePath $target -WorkingDirectory (Split-Path -Parent $target) -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 2
        if ($newProcess.HasExited) { throw 'Updated application exited immediately; restoring previous version.' }
    }
} catch {
    $message = $_.Exception.Message
    if ($installed -and (Test-Path -LiteralPath $backup)) {
        try { [IO.File]::Replace($backup, $target, (Join-Path $stage 'failed.exe')) } catch { $message += "`r`nRollback: $($_.Exception.Message)" }
    }
    Set-Content -LiteralPath $failure -Value $message
    if (!$NoRestart -and (!$oldProcess -or $oldProcess.HasExited)) {
        try { Start-Process -FilePath $target -WorkingDirectory (Split-Path -Parent $target) -WindowStyle Hidden } catch { }
    }
    if ($NoRestart) { exit 1 }
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show($message, 'NxFileViewer Fork Update') | Out-Null
    exit 1
}
