# Checks the server's functions (the sending ones and, since chunk 34, the spread) against the Supabase project (see
# docs/systems/server.md), then deletes what it sent.
# Run it after any change to supabase/migrations. Every check prints pass or FAIL; the exit code is 1 if any failed.
#   powershell -ExecutionPolicy Bypass -File tools\check-server.ps1 -Url https://<ref>.supabase.co -Key sb_publishable_...
# -Keep stops before the deletion and prints the player ID, so the rows can be read in the SQL editor; then
# -Delete <player ID> deletes them and checks the deleted ID is refused.
param(
    [Parameter(Mandatory = $true)][string]$Url,
    [Parameter(Mandatory = $true)][string]$Key,
    [switch]$Keep,
    [string]$Delete
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$script:failed = $false

function Send([string]$Path, [string]$Body, [string]$Token) {
    $headers = @{ apikey = $Key }
    if ($Token) {
        $headers.Authorization = "Bearer $Token"
    }
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$Url/$Path" -Headers $headers `
            -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($Body))
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Json = ($response.Content | ConvertFrom-Json) }
    }
    catch [System.Net.WebException] {
        $status = 0
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        return [pscustomobject]@{ Status = $status; Json = $null }
    }
}

function Check([string]$Name, [bool]$Ok, [string]$Got) {
    if ($Ok) {
        Write-Host "pass: $Name"
    }
    else {
        Write-Host "FAIL: $Name ($Got)"
        $script:failed = $true
    }
}

function SignIn {
    $reply = Send 'auth/v1/signup' '{}'
    Check 'anonymous sign-in' ($reply.Status -eq 200 -and $reply.Json.access_token) "status $($reply.Status)"
    return $reply.Json.access_token
}

function Record([string]$Player) {
    return '{"version":2,"id":"' + [guid]::NewGuid() + '","player":"' + $Player + '","consent":1,"started":"2026-11-19T12:00:00Z"}'
}

function DeleteAndRefuse([string]$Player, [string]$Token) {
    $reply = Send 'rest/v1/rpc/delete_player' ('{"player":"' + $Player + '"}')
    Check 'delete_player without a session deletes the player' ($reply.Status -eq 200 -and $reply.Json.deleted -eq $true) "status $($reply.Status), deleted $($reply.Json.deleted)"
    if (-not $Token) {
        $Token = SignIn
    }
    $reply = Send 'rest/v1/rpc/upload_records' ('{"player":"' + $Player + '","records":[' + (Record $Player) + ']}') $Token
    Check 'a record under the deleted ID is refused' ($reply.Status -eq 200 -and $reply.Json.playerDeleted -eq $true -and $reply.Json.stored -eq 0) "status $($reply.Status)"
}

if ($Delete) {
    DeleteAndRefuse $Delete $null
    exit ([int]$script:failed)
}

$row = [regex]::Match((Get-Content (Join-Path $PSScriptRoot '..\supabase\levels.sql') -Raw), "\((\d+), '([0-9a-f]{16})', (\d+)\)")
$level = [int]$row.Groups[1].Value
$layout = $row.Groups[2].Value
$par = [int]$row.Groups[3].Value
$player = [guid]::NewGuid().ToString()
$token = SignIn

$scores = '{"player":"' + $player + '","scores":[' +
    '{"level":' + $level + ',"layout":"' + $layout + '","moves":' + ($par + 2) + '},' +
    '{"level":999999,"layout":"' + $layout + '","moves":5},' +
    '{"level":' + $level + ',"layout":"' + $layout + '","moves":' + ($par - 1) + '}]}'
$reply = Send 'rest/v1/rpc/submit_scores' $scores $token
Check 'submit_scores takes a valid score and lists the unknown and below-par ones' (
    $reply.Status -eq 200 -and $reply.Json.accepted -eq 1 -and ($reply.Json.unknown -join ',') -eq '999999' -and ($reply.Json.belowPar -join ',') -eq "$level"
) "status $($reply.Status), $($reply.Json | ConvertTo-Json -Compress)"

$reply = Send 'rest/v1/rpc/submit_scores' ('{"player":"' + $player + '","scores":[{"level":' + $level + ',"layout":"' + $layout + '","moves":' + ($par + 5) + '}]}') $token
Check 'a worse score is accepted (the lowest is kept)' ($reply.Status -eq 200 -and $reply.Json.accepted -eq 1) "status $($reply.Status)"

$spread = '{"level":' + $level + ',"layout":"' + $layout + '"}'
$reply = Send 'rest/v1/rpc/best_moves_spread' $spread $token
$keys = ''
$kept = 0
if ($reply.Json) {
    $keys = ($reply.Json.PSObject.Properties.Name | Sort-Object) -join ','
    $kept = @($reply.Json.counts | Where-Object { $_ -and $_[0] -eq ($par + 2) -and $_[1] -ge 1 }).Count
}
Check 'best_moves_spread counts the kept score and holds nothing but counts' ($reply.Status -eq 200 -and $keys -eq 'counts,more' -and $kept -eq 1) "status $($reply.Status), keys $keys"

$reply = Send 'rest/v1/rpc/best_moves_spread' ('{"level":' + $level + ',"layout":"0000000000000000"}') $token
Check 'an unknown layout has an empty spread' ($reply.Status -eq 200 -and @($reply.Json.counts).Count -eq 0 -and $reply.Json.more -eq 0) "status $($reply.Status)"

$reply = Send 'rest/v1/rpc/best_moves_spread' $spread
Check 'best_moves_spread without a session is refused' ($reply.Status -ge 400 -and $reply.Status -lt 500) "status $($reply.Status)"

$first = Record $player
$records = '{"player":"' + $player + '","records":[' + $first + ',' + (Record $player) + ',' + (Record ([guid]::NewGuid().ToString())) + ']}'
$reply = Send 'rest/v1/rpc/upload_records' $records $token
Check 'upload_records stores two and skips one under another player' (
    $reply.Status -eq 200 -and $reply.Json.stored -eq 2 -and $reply.Json.skipped -eq 1
) "status $($reply.Status), $($reply.Json | ConvertTo-Json -Compress)"

$reply = Send 'rest/v1/rpc/upload_records' ('{"player":"' + $player + '","records":[' + $first + ']}') $token
Check 'a record sent twice is stored once' ($reply.Status -eq 200 -and $reply.Json.duplicates -eq 1 -and $reply.Json.stored -eq 0) "status $($reply.Status)"

$reply = Send 'rest/v1/rpc/submit_scores' $scores
Check 'submit_scores without a session is refused' ($reply.Status -ge 400 -and $reply.Status -lt 500) "status $($reply.Status)"

Write-Host "player: $player"
if ($Keep) {
    Write-Host "Kept. Read the rows, then run again with -Delete $player"
}
else {
    DeleteAndRefuse $player $token
}
exit ([int]$script:failed)
