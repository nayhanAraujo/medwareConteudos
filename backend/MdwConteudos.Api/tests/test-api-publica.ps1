param(
    [string]$BasePublic = "http://127.0.0.1:5080/api/v1",
    [string]$BasePartner = "http://127.0.0.1:5080/apiconteudos/v1",
    [string]$Senha = ""
)

$ErrorActionPreference = "Stop"
$passed = 0
$failed = 0
$skipped = 0

function Write-Result($name, $ok, $detail = "") {
    if ($ok) {
        Write-Host "[OK] $name" -ForegroundColor Green
        if ($detail) { Write-Host "     $detail" }
        $script:passed++
    } else {
        Write-Host "[FAIL] $name" -ForegroundColor Red
        if ($detail) { Write-Host "     $detail" }
        $script:failed++
    }
}

function Invoke-Api {
    param(
        [string]$Method = "GET",
        [string]$Url,
        [hashtable]$Headers = @{},
        [string]$Body = $null
    )
    $params = @{ Method = $Method; Uri = $Url; Headers = $Headers; UseBasicParsing = $true }
    if ($Body) { $params.Body = $Body; $params.ContentType = "application/json" }
    return Invoke-WebRequest @params
}

if (-not $Senha) {
    $envPath = Join-Path (Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent) ".env"
    if (Test-Path $envPath) {
        Get-Content $envPath | ForEach-Object {
            if ($_ -match '^API_JWT_PASSWORD=(.+)$') { $Senha = $matches[1].Trim('"') }
        }
    }
}

Write-Host "=== Testes API Publica (.NET) ===" -ForegroundColor Cyan
Write-Host "Public: $BasePublic"
Write-Host "Partner: $BasePartner"

try {
    $r = Invoke-Api -Url "$BasePublic/health"
    $j = $r.Content | ConvertFrom-Json
    Write-Result "GET /health" ($r.StatusCode -eq 200 -and $j.success -eq $true) "status=$($j.status)"
} catch { Write-Result "GET /health" $false $_.Exception.Message }

try {
    $r = Invoke-Api -Url "$BasePublic/variaveis"
    $j = $r.Content | ConvertFrom-Json
    Write-Result "GET /variaveis" ($r.StatusCode -eq 200 -and $j.success -eq $true) "total=$($j.total)"
    $codVar = $j.data[0].codvariavel
} catch { Write-Result "GET /variaveis" $false $_.Exception.Message; $codVar = 1 }

try {
    $r = Invoke-Api -Url "$BasePublic/variaveis/$codVar"
    $j = $r.Content | ConvertFrom-Json
    Write-Result "GET /variaveis/{id}" ($r.StatusCode -eq 200 -and $j.success -eq $true) "cod=$codVar"
} catch { Write-Result "GET /variaveis/{id}" $false $_.Exception.Message }

foreach ($ep in @("normalidades", "formulas", "referencias", "especialidades", "sistema/info")) {
    try {
        $r = Invoke-Api -Url "$BasePublic/$ep"
        $j = $r.Content | ConvertFrom-Json
        Write-Result "GET /$ep" ($r.StatusCode -eq 200 -and ($j.success -eq $true -or $ep -eq "formulas")) ""
    } catch { Write-Result "GET /$ep" $false $_.Exception.Message }
}

try {
    $r = Invoke-Api -Url "$BasePublic/normalidades_ecodopplercardiograma?referencia=1"
    Write-Result "GET /normalidades_ecodopplercardiograma" ($r.StatusCode -eq 200) ""
} catch { Write-Result "GET /normalidades_ecodopplercardiograma" $false $_.Exception.Message }

try {
    $r = Invoke-Api -Url "$BasePublic/relatorios?ativo=1"
    $j = $r.Content | ConvertFrom-Json
    Write-Result "GET /relatorios" ($r.StatusCode -eq 200 -and $j.success -eq $true) "total=$($j.total)"
} catch { Write-Result "GET /relatorios" $false $_.Exception.Message }

