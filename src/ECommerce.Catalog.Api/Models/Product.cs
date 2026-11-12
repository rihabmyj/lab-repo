namespace ECommerce.Catalog.Api.Models;

public class Product
{
    // Category of the products that existed before categories were introduced.
    public const string DefaultCategory = "Uncategorized";

    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int AvailableStock { get; set; }
    public string Category { get; set; } = DefaultCategory;
}
