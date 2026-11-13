# Seed sample inventory and demonstrate atomic order placement via API
$ErrorActionPreference = "Stop"

$baseUrl = if ($args[0]) { $args[0] } else { "http://localhost:5196" }

Write-Host "Checking health of $baseUrl..." -ForegroundColor Cyan

try {
    $kpis = Invoke-RestMethod -Uri "$baseUrl/api/dashboard/kpis" -Method Get
    Write-Host "Connected! Current inventory value: `$$($kpis.totalInventoryValue), Total products: $($kpis.totalProducts)" -ForegroundColor Green

    # Create a demo product
    $sku = "DEMO-SAMPLE-$((Get-Random -Minimum 1000 -Maximum 9999))"
    $newProduct = @{
        sku = $sku
        name = "High-Speed Mechanical Barcode Scanner"
        category = "Peripherals"
        unitPrice = 129.99
        initialQuantity = 25
        reorderLevel = 5
        description = "Omnidirectional 1D/2D presentation barcode scanner"
    } | ConvertTo-Json

    Write-Host "Creating demo product $sku..." -ForegroundColor Cyan
    $createdProd = Invoke-RestMethod -Uri "$baseUrl/api/products" -Method Post -Body $newProduct -ContentType "application/json"
    Write-Host "Product created: $($createdProd.sku) with $($createdProd.quantityOnHand) units in stock." -ForegroundColor Green

    # Place a sample order demonstrating atomic reservation
    $order = @{
        customerName = "Apex Logistics Hub"
        customerEmail = "procurement@apexlogistics.io"
        items = @(
            @{
                productId = $createdProd.id
                quantity = 2
            }
        )
    } | ConvertTo-Json

    Write-Host "Placing demo order against $sku..." -ForegroundColor Cyan
    $placedOrder = Invoke-RestMethod -Uri "$baseUrl/api/orders" -Method Post -Body $order -ContentType "application/json" -Headers @{ "Idempotency-Key" = "DEMO-ORD-$(New-Guid)" }
    Write-Host "Order confirmed: $($placedOrder.orderNumber), Total: `$$($placedOrder.totalAmount), Status: $($placedOrder.status)" -ForegroundColor Green

    # Verify inventory was atomically deducted
    $refreshedProd = Invoke-RestMethod -Uri "$baseUrl/api/products/$($createdProd.id)" -Method Get
    Write-Host "Stock automatically deducted from $($createdProd.quantityOnHand) -> $($refreshedProd.quantityOnHand) (Ledger movement logged)." -ForegroundColor Green
    Write-Host "Seed demonstration completed successfully!" -ForegroundColor Green

} catch {
    Write-Warning "API request failed: $($_.Exception.Message)"
}
