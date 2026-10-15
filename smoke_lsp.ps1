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
# drain stderr on a background task: the server traces every message and a full
# pipe buffer would otherwise deadlock the process
$stderrTask = $proc.StandardError.ReadToEndAsync()

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

# a json null / missing array is "zero elements", unlike @($null).Count
function Count-Of($x) {
    if ($null -eq $x) { return 0 }
    return @($x).Count
}

$script = @(
    'add <- function(a, b) {'
    '    a + b'
    '}'
    ''
    'print(add(1, 2))'
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

# --- how many global functions did the interpreter expose? ------------------
Send-Message @{ jsonrpc = '2.0'; id = 90; method = 'workspace/symbol'; params = @{ query = '' } }
$ws = Read-Message
$nWs = Count-Of $ws.result
Write-Host ('  [info] workspace/symbol (global functions) count=' + $nWs)
if ($nWs -gt 0) {
    Write-Host ('  [info] first names: ' + ((@($ws.result) | Select-Object -First 8 | ForEach-Object { $_.name }) -join ', '))
}

# --- hover over 'print' on line 4 (0-based), character 2 --------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 2
    method  = 'textDocument/hover'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 4; character = 2 } }
}
$hover = Read-Message
Check 'hover @ print' ($null -ne $hover.result -and -not [string]::IsNullOrEmpty($hover.result.contents.value)) ($hover | ConvertTo-Json -Depth 8 -Compress)
if ($null -ne $hover.result) {
    Write-Host ('  [info] hover markdown head: ' + ($hover.result.contents.value -split "`r?`n")[0])
}

# --- completion with an empty prefix (all items) ---------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 3
    method  = 'textDocument/completion'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 4; character = 6 } }
}
$compAll = Read-Message
$nAll = Count-Of $compAll.result
Check 'completion (empty prefix)' ($nAll -gt 0) ('count=' + $nAll)

# --- completion filtered by prefix 'pr' ------------------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 4
    method  = 'textDocument/completion'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 4; character = 7 } }
}
$compPre = Read-Message
Write-Host ('  [info] completion (prefix "pr") count=' + (Count-Of $compPre.result))

# --- documentSymbol --------------------------------------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 5
    method  = 'textDocument/documentSymbol'
    params  = @{ textDocument = @{ uri = $uri } }
}
$sym = Read-Message
$nSym = Count-Of $sym.result
$symNames = (@($sym.result) | ForEach-Object { $_.name }) -join ', '
Check 'documentSymbol' ($nSym -gt 0) ('count=' + $nSym)
Write-Host ('  [info] documentSymbol names: ' + $symNames)

# --- definition of 'add' used on line 4 ------------------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 6
    method  = 'textDocument/definition'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 4; character = 7 } }
}
$def = Read-Message
$nDef = Count-Of $def.result
Check 'definition @ add' ($nDef -gt 0) ($def | ConvertTo-Json -Depth 8 -Compress)
if ($nDef -gt 0) {
    Write-Host ('  [info] definition line=' + $def.result[0].range.start.line)
}

# --- signature help inside add( --------------------------------------------
Send-Message @{
    jsonrpc = '2.0'
    id      = 7
    method  = 'textDocument/signatureHelp'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 4; character = 10 } }
}
$sig = Read-Message
$nSig = Count-Of $sig.result.signatures
Check 'signatureHelp @ add(' ($nSig -gt 0) ($sig | ConvertTo-Json -Depth 8 -Compress)
if ($nSig -gt 0) {
    Write-Host ('  [info] signature: ' + $sig.result.signatures[0].label + '  activeParameter=' + $sig.result.activeParameter)
}

# --- unknown method must return -32601 ------------------------------------
Send-Message @{ jsonrpc = '2.0'; id = 8; method = 'textDocument/doesNotExist'; params = @{} }
$unknown = Read-Message
Check 'unknown method -> -32601' ($unknown.error.code -eq -32601) ($unknown | ConvertTo-Json -Depth 8 -Compress)

# --- didChange should replace the cached document -------------------------
$script2 = @('solo <- function() { 1 }') -join "`n"
Send-Message @{
    jsonrpc = '2.0'
    method  = 'textDocument/didChange'
    params  = @{ textDocument = @{ uri = $uri; version = 2 }; contentChanges = @(@{ text = $script2 }) }
}
Send-Message @{
    jsonrpc = '2.0'
    id      = 10
    method  = 'textDocument/definition'
    params  = @{ textDocument = @{ uri = $uri }; position = @{ line = 0; character = 1 } }
}
$def2 = Read-Message
$nDef2 = Count-Of $def2.result
Check 'didChange replaced the document (solo found)' ($nDef2 -gt 0) ($def2 | ConvertTo-Json -Depth 8 -Compress)

# --- shutdown / exit -------------------------------------------------------
Send-Message @{ jsonrpc = '2.0'; id = 9; method = 'shutdown'; params = $null }
$sd = Read-Message
Check 'shutdown replies (result null)' ($null -ne $sd -and $null -eq $sd.result) ($sd | ConvertTo-Json -Depth 8 -Compress)

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
Write-Host '--- server stderr ---'
$stderrText = $stderrTask.Result
if ([string]::IsNullOrEmpty($stderrText)) {
    Write-Host '(empty)'
}
else {
    ($stderrText -split "`r?`n") | Select-Object -First 40 | ForEach-Object { Write-Host $_ }
}

if (-not $proc.HasExited) { $proc.Kill() }
