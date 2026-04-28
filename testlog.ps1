# logloop.ps1

$log = "C:\temp\testlog.txt"

# Open a persistent write-only stream
$fs = [System.IO.File]::Open($log,
    [System.IO.FileMode]::Append,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::Read)   # allow readers, but no other writers

$sw = New-Object System.IO.StreamWriter($fs)
$sw.AutoFlush = $true

try {
    while ($true) {

        # Random delay 1–10 seconds
        Start-Sleep -Seconds (Get-Random -Minimum 1 -Maximum 11)

        # Timestamp + random text
        $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.ffff")

        # Random text length 5–200 characters
        $randomLength = Get-Random -Minimum 5 -Maximum 201
        $randomText   = -join ((65..90) + (97..122) | Get-Random -Count $randomLength | ForEach-Object {[char]$_})

        $line = "$timestamp  $randomText"

        # Write safely with zero locking issues
        $sw.WriteLine($line)

        Write-Host "Wrote: $line"
    }
}
finally {
    # Clean shutdown if script is stopped
    $sw.Close()
    $fs.Close()
}
