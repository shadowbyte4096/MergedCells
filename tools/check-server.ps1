# Checks the server's functions (the sending ones, since chunk 34 the spread, since chunk 34b the level library, and after it
# the report email's Edge Function from outside) against the Supabase project (see docs/systems/server.md), then deletes
# what it sent. The report email's own checks are supabase/tests/library_report_emails.sql.
# Run it after any change to supabase/migrations. Every check prints pass or FAIL; the exit code is 1 if any failed.
#   powershell -ExecutionPolicy Bypass -File tools\check-server.ps1 -Url https://<ref>.supabase.co -Key sb_publishable_...
# -Keep stops before the deletion and prints the player IDs, so the rows can be read in the SQL editor; the library's
# author then also keeps one real level published (the share code tests' pinned level), for a game to find and get. Then
# -Delete <player ID> deletes each and checks the deleted ID is refused.
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
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Json = ($response.Content | ConvertFrom-Json); Content = $response.Content }
    }
    catch [System.Net.WebException] {
        $status = 0
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        return [pscustomobject]@{ Status = $status; Json = $null; Content = "" }
    }
}

# The report email's Edge Function, as an email link reaches it: no apikey, no sign-in
function ReportFunction([string]$Method, [string]$Query, [hashtable]$Headers) {
    $arguments = @{ UseBasicParsing = $true; Method = $Method; Uri = "$Url/functions/v1/library-reports$Query"; Headers = $Headers }
    if ($Method -eq 'Post') {
        $arguments.ContentType = 'application/json'
        $arguments.Body = '{}'
    }
    try {
        $response = Invoke-WebRequest @arguments
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Content = [string]$response.Content }
    }
    catch [System.Net.WebException] {
        $status = 0
        $content = ''
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            $content = (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
        }
        return [pscustomobject]@{ Status = $status; Content = $content }
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

# The level library. The server never reads inside a code, so the checks send the share code tests' pinned code under
# random IDs and layouts; only the kept level (-Keep) is that code's own level, which a game can decode and get
$pinnedCode = 'MC1-D44sGkttTB6aPy17XoyaEOfV8NcGDkxvbmcgV2F5IFJvdW5kAgMAAGgI_Ua87w'

function RandomLayout {
    return -join ((1..16) | ForEach-Object { '{0:x}' -f (Get-Random -Maximum 16) })
}

function Publish([string]$Player, [string]$Level, [string]$Layout, [string]$Token, [string]$Code = $pinnedCode, [string]$Name = 'Check level',
    [int]$Rows = 2, [int]$Columns = 3, [int]$Par = 2) {
    $body = '{"player":"' + $Player + '","level":"' + $Level + '","code":"' + $Code + '","name":"' + $Name + '","layout":"' + $Layout +
        '","rows":' + $Rows + ',"columns":' + $Columns + ',"par":' + $Par + ',"score":30}'
    return Send 'rest/v1/rpc/library_publish' $body $Token
}

function Levels([string]$Player, [string]$View, [string]$Token) {
    return Send 'rest/v1/rpc/library_levels' ('{"player":"' + $Player + '","view":"' + $View +
        '","min_score":null,"max_score":null,"min_side":null,"max_side":null,"page":0,"size":50}') $Token
}

function LevelCall([string]$Function, [string]$Player, [string]$Level, [string]$Token, [string]$Extra = '') {
    return Send "rest/v1/rpc/$Function" ('{"player":"' + $Player + '","level":"' + $Level + '"' + $Extra + '}') $Token
}

function Listed($Reply, [string]$Level) {
    if (-not $Reply.Json) {
        return $null
    }
    return @($Reply.Json.levels | Where-Object { $_.id -eq $Level })[0]
}

$author = [guid]::NewGuid().ToString()
$authorToken = SignIn
$readers = @(1..3 | ForEach-Object { [pscustomobject]@{ Player = [guid]::NewGuid().ToString(); Token = SignIn } })
$b = $readers[0]
$level = [guid]::NewGuid().ToString()
$levelLayout = RandomLayout

$reply = Publish $author $level $levelLayout $authorToken
Check 'library_publish publishes a level' ($reply.Status -eq 200 -and $reply.Json.outcome -eq 'published') "status $($reply.Status), $($reply.Json | ConvertTo-Json -Compress)"

$reply = Publish $b.Player $level (RandomLayout) $b.Token
Check "another player's ID is taken" ($reply.Status -eq 200 -and $reply.Json.outcome -eq 'taken') "status $($reply.Status), $($reply.Json.outcome)"

$reply = Publish $b.Player ([guid]::NewGuid().ToString()) $levelLayout $b.Token
Check "another player's layout under a new ID is a copy" ($reply.Status -eq 200 -and $reply.Json.outcome -eq 'copy') "status $($reply.Status), $($reply.Json.outcome)"

$reply = Publish $b.Player ([guid]::NewGuid().ToString()) $layout $b.Token
Check "a shipped level's layout is a campaign level" ($reply.Status -eq 200 -and $reply.Json.outcome -eq 'campaign') "status $($reply.Status), $($reply.Json.outcome)"

$reply = Publish $author ([guid]::NewGuid().ToString()) (RandomLayout) $authorToken 'not a code'
Check 'a code out of shape is refused' ($reply.Status -ge 400 -and $reply.Status -lt 500) "status $($reply.Status)"

$reply = Publish $author ([guid]::NewGuid().ToString()) (RandomLayout) $null
Check 'library_publish without a session is refused' ($reply.Status -ge 400 -and $reply.Status -lt 500) "status $($reply.Status)"

$reply = Levels $b.Player 'newest' $b.Token
$row = Listed $reply $level
Check 'Newest lists the level, not as the reader''s own, and never with a player ID' (
    $row -and $row.mine -eq $false -and $row.finished -eq 0 -and $row.code -eq $pinnedCode -and -not $reply.Content.Contains($author)
) "status $($reply.Status)"

$row = Listed (Levels $author 'mine' $authorToken) $level
Check "the author's own list shows it as theirs" ($row -and $row.mine -eq $true -and $row.hidden -eq $false) 'not listed'

$reply = LevelCall 'library_like' $b.Player $level $b.Token ',"liked":true'
Check 'a like before a finish changes nothing' ($reply.Status -eq 200 -and $reply.Json.likes -eq 0 -and $reply.Json.liked -eq $false) "status $($reply.Status)"

$reply = LevelCall 'library_finish' $author $level $authorToken
Check "the author's own finish doesn't count" ($reply.Status -eq 200 -and $reply.Json.mine -eq $true -and $reply.Json.finished -eq 0) "status $($reply.Status)"

LevelCall 'library_finish' $b.Player $level $b.Token | Out-Null
$reply = LevelCall 'library_finish' $b.Player $level $b.Token
Check 'a finish counts once per player' ($reply.Status -eq 200 -and $reply.Json.listed -eq $true -and $reply.Json.finished -eq 1) "status $($reply.Status), finished $($reply.Json.finished)"

$reply = LevelCall 'library_like' $b.Player $level $b.Token ',"liked":true'
Check 'a like after a finish counts' ($reply.Status -eq 200 -and $reply.Json.likes -eq 1 -and $reply.Json.liked -eq $true) "status $($reply.Status)"

$row = Listed (Levels $b.Player 'liked' $b.Token) $level
Check 'Most liked shows the like' ($row -and $row.likes -eq 1) 'not listed'

$reply = LevelCall 'library_like' $b.Player $level $b.Token ',"liked":false'
Check 'an unlike takes it back' ($reply.Status -eq 200 -and $reply.Json.likes -eq 0 -and $reply.Json.liked -eq $false) "status $($reply.Status)"

$reply = LevelCall 'library_finish' $b.Player ([guid]::NewGuid().ToString()) $b.Token
Check 'a level the library lacks is not listed' ($reply.Status -eq 200 -and $reply.Json.listed -eq $false) "status $($reply.Status)"

$reply = LevelCall 'library_report' $author $level $authorToken ',"reason":"name"'
Check "a report on the caller's own level is refused" ($reply.Status -eq 200 -and $reply.Json.reported -eq $false) "status $($reply.Status)"

$second = [guid]::NewGuid().ToString()
Publish $author $second (RandomLayout) $authorToken | Out-Null
$reply = LevelCall 'library_block' $b.Player $second $b.Token
Check 'a block counts the blocked player' ($reply.Status -eq 200 -and $reply.Json.blocked -eq 1) "status $($reply.Status)"
$hidden = -not (Listed (Levels $b.Player 'newest' $b.Token) $second)
$shown = [bool](Listed (Levels $readers[1].Player 'newest' $readers[1].Token) $second)
Check "a block hides the author's levels from the blocker only" ($hidden -and $shown) "hidden from the blocker $hidden, shown to another $shown"
$reply = Send 'rest/v1/rpc/library_unblock' ('{"player":"' + $b.Player + '"}') $b.Token
Check 'unblock brings them back' ($reply.Status -eq 200 -and $reply.Json.blocked -eq 0 -and (Listed (Levels $b.Player 'newest' $b.Token) $second)) "status $($reply.Status)"

$reply = LevelCall 'library_remove' $author $second $null
Check 'library_remove without a session removes a level' ($reply.Status -eq 200 -and $reply.Json.removed -eq $true -and -not (Listed (Levels $author 'mine' $authorToken) $second)) "status $($reply.Status)"

# Player IDs aren't tied to sign-ins, so one sign-in reporting under three made-up IDs must not hide a level
$fourth = [guid]::NewGuid().ToString()
Publish $author $fourth (RandomLayout) $authorToken | Out-Null
$madeUp = @(1..3 | ForEach-Object { [guid]::NewGuid().ToString() })
foreach ($id in $madeUp) {
    LevelCall 'library_report' $id $fourth $b.Token ',"reason":"name"' | Out-Null
}
Check 'three reports from one sign-in under made-up player IDs hide nothing' ([bool](Listed (Levels $readers[1].Player 'newest' $readers[1].Token) $fourth)) 'hidden'
LevelCall 'library_remove' $author $fourth $null | Out-Null
foreach ($id in $madeUp) {
    Send 'rest/v1/rpc/delete_player' ('{"player":"' + $id + '"}') | Out-Null
}

foreach ($reader in $readers) {
    LevelCall 'library_report' $reader.Player $level $reader.Token ',"reason":"picture"' | Out-Null
}
$hiddenForReaders = -not (Listed (Levels $b.Player 'newest' $b.Token) $level)
$row = Listed (Levels $author 'mine' $authorToken) $level
Check 'three reports hide a level, and its author still sees it as hidden' ($hiddenForReaders -and $row -and $row.hidden -eq $true) "hidden from readers $hiddenForReaders"

$reply = LevelCall 'library_remove' $author $level $null
$row = Listed (Levels $author 'mine' $authorToken) $level
Check 'removing a hidden level leaves a tombstone' ($reply.Json.removed -eq $true -and $row -and $row.removed -eq $true -and $null -eq $row.code) "status $($reply.Status)"
$reply = Publish $author $level (RandomLayout) $authorToken
Check "a tombstone's ID can't be published again" ($reply.Status -eq 200 -and $reply.Json.outcome -eq 'removed') "status $($reply.Status), $($reply.Json.outcome)"

$third = [guid]::NewGuid().ToString()
Publish $author $third (RandomLayout) $authorToken | Out-Null
foreach ($reader in $readers) {
    Send 'rest/v1/rpc/delete_player' ('{"player":"' + $reader.Player + '"}') | Out-Null
}
$reply = ReportFunction 'Post' '' @{}
Check 'the report email refuses a POST without its key' ($reply.Status -eq 401 -and $reply.Content -eq 'The report key is wrong.') "status $($reply.Status), $($reply.Content)"
$reply = ReportFunction 'Post' '' @{ 'x-report-key' = 'wrong' }
Check 'the report email refuses a wrong key' ($reply.Status -eq 401 -and $reply.Content -eq 'The report key is wrong.') "status $($reply.Status), $($reply.Content)"
$madeUpLink = '?t=' + [guid]::NewGuid().ToString('N')
$reply = ReportFunction 'Get' $madeUpLink @{}
$noLongerWorks = "This link no longer works: it was used, a newer email replaced it, or it's over 14 days old."
Check 'a made-up link does nothing' ($reply.Status -eq 200 -and $reply.Content -eq $noLongerWorks) "status $($reply.Status), $($reply.Content)"
$reply = ReportFunction 'Get' '' @{}
Check 'a link without a code is not found' ($reply.Status -eq 404 -and $reply.Content -eq 'This link is missing its code.') "status $($reply.Status), $($reply.Content)"
$reply = ReportFunction 'Head' $madeUpLink @{}
Check 'HEAD on a link does nothing' ($reply.Status -eq 204) "status $($reply.Status)"

$kept = '0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10'
if ($Keep) {
    $reply = Publish $author $kept '9595701f6d6c9037' $authorToken -Name 'Long Way Round'
    Check 'the kept level is published' ($reply.Status -eq 200 -and $reply.Json.outcome -eq 'published') "status $($reply.Status), $($reply.Json.outcome)"
    Write-Host "library author: $author"
}
else {
    DeleteAndRefuse $author $authorToken
    $reader = [pscustomobject]@{ Player = [guid]::NewGuid().ToString(); Token = SignIn }
    Check "delete_player takes the author's library levels with it" (-not (Listed (Levels $reader.Player 'newest' $reader.Token) $third)) 'still listed'
    Send 'rest/v1/rpc/delete_player' ('{"player":"' + $reader.Player + '"}') | Out-Null
}

Write-Host "player: $player"
if ($Keep) {
    Write-Host "Kept. Read the rows, then run again with -Delete for each ID above"
}
else {
    DeleteAndRefuse $player $token
}
exit ([int]$script:failed)