try {
    $r = Invoke-Api -Url "$BasePublic/scripts?sistema=Laudos%20UX&ativo=1&incluir_arquivos=1"
    $j = $r.Content | ConvertFrom-Json
    Write-Result "GET /scripts" ($r.StatusCode -eq 200 -and $j.success -eq $true) "total=$($j.total)"
    $codScript = ($j.data | Where-Object { $_.imagens -and $_.imagens.Count -gt 0 } | Select-Object -First 1).codscriptlaudo
    if (-not $codScript) { $codScript = $j.data[0].codscriptlaudo }
} catch { Write-Result "GET /scripts" $false $_.Exception.Message; $codScript = 17 }

try {
    $r = Invoke-Api -Url "$BasePublic/scripts/ultimo-verificado?sistema=Laudos%20UX&ativo=1"
    Write-Result "GET /scripts/ultimo-verificado" ($r.StatusCode -eq 200 -or $r.StatusCode -eq 204) "status=$($r.StatusCode)"
} catch { Write-Result "GET /scripts/ultimo-verificado" $false $_.Exception.Message }

try {
    $candidates = @($j.data | Where-Object { $_.imagens -and $_.imagens.Count -gt 0 })
    $imgOk = $false
    foreach ($s in $candidates) {
        try {
            $r = Invoke-Api -Url "$BasePublic/scripts/$($s.codscriptlaudo)/imagem?indice=0"
            if ($r.StatusCode -eq 200) {
                Write-Result "GET /scripts/{id}/imagem" $true "cod=$($s.codscriptlaudo) content-type=$($r.Headers['Content-Type'])"
                $imgOk = $true
                break
            }
        } catch { }
    }
    if (-not $imgOk) { Write-Result "GET /scripts/{id}/imagem" $false "nenhum script com arquivo de imagem no disco" }
} catch { Write-Result "GET /scripts/{id}/imagem" $false $_.Exception.Message }

try {
    $r = Invoke-Api -Url "$BasePublic/scripts/$codScript/download"
    Write-Result "GET /scripts/{id}/download" ($r.StatusCode -eq 200) "bytes=$($r.RawContentLength)"
} catch { Write-Result "GET /scripts/{id}/download" $false $_.Exception.Message }

if ($Senha) {
    try {
        $body = "{`"senha`":`"$Senha`"}"
        $r = Invoke-Api -Method POST -Url "$BasePublic/token" -Body $body
        $j = $r.Content | ConvertFrom-Json
        $token = $j.token
        Write-Result "POST /token" ($r.StatusCode -eq 200 -and $j.success -eq $true) ""
    } catch { Write-Result "POST /token" $false $_.Exception.Message; $token = $null }

    if ($token) {
        $auth = @{ Authorization = "Bearer $token" }
        try {
            $r = Invoke-Api -Url "$BasePartner/paineis?ativo=1" -Headers $auth
            $j = $r.Content | ConvertFrom-Json
            Write-Result "GET /apiconteudos/paineis" ($r.StatusCode -eq 200 -and $j.success -eq $true) "total=$($j.total)"
        } catch { Write-Result "GET /apiconteudos/paineis" $false $_.Exception.Message }

        try {
            $r = Invoke-Api -Url "$BasePartner/paineis" -Headers $auth
            $j = $r.Content | ConvertFrom-Json
            if ($j.data.Count -gt 0 -and $j.data[0].tem_arquivo_pbix) {
                $codPainel = $j.data[0].codpainel
                $r2 = Invoke-Api -Url "$BasePartner/paineis/$codPainel/download" -Headers $auth
                Write-Result "GET /apiconteudos/paineis/{id}/download" ($r2.StatusCode -eq 200) "codpainel=$codPainel"
            } else {
                Write-Host "[SKIP] download painel (sem PBIX no banco)" -ForegroundColor Yellow
                $script:skipped++
            }
        } catch { Write-Result "GET /apiconteudos/paineis/{id}/download" $false $_.Exception.Message }
    }
} else {
    Write-Host "[SKIP] token/paineis (API_JWT_PASSWORD nao configurada)" -ForegroundColor Yellow
    $skipped += 3
}

Write-Host ""
Write-Host "Resumo: $passed OK | $failed FAIL | $skipped SKIP" -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
