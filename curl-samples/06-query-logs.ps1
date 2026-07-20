# =====================================================================
#  Query the adapter's per-call audit logs (rate-calls-yyyyMMdd.jsonl).
#
#  Dot-source it so $log and the helpers stay in your session:
#      . .\curl-samples\06-query-logs.ps1
#      . .\curl-samples\06-query-logs.ps1 -Days 30
#
#  Then use $log with the example queries at the bottom. Runs on the
#  server against C:\inetpub\SOAP_hijacker\logs by default; point -LogDir
#  elsewhere if you've copied the .jsonl files off the box.
# =====================================================================
param(
    [string]$LogDir = 'C:\inetpub\SOAP_hijacker\logs',
    [int]$Days = 7
)

# Parse GLP's string money figures culture-safely ("22.92" -> 22.92 regardless of locale).
function script:ToDec($v) {
    if ($null -ne $v -and "$v" -ne '') {
        [decimal]::Parse([string]$v, [Globalization.CultureInfo]::InvariantCulture)
    }
}

# Load recent log lines as flat objects (the key figures pulled up; full record kept in .Raw).
function Import-RateLog {
    param([string]$Dir = $LogDir, [int]$Days = 7)

    $since = (Get-Date).AddDays(-$Days).Date
    Get-ChildItem -Path $Dir -Filter 'rate-calls-*.jsonl' -ErrorAction Stop |
        Where-Object { $_.LastWriteTime -ge $since } |
        Get-Content |
        Where-Object { $_.Trim() } |
        ForEach-Object {
            $r = $_ | ConvertFrom-Json
            # glpResp is a JSON array on success, a string on a non-JSON GLP error, or absent.
            $g = if ($r.glpResp -is [array] -and $r.glpResp.Count) { $r.glpResp[0] } else { $null }
            [pscustomobject]@{
                Ts           = [datetimeoffset]$r.ts
                Op           = $r.op
                Route        = $r.route
                Account      = $r.account
                Dest         = $r.destCountry
                Status       = $r.status
                Ms           = $r.ms
                Freight      = ToDec $g.FreightCost
                Fuel         = ToDec $g.FuelSurcharge
                TotalFreight = ToDec $g.TotalFreightCost
                Landed       = ToDec $g.TotalTaxesDuties
                Total        = ToDec $g.TotalCost
                Error        = $r.error
                Id           = $r.id
                Raw          = $r
            }
        }
}

$log = Import-RateLog -Dir $LogDir -Days $Days
Write-Host ("Loaded {0} calls from the last {1} day(s) in {2}." -f $log.Count, $Days, $LogDir)
Write-Host "Use `$log with the example queries in the comments below (dot-source this script first)."

<#  ============================ EXAMPLE QUERIES ============================
    ?  = Where-Object   %  = ForEach-Object   ft = Format-Table

# Every call for one account, newest last:
$log | ? Account -eq '3528' | sort Ts | ft Ts,Op,Dest,Freight,Fuel,TotalFreight,Total,Status

# Only the faults / errors (HTTP >= 400 or a captured error):
$log | ? { $_.Status -ge 400 -or $_.Error } | ft Ts,Op,Account,Status,Error -Wrap

# Latency summary (milliseconds):
$log | measure Ms -Average -Maximum -Minimum

# How many calls by operation + routing (translate vs passthrough):
$log | group Op,Route | sort Count -Descending | ft Count,Name

# Average total freight by destination country:
$log | ? TotalFreight | group Dest | % {
    [pscustomobject]@{
        Dest  = $_.Name
        Calls = $_.Count
        AvgTotalFreight = [math]::Round((($_.Group.TotalFreight | measure -Average).Average), 2)
    }
} | sort AvgTotalFreight -Descending | ft

# Fuel surcharge as a share of freight (the fuel-inclusive-pricing question), per call:
$log | ? Freight | select Ts,Account,Dest,Freight,Fuel,
    @{n='Fuel%';e={ [math]::Round($_.Fuel / $_.Freight * 100, 1) }} | ft

# Calls in a specific window:
$log | ? { $_.Ts -ge [datetimeoffset]'2026-07-20T00:00:00Z' -and $_.Ts -lt [datetimeoffset]'2026-07-21T00:00:00Z' } | ft Ts,Op,Account,Total,Status

# Daily call volume:
$log | group { $_.Ts.ToString('yyyy-MM-dd') } | sort Name | ft Name,Count

# Full forensic record for one call (inbound SOAP, GLP req/resp, outbound) by id:
($log | ? Id -eq 'PASTE-ID-HERE').Raw | ConvertTo-Json -Depth 10

# Export a slice to CSV for a spreadsheet:
$log | ? Account -eq '3528' | select Ts,Op,Dest,Freight,Fuel,TotalFreight,Landed,Total,Status |
    Export-Csv .\account-3528.csv -NoTypeInformation

    NOTE: for a large date range, raise -Days but expect higher memory — each row keeps the full
    record (with bodies) in .Raw. Drop .Raw (select everything except Raw) if you only need figures.
========================================================================= #>
