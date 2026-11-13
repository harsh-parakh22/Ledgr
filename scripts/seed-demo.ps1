# Seed sample inventory and orders via API
$ErrorActionPreference = "Stop"

$baseUrl = if ($args[0]) { $args[0] } else { "http://localhost:5000" }

Write-Host "Checking health of $baseUrl..." -ForegroundColor Cyan

try {
    $kpis = Invoke-RestMethod -Uri "$baseUrl/api/dashboard" -Method Get
    Write-Host "Connected! Current inventory value: `$$($kpis.totalInventoryValue)" -ForegroundColor Green
} catch {
    Write-Warning "Could not connect to $baseUrl. Ensure Ledgr.Api is running first (`dotnet run --project src/Ledgr.Api`)."
}
