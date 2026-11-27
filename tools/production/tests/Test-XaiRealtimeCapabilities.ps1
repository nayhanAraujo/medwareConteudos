param([string]$Model = 'grok-voice-latest', [int]$SilenceDurationMs = 400)
$ErrorActionPreference = 'Stop'
if (-not $env:XAI_API_KEY) { throw 'Set XAI_API_KEY in this process before running this opt-in live probe. Never pass a key on the command line.' }
$socket = New-Object System.Net.WebSockets.ClientWebSocket
$timeout = New-Object System.Threading.CancellationTokenSource
$timeout.CancelAfter(25000)
try {
    $socket.Options.SetRequestHeader('Authorization', ('Bearer ' + $env:XAI_API_KEY))
    $null = $socket.ConnectAsync([Uri]('wss://api.x.ai/v1/realtime?model=' + [Uri]::EscapeDataString($Model)), $timeout.Token).GetAwaiter().GetResult()
    $sent = $false
    while (-not $timeout.IsCancellationRequested) {
        $stream = New-Object IO.MemoryStream
        try {
            $buffer = New-Object byte[] 16384
            do {
                $segment = New-Object 'System.ArraySegment[byte]' -ArgumentList (,$buffer)
                $frame = $socket.ReceiveAsync($segment, $timeout.Token).GetAwaiter().GetResult()
                if ($frame.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close) { throw 'Provider closed the probe.' }
                $stream.Write($buffer, 0, $frame.Count)
                if ($stream.Length -gt 1048576) { throw 'Provider frame exceeds limit.' }
            } while (-not $frame.EndOfMessage)
            $event = ConvertFrom-Json ([Text.Encoding]::UTF8.GetString($stream.ToArray()))
        } finally { $stream.Dispose() }
        if (-not $sent -and $event.type -in @('session.created', 'conversation.created')) {
            $request = @{ type = 'session.update'; session = @{
                turn_detection = @{ type = 'server_vad'; silence_duration_ms = $SilenceDurationMs }
                audio = @{ input = @{ format = @{ type = 'audio/pcm'; rate = 24000 } } }
                tools = @()
            } } | ConvertTo-Json -Depth 10 -Compress
            $bytes = [Text.Encoding]::UTF8.GetBytes($request)
            $segment = New-Object 'System.ArraySegment[byte]' -ArgumentList (,$bytes)
            $null = $socket.SendAsync($segment, [Net.WebSockets.WebSocketMessageType]::Text, $true, $timeout.Token).GetAwaiter().GetResult()
            $sent = $true
        }
        if ($event.type -eq 'error') { throw ('Provider rejected capabilities. Code: ' + $event.error.code) }
        if ($event.type -eq 'session.updated') {
            if ($event.session.turn_detection.type -ne 'server_vad' -or $event.session.turn_detection.silence_duration_ms -ne $SilenceDurationMs) {
                throw 'FAIL: Provider did not acknowledge the requested VAD interval.'
            }
            Write-Output ('PASS: session and server_vad acknowledged at ' + $SilenceDurationMs + ' ms. No audio or business tools sent. Studio discards output audio; provider output audio may still be billed.')
            break
        }
    }
} finally { $socket.Abort(); $socket.Dispose(); $timeout.Dispose() }
