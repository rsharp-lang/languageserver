# temporary end-to-end smoke test for the R# language server (stdio transport).
# exercises the same request set the Monaco bridge will issue.
$ErrorActionPreference = 'Stop'

$dll = Join-Path $PSScriptRoot 'src\lsp\bin\Debug\net10.0\lsp.dll'

if (-not (Test-Path $dll)) { throw "lsp.dll not found: $dll" }

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = 'dotnet'
$psi.Arguments = '"' + $dll + '"'
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false

$proc = [System.Diagnostics.Process]::Start($psi)
$stdin = $proc.StandardInput
$out = $proc.StandardOutput.BaseStream

$stderr = New-Object System.Collections.ArrayList
$proc.add_ErrorDataReceived({ param($s, $e) if ($null -ne $e.Data) { [void]$stderr.Add($e.Data) } })
$proc.BeginErrorReadLine()

function Send-Message($msg) {
    $json = $msg | ConvertTo-Json -Depth 32 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $header = 'Content-Length: ' + $bytes.Length + "`r`n`r`n"
    $stdin.Write($header)
    $stdin.Write($json)
    $stdin.Flush()
}

function Read-Message {
    $headerText = ''
    $one = New-Object byte[] 1

    while ($true) {
        $n = $out.Read($one, 0, 1)
        if ($n -le 0) { return $null }
        $headerText += [char]$one[0]
        if ($headerText.EndsWith("`r`n`r`n")) { break }
    }

    $len = 0
    foreach ($line in ($headerText -split "`r`n")) {
        if ($line -match '^Content-Length:\s*(\d+)') { $len = [int]$Matches[1] }
    }
    if ($len -le 0) { return $null }

    $body = New-Object byte[] $len
    $read = 0
    while ($read -lt $len) {
        $n = $out.Read($body, $read, $len - $read)
        if ($n -le 0) { break }
        $read += $n
    }

    return ([System.Text.Encoding]::UTF8.GetString($body, 0, $read) | ConvertFrom-Json)
}

$script = @(
    'add <- function(a, b) {'
    '    return(a + b)'
    '}'
    ''
    'x = 1 + 2'
    'print(add(x, 3))'
) -join "`n"

$uri = 'file:///test.r'
$failures = New-Object System.Collections.ArrayList

function Check([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) {
        Write-Host ('  [PASS] ' + $name)
    }
    else {
        Write-Host ('  [FAIL] ' + $name + ' -- ' + $detail)
        [void]$failures.Add($name)
    }
}

Write-Host 'R# LSP stdio smoke test'
Write-Host ''

# --- initialize -------------------------------------------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 1
    method  = 'initialize'
    params  = @{ processId = $PID; rootUri = $null; capabilities = @{} }
}
$init = Read-Message

Check 'initialize: hoverProvider' ($init.result.capabilities.hoverProvider -eq $true) ($init | ConvertTo-Json -Depth 8 -Compress)
Check 'initialize: completionProvider' ($null -ne $init.result.capabilities.completionProvider) ''
Check 'initialize: signatureHelpProvider' ($null -ne $init.result.capabilities.signatureHelpProvider) ''
Check 'initialize: serverInfo' ($init.result.serverInfo.name -eq 'rsharp-lsp') ''

# --- didOpen (notification) -------------------------------------------------
Send-Message @{ jsonrpc = '2.0'; method = 'initialized'; params = @{} }
Send-Message @{
    jsonrpc = '2.0'
    method  = 'textDocument/didOpen'
    params  = @{ textDocument = @{ uri = $uri; languageId = 'r'; version = 1; text = $script } }
}

# --- hover over 'print' on line 5 (0-based), character 2 --------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 2
    method  = 'textDocument/hover'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 5; character = 2 } }
}
$hover = Read-Message
$hoverValue = $hover.result.contents.value
Check 'hover @ print' (-not [string]::IsNullOrEmpty($hoverValue)) ($hover | ConvertTo-Json -Depth 8 -Compress)

# --- completion with an empty prefix (all items) ---------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 3
    method  = 'textDocument/completion'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 5; character = 10 } }
}
$compAll = Read-Message
$nAll = @($compAll.result).Count
Check 'completion (empty prefix)' ($nAll -gt 0) ('count=' + $nAll)

# --- completion filtered by prefix 'ad' ------------------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 4
    method  = 'textDocument/completion'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 5; character = 8 } }
}
$compPre = Read-Message
Write-Host ('  [info] completion (prefix "ad") count=' + @($compPre.result).Count)

# --- documentSymbol --------------------------------------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 5
    method  = 'textDocument/documentSymbol'
    params  = @{ textDocument = @{ uri = $uri } }
}
$sym = Read-Message
$nSym = @($sym.result).Count
$symNames = (@($sym.result) | ForEach-Object { $_.name }) -join ', '
Check 'documentSymbol' ($nSym -gt 0) ('count=' + $nSym)
Write-Host ('  [info] documentSymbol names: ' + $symNames)

# --- definition of 'add' used on line 5 ------------------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 6
    method  = 'textDocument/definition'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 5; character = 7 } }
}
$def = Read-Message
$nDef = @($def.result).Count
Check 'definition @ add' ($nDef -gt 0) ($def | ConvertTo-Json -Depth 8 -Compress)

# --- signature help inside add( --------------------------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 7
    method  = 'textDocument/signatureHelp'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 5; character = 10 } }
}
$sig = Read-Message
$nSig = @($sig.result.signatures).Count
Check 'signatureHelp @ add(' ($nSig -gt 0) ($sig | ConvertTo-Json -Depth 8 -Compress)

# --- unknown method must return -32601 ------------------------------------
Send-Message @{ jsonrpc = '2.0'; id = 8; method = 'textDocument/doesNotExist'; params = @{} }
$unknown = Read-Message
Check 'unknown method -> -32601' ($unknown.error.code -eq -32601) ($unknown | ConvertTo-Json -Depth 8 -Compress)

# --- shutdown / exit -------------------------------------------------------
Send-Message @{ jsonrpc = '2.0'; id = 9; method = 'shutdown'; params = $null }
$sd = Read-Message
Check 'shutdown' ($null -ne $sd) ($sd | ConvertTo-Json -Depth 8 -Compress)

Send-Message @{ jsonrpc = '2.0'; method = 'exit' }
[void]$proc.WaitForExit(5000)

Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'ALL CHECKS PASSED'
}
else {
    Write-Host ('FAILURES: ' + ($failures -join ', '))
}

Write-Host ''
Write-Host '--- server stderr (last 15 lines) ---'
$stderr | Select-Object -Last 15 | ForEach-Object { Write-Host $_ }

if (-not $proc.HasExited) { $proc.Kill() }
