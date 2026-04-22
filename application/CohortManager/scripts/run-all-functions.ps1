# Launches every Azure Function in its own Windows Terminal tab.
# Edit the $functions list below to add/remove/reorder functions - it's the single source of truth.

$ErrorActionPreference = "Stop"
$functionsRoot = Resolve-Path (Join-Path $PSScriptRoot "..\src\Functions")

$functions = @(
    # ── CaaS Integration ────────────────────────────────────────────────────────
    @{ Path = "CaasIntegration\receiveCaasFile";                                       Port = 7060 },

    # ── Participant Management Services ──────────────────────────────────────────
    @{ Path = "ParticipantManagementServices\ManageParticipant";                       Port = 7061 },
    @{ Path = "ParticipantManagementServices\ManageServiceNowParticipant";             Port = 7064 },
    @{ Path = "ParticipantManagementServices\UpdateBlockedFlag";                       Port = 7027 },

    # ── Audit Services ───────────────────────────────────────────────────────────
    @{ Path = "AuditServices\AuditWriter";                                             Port = 7062 },

    # ── Exception Handling ───────────────────────────────────────────────────────
    @{ Path = "ExceptionHandling\CreateException";                                     Port = 7070 },
    @{ Path = "ExceptionHandling\UpdateException";                                     Port = 7073 },

    # ── Screening Validation Service ─────────────────────────────────────────────
    @{ Path = "ScreeningValidationService\StaticValidation";                           Port = 7074 },
    @{ Path = "ScreeningValidationService\LookupValidation";                           Port = 7075 },
    @{ Path = "ScreeningValidationService\RemoveValidationException";                  Port = 7085 },

    # ── Demographic Services ─────────────────────────────────────────────────────
    @{ Path = "DemographicServices\DemographicDurableFunction";                        Port = 7079 },
    @{ Path = "DemographicServices\RetrievePDSDemographic";                            Port = 8082 },
    @{ Path = "DemographicServices\ManageCaasSubscription";                            Port = 9084 },

    # ── Cohort Distribution Services ─────────────────────────────────────────────
    @{ Path = "CohortDistributionServices\DistributeParticipant";                      Port = 7063 },
    @{ Path = "CohortDistributionServices\TransformDataService";                       Port = 7080 },
    @{ Path = "CohortDistributionServices\RetrieveCohortDistribution";                 Port = 7078 },
    @{ Path = "CohortDistributionServices\RetrieveCohortRequestAudit";                 Port = 7086 },

    # ── Service Now Integration ───────────────────────────────────────────────────
    @{ Path = "ServiceNowIntegration\ServiceNowMessageHandler";                        Port = 9092 },
    @{ Path = "ServiceNowIntegration\ServiceNowCohortLookup";                          Port = 7180 },

    # ── Screening Data Services ───────────────────────────────────────────────────
    @{ Path = "screeningDataServices\ExceptionManagementDataService";                  Port = 7911 },
    @{ Path = "screeningDataServices\ScreeningLkpDataService";                         Port = 8996 },
    @{ Path = "screeningDataServices\ParticipantDemographicDataService";               Port = 7993 },
    @{ Path = "screeningDataServices\ParticipantManagementDataService";                Port = 7994 },
    @{ Path = "screeningDataServices\CohortDistributionDataService";                   Port = 7992 },
    @{ Path = "screeningDataServices\ReferenceDataService";                            Port = 7988 },
    @{ Path = "screeningDataServices\GetValidationExceptions";                         Port = 7071 },
    @{ Path = "screeningDataServices\BsSelectRequestAudit";                            Port = 7989 },
    @{ Path = "screeningDataServices\NemsSubscriptionDataService";                     Port = 7990 },
    @{ Path = "screeningDataServices\GeneCodeLkpDataService";                          Port = 7991 },
    @{ Path = "screeningDataServices\HigherRiskReferralReasonLkpDataService";          Port = 7972 },
    @{ Path = "screeningDataServices\ServiceNowCasesDataService";                      Port = 9996 }
)

Write-Host "Functions root: $functionsRoot" -ForegroundColor DarkGray

# Build all functions first
Write-Host "Building $($functions.Count) Azure Functions..." -ForegroundColor Cyan
foreach ($fn in $functions) {
    $cwd = Join-Path $functionsRoot $fn.Path
    $name = Split-Path $fn.Path -Leaf

    if (-not (Test-Path $cwd)) {
        Write-Host "  [SKIP] $name - path not found: $cwd" -ForegroundColor Yellow
        continue
    }

    Write-Host "  [BUILD] $name" -ForegroundColor DarkCyan
    $result = & dotnet publish "$cwd" -o "$cwd\bin\output" --nologo -v q 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [FAIL]  $name build failed:`n$result" -ForegroundColor Red
        exit 1
    }
}

Write-Host "All functions built successfully.`n" -ForegroundColor Green

# Launch all functions
Write-Host "Starting $($functions.Count) Azure Functions..." -ForegroundColor Cyan

$hasWT = [bool](Get-Command wt.exe -ErrorAction SilentlyContinue)
if (-not $hasWT) {
    Write-Host "Windows Terminal (wt.exe) not found - falling back to separate pwsh windows." -ForegroundColor Yellow
}

foreach ($fn in $functions) {
    $cwd = Join-Path $functionsRoot $fn.Path
    $name = Split-Path $fn.Path -Leaf
    $title = "$name :$($fn.Port)"

    if (-not (Test-Path $cwd)) {
        Write-Host "  [SKIP] $title - path not found: $cwd" -ForegroundColor Yellow
        continue
    }

    Write-Host "  [RUN]  $title" -ForegroundColor Green

    if ($hasWT) {
        $wtArgs = "-w CohortFunctions new-tab --title `"$title`" -d `"$cwd`" pwsh.exe -NoExit -Command `"func start --port $($fn.Port)`""
        Start-Process wt.exe -ArgumentList $wtArgs
    } else {
        Start-Process pwsh.exe -ArgumentList @(
            "-NoExit", "-Command",
            "Set-Location '$cwd'; `$Host.UI.RawUI.WindowTitle='$title'; func start --port $($fn.Port)"
        )
    }

    # Waits until the port is actually listening before starting the next function
    $timeout = 60
    $elapsed = 0
    Write-Host "    Waiting for port $($fn.Port)..." -ForegroundColor DarkGray
    while ($elapsed -lt $timeout) {
        $tcp = [System.Net.Sockets.TcpClient]::new()
        try {
            $tcp.Connect("localhost", $fn.Port)
            $tcp.Close()
            Write-Host "    Port $($fn.Port) is up." -ForegroundColor DarkGreen
            break
        } catch {
            Start-Sleep -Seconds 1
            $elapsed++
        } finally {
            $tcp.Dispose()
        }
    }
    if ($elapsed -ge $timeout) {
        Write-Host "    [WARN] Timed out waiting for port $($fn.Port) - continuing anyway." -ForegroundColor Yellow
    }
}

Write-Host "`nAll functions launched. Check the 'CohortFunctions' Windows Terminal window." -ForegroundColor Cyan

