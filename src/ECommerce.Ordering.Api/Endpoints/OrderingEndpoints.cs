using ECommerce.Ordering.Api.Data;
using ECommerce.Ordering.Api.Models;
using ECommerce.Ordering.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Ordering.Api.Endpoints;

public static class OrderingEndpoints
{
    // Matches HasMaxLength(200) on Order.Customer in OrderingDbContext.
    private const int MaxCustomerLength = 200;

    public static RouteGroupBuilder MapOrderingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/orders").WithTags("Ordering");

        group.MapGet("/", async (OrderingDbContext db) =>
            await db.Orders.AsNoTracking().Include(o => o.Items).ToListAsync())
            .WithName("GetOrders");

        group.MapGet("/{id:int}", async (int id, OrderingDbContext db) =>
            await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id) is { } order
                ? Results.Ok(order)
                : Results.NotFound())
            .WithName("GetOrderById");

        group.MapPost("/", async (
            CreateOrderRequest request,
            OrderingDbContext db,
            CatalogServiceClient catalog,
            CancellationToken ct) =>
        {
            // Validate the request itself before calling the Catalog service. Properties can be
            // null when missing from the JSON body, despite the non-nullable record types.
            var customer = request.Customer?.Trim();
            if (string.IsNullOrEmpty(customer))
            {
                return Results.BadRequest("A customer name is required.");
            }

            if (customer.Length > MaxCustomerLength)
            {
                return Results.BadRequest($"The customer name cannot exceed {MaxCustomerLength} characters.");
            }

            if (request.Items is null || request.Items.Count == 0)
            {
                return Results.BadRequest("An order must contain at least one item.");
            }

            if (request.Items.Any(i => i is null))
            {
                return Results.BadRequest("Order items cannot be null.");
            }

            if (request.Items.FirstOrDefault(i => i.Quantity <= 0) is { } invalidLine)
            {
                return Results.BadRequest($"Quantity for product {invalidLine.ProductId} must be greater than zero.");
            }

            // Total requested per product, so that the same product listed on several
            // lines is checked against the stock as a whole.
            var requestedByProduct = request.Items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity));

            var order = new Order
            {
                Customer = customer,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = OrderStatus.Pending
            };

            // Validate each product against the Catalog service (sync HTTP call).
            foreach (var item in request.Items)
            {
                var product = await catalog.GetProductAsync(item.ProductId, ct);
                if (product is null)
                {
                    return Results.BadRequest($"Product {item.ProductId} does not exist.");
                }

                if (requestedByProduct[item.ProductId] > product.AvailableStock)
                {
                    return Results.BadRequest(
                        $"Insufficient stock for product {item.ProductId}: {product.AvailableStock} available, {requestedByProduct[item.ProductId]} requested.");
                }

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = item.Quantity
                });
            }

            order.Status = OrderStatus.Confirmed;
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

            return Results.CreatedAtRoute("GetOrderById", new { id = order.Id }, order);
        })
        .WithName("CreateOrder");

        return group;
    }
}

public record CreateOrderRequest(string Customer, List<OrderLine> Items);
public record OrderLine(int ProductId, int Quantity);
