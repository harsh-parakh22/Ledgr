# Reset local SQLite database for development
$ErrorActionPreference = "Stop"

$dbFiles = @(
    "src/Ledgr.Api/ledgr.db",
    "src/Ledgr.Api/ledgr.db-shm",
    "src/Ledgr.Api/ledgr.db-wal",
    "ledgr.db",
    "ledgr.db-shm",
    "ledgr.db-wal"
)

foreach ($file in $dbFiles) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "Removed $file" -ForegroundColor Yellow
    }
}

Write-Host "Database reset complete. Next application run will automatically re-create and re-seed the schema." -ForegroundColor Green
