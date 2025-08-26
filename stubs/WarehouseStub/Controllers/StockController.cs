using Microsoft.AspNetCore.Mvc;
using WarehouseStub.Models;
using System.Collections.Concurrent;

namespace WarehouseStub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly ILogger<StockController> _logger;
    private static readonly ConcurrentDictionary<string, Stock> _stockData = new();

    static StockController()
    {
        // Initialize with sample stock data
        SeedStockData();
    }

    public StockController(ILogger<StockController> logger)
    {
        _logger = logger;
    }

    [HttpGet("{productId}")]
    public async Task<ActionResult<ApiResponse<Stock>>> GetStock(string productId)
    {
        _logger.LogInformation("Warehouse: Getting stock for product {ProductId}", productId);
        
        // Simulate processing time
        await Task.Delay(Random.Shared.Next(20, 100));
        
        // Simulate occasional failures for circuit breaker testing
        if (Random.Shared.Next(1, 100) <= 1) // 1% failure rate
        {
            _logger.LogWarning("Warehouse: Simulated service failure for product {ProductId}", productId);
            return StatusCode(503, new ApiResponse<Stock>
            {
                Success = false,
                ErrorMessage = "Warehouse service temporarily unavailable",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        if (!_stockData.TryGetValue(productId, out var stock))
        {
            _logger.LogInformation("Warehouse: Stock not found for product {ProductId}", productId);
            return NotFound(new ApiResponse<Stock>
            {
                Success = false,
                ErrorMessage = "Stock not found for product",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        // Simulate stock level changes over time
        SimulateStockFluctuation(stock);

        return Ok(new ApiResponse<Stock>
        {
            Success = true,
            Data = stock,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    [HttpGet("bulk")]
    public async Task<ActionResult<ApiResponse<BulkStockResponse>>> GetBulkStock([FromQuery] List<string> productIds)
    {
        _logger.LogInformation("Warehouse: Getting bulk stock for {ProductCount} products", productIds.Count);
        
        await Task.Delay(Random.Shared.Next(50, 200));
        
        // Simulate occasional failures
        if (Random.Shared.Next(1, 100) <= 1) // 1% failure rate
        {
            _logger.LogWarning("Warehouse: Simulated bulk service failure");
            return StatusCode(503, new ApiResponse<BulkStockResponse>
            {
                Success = false,
                ErrorMessage = "Warehouse service temporarily unavailable",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        var response = new BulkStockResponse();
        
        foreach (var productId in productIds)
        {
            if (_stockData.TryGetValue(productId, out var stock))
            {
                // Simulate stock level changes
                SimulateStockFluctuation(stock);
                response.Stocks.Add(stock);
            }
            else
            {
                response.NotFound.Add(productId);
            }
        }

        _logger.LogInformation("Warehouse: Found stock for {FoundCount}/{TotalCount} products", 
            response.Stocks.Count, productIds.Count);

        return Ok(new ApiResponse<BulkStockResponse>
        {
            Success = true,
            Data = response,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    [HttpPost("{productId}/reserve")]
    public async Task<ActionResult<ApiResponse<Stock>>> ReserveStock(string productId, [FromBody] int quantity)
    {
        _logger.LogInformation("Warehouse: Reserving {Quantity} units for product {ProductId}", quantity, productId);
        
        await Task.Delay(Random.Shared.Next(30, 100));
        
        if (!_stockData.TryGetValue(productId, out var stock))
        {
            return NotFound(new ApiResponse<Stock>
            {
                Success = false,
                ErrorMessage = "Stock not found for product",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        if (stock.AvailableQuantity < quantity)
        {
            return BadRequest(new ApiResponse<Stock>
            {
                Success = false,
                ErrorMessage = $"Insufficient stock. Available: {stock.AvailableQuantity}, Requested: {quantity}",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        stock.ReservedQuantity += quantity;
        stock.LastUpdated = DateTime.UtcNow;
        
        return Ok(new ApiResponse<Stock>
        {
            Success = true,
            Data = stock,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    [HttpPost("{productId}/release")]
    public async Task<ActionResult<ApiResponse<Stock>>> ReleaseStock(string productId, [FromBody] int quantity)
    {
        _logger.LogInformation("Warehouse: Releasing {Quantity} units for product {ProductId}", quantity, productId);
        
        await Task.Delay(Random.Shared.Next(20, 80));
        
        if (!_stockData.TryGetValue(productId, out var stock))
        {
            return NotFound(new ApiResponse<Stock>
            {
                Success = false,
                ErrorMessage = "Stock not found for product",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        stock.ReservedQuantity = Math.Max(0, stock.ReservedQuantity - quantity);
        stock.LastUpdated = DateTime.UtcNow;
        
        return Ok(new ApiResponse<Stock>
        {
            Success = true,
            Data = stock,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    [HttpPut("{productId}")]
    public async Task<ActionResult<ApiResponse<Stock>>> UpdateStock(string productId, [FromBody] Stock updatedStock)
    {
        _logger.LogInformation("Warehouse: Updating stock for product {ProductId}", productId);
        
        await Task.Delay(Random.Shared.Next(50, 150));
        
        if (!_stockData.TryGetValue(productId, out var stock))
        {
            // Create new stock entry
            stock = new Stock
            {
                ProductId = productId,
                Location = GenerateLocation()
            };
            _stockData.TryAdd(productId, stock);
        }

        stock.Quantity = updatedStock.Quantity;
        stock.InStock = updatedStock.Quantity > 0;
        stock.Location = updatedStock.Location ?? stock.Location;
        stock.LastUpdated = DateTime.UtcNow;
        
        return Ok(new ApiResponse<Stock>
        {
            Success = true,
            Data = stock,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    private static void SeedStockData()
    {
        var sampleStocks = new[]
        {
            new Stock { ProductId = "prod-001", Quantity = 25, InStock = true, Location = "Warehouse-A-01", ReservedQuantity = 3 },
            new Stock { ProductId = "prod-002", Quantity = 150, InStock = true, Location = "Warehouse-A-02", ReservedQuantity = 15 },
            new Stock { ProductId = "prod-003", Quantity = 75, InStock = true, Location = "Warehouse-B-01", ReservedQuantity = 5 },
            new Stock { ProductId = "prod-004", Quantity = 50, InStock = true, Location = "Warehouse-B-02", ReservedQuantity = 2 },
            new Stock { ProductId = "prod-005", Quantity = 100, InStock = true, Location = "Warehouse-C-01", ReservedQuantity = 10 }
        };

        foreach (var stock in sampleStocks)
        {
            _stockData.TryAdd(stock.ProductId, stock);
        }
    }

    private static void SimulateStockFluctuation(Stock stock)
    {
        // Simulate small random changes in stock levels (realistic warehouse operations)
        var changePercent = Random.Shared.Next(-5, 10); // -5% to +10% change
        var originalQuantity = stock.Quantity;
        
        if (changePercent != 0)
        {
            var change = (int)(originalQuantity * changePercent / 100.0);
            stock.Quantity = Math.Max(0, originalQuantity + change);
            stock.InStock = stock.Quantity > 0;
            stock.LastUpdated = DateTime.UtcNow;
        }
    }

    private static string GenerateLocation()
    {
        var warehouses = new[] { "Warehouse-A", "Warehouse-B", "Warehouse-C" };
        var zones = new[] { "01", "02", "03", "04", "05" };
        
        var warehouse = warehouses[Random.Shared.Next(warehouses.Length)];
        var zone = zones[Random.Shared.Next(zones.Length)];
        
        return $"{warehouse}-{zone}";
    }
}