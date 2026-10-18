$ErrorActionPreference = 'Stop'

# Exercise the same Node -> .NET redirected stream boundary without building the API.
$sourcePath = Join-Path $PSScriptRoot '../../../backend/ConversorHtml.Application/Services/VoiceBridgeRunner.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
foreach ($stream in @('StandardOutputEncoding', 'StandardErrorEncoding')) {
    if ($source -notmatch "$stream\s*=\s*Encoding\.UTF8") {
        throw "VoiceBridgeRunner must explicitly set $stream to UTF-8."
    }
}

# ASCII source keeps this script compatible with Windows PowerShell 5.1 without BOM.
$sample = ConvertFrom-Json '"C\u00c2MARAS ESQUERDAS | \u00c1trio esquerdo | Di\u00e2metro diast\u00f3lico do VE | Di\u00e2metro sist\u00f3lico do VE | se\u00e7\u00e3o | fra\u00e7\u00e3o | m\u00b2 | cm\u00b2 | cm\u00b3 | \u00b0 | \u00b5 | \u2264 | \u2265"'
$payloadPath = Join-Path ([IO.Path]::GetTempPath()) ('voice-encoding-test-' + [Guid]::NewGuid().ToString('N') + '.json')
$utf8 = New-Object System.Text.UTF8Encoding($false)
try {
    [IO.File]::WriteAllText($payloadPath, (ConvertTo-Json @{ text = $sample } -Compress), $utf8)
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'node'
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [Text.Encoding]::UTF8
    $startInfo.StandardErrorEncoding = [Text.Encoding]::UTF8
    $startInfo.EnvironmentVariables['VOICE_ENCODING_TEST_PAYLOAD'] = $payloadPath
    $startInfo.Arguments = '-e "const fs=require(''node:fs'');const p=JSON.parse(fs.readFileSync(process.env.VOICE_ENCODING_TEST_PAYLOAD,''utf8''));process.stdout.write(JSON.stringify({camposScriptJson:JSON.stringify({camposScript:[{descricao:p.text}]}),content:p.text}));process.stderr.write(p.text);"'
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) { throw 'Node did not start.' }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(10000)) {
            $process.Kill()
            throw 'Node encoding test timed out.'
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Node failed: $stderr" }
        $response = ConvertFrom-Json $stdout
        $model = ConvertFrom-Json $response.camposScriptJson
        if ($model.camposScript[0].descricao -cne $sample) { throw 'Model JSON lost Unicode characters.' }
        if ($response.content -cne $sample) { throw 'TXT content lost Unicode characters.' }
        if ($stderr -cne $sample) { throw 'Diagnostic output lost Unicode characters.' }
        Write-Output 'PASS: UTF-8 payload, nested model JSON, TXT content and stderr preserved.'
    }
    finally { $process.Dispose() }
}
finally {
    if (Test-Path -LiteralPath $payloadPath) { Remove-Item -LiteralPath $payloadPath }
}
