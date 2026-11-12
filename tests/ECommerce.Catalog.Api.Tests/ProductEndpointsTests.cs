using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ECommerce.Catalog.Api.Tests;

/// <summary>
/// Hosts Catalog.Api in memory against a real PostgreSQL database. Program.cs applies
/// the migrations on startup, so the database has the real schema and the seeded products.
/// </summary>
public sealed class CatalogApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    // Aspire normally injects this connection string.
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:catalogdb", connectionString);
}

public sealed class ProductEndpointsTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly CatalogApiFactory _factory;
    private readonly HttpClient _client;

    public ProductEndpointsTests(PostgresFixture postgres)
    {
        _factory = new CatalogApiFactory(postgres.NewDatabase());
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetProducts_ReturnsTheFourSeededProducts()
    {
        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products");

        Assert.NotNull(products);
        Assert.Equal(4, products.Count);
    }

    [Fact]
    public async Task GetProducts_EverySeededProductHasARealCategory()
    {
        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products");

        Assert.NotNull(products);
        Assert.All(products, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Category));
            Assert.NotEqual("Uncategorized", p.Category);
        });
    }

    [Fact]
    public async Task GetProductById_ReturnsTheProduct()
    {
        var product = await _client.GetFromJsonAsync<ProductResponse>("/api/products/1");

        Assert.NotNull(product);
        Assert.Equal("Mechanical Keyboard", product.Name);
        Assert.Equal(119.99m, product.Price);
        Assert.Equal("Peripherals", product.Category);
    }

    [Fact]
    public async Task GetProductById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/products/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithoutCategory_GetsTheDefaultCategory()
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            new { name = "Webcam", description = "1080p webcam", price = 59.90m, availableStock = 10 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        Assert.Equal(5, product.Id);
        Assert.Equal("Uncategorized", product.Category);
    }

    [Fact]
    public async Task CreateProduct_NameTooLong_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            new { name = new string('A', 201), price = 10m, availableStock = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WhitespaceCategory_GetsTheDefaultCategory()
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            new { name = "Webcam", price = 59.90m, availableStock = 10, category = "   " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        Assert.Equal("Uncategorized", product.Category);
    }

    [Theory]
    [InlineData(100, HttpStatusCode.Created)]
    [InlineData(101, HttpStatusCode.BadRequest)]
    public async Task CreateProduct_CategoryLength_RespectsDatabaseLimit(int length, HttpStatusCode expected)
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            new { name = "Webcam", price = 59.90m, availableStock = 10, category = new string('C', length) });

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Created)
        {
            var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.NotNull(product);
            Assert.Equal(new string('C', length), product.Category);
        }
    }

    private sealed record ProductResponse(int Id, string Name, decimal Price, int AvailableStock, string Category);
}
