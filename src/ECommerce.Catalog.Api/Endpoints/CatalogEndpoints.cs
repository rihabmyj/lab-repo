using ECommerce.Catalog.Api.Data;
using ECommerce.Catalog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Api.Endpoints;

public static class CatalogEndpoints
{
    // Same limits as the columns in CatalogDbContext: PostgreSQL rejects longer values.
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const int MaxCategoryLength = 100;

    public static RouteGroupBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/products").WithTags("Catalog");

        group.MapGet("/", async (CatalogDbContext db) =>
            await db.Products.AsNoTracking().ToListAsync())
            .WithName("GetProducts");

        group.MapGet("/{id:int}", async (int id, CatalogDbContext db) =>
            await db.Products.FindAsync(id) is { } product
                ? Results.Ok(product)
                : Results.NotFound())
            .WithName("GetProductById");

        group.MapPost("/", async (CreateProductRequest request, CatalogDbContext db) =>
        {
            if (Validate(request) is { } error)
            {
                return Results.BadRequest(error);
            }

            var product = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                AvailableStock = request.AvailableStock,
                Category = string.IsNullOrWhiteSpace(request.Category) ? Product.DefaultCategory : request.Category
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            return Results.CreatedAtRoute("GetProductById", new { id = product.Id }, product);
        })
        .WithName("CreateProduct");

        return group;
    }

    // Returns the first validation error, or null when the request is valid.
    private static string? Validate(CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        if (request.Name.Length > MaxNameLength)
        {
            return $"Name must not exceed {MaxNameLength} characters.";
        }

        if (request.Description?.Length > MaxDescriptionLength)
        {
            return $"Description must not exceed {MaxDescriptionLength} characters.";
        }

        if (request.Category?.Length > MaxCategoryLength)
        {
            return $"Category must not exceed {MaxCategoryLength} characters.";
        }

        return null;
    }
}

public record CreateProductRequest(string Name, string? Description, decimal Price, int AvailableStock, string? Category = null);
