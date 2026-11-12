using System.Net;
using System.Net.Http.Json;
using ECommerce.Ordering.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Ordering.Api.Tests;

/// <summary>
/// Hosts Ordering.Api in memory against a real PostgreSQL database and a fake Catalog.
/// Give each factory its own database so that no test sees another test's orders.
/// </summary>
public sealed class OrderingApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Aspire normally injects this setting. The address is never called:
        // the fake handler below answers instead of the network.
        builder.UseSetting("services:catalog:http:0", "http://catalog.test");
        builder.UseSetting("ConnectionStrings:orderingdb", connectionString);

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<CatalogServiceClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new FakeCatalogHandler());
        });
    }
}

/// <summary>
/// Stands in for Catalog.Api: answers GET /api/products/{id} for the known
/// products, and 404 for anything else, like the real service.
/// </summary>
public sealed class FakeCatalogHandler : HttpMessageHandler
{
    private const string ProductsPath = "/api/products/";

    private static readonly Dictionary<int, CatalogProduct> Products = new()
    {
        [1] = new CatalogProduct(1, "Mechanical Keyboard", "Hot-swappable RGB keyboard", 119.99m, 50),
        [2] = new CatalogProduct(2, "Wireless Mouse", "Ergonomic 8k DPI mouse", 49.50m, 120),
    };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        if (request.Method == HttpMethod.Get
            && path.StartsWith(ProductsPath, StringComparison.Ordinal)
            && int.TryParse(path[ProductsPath.Length..], out var id)
            && Products.TryGetValue(id, out var product))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(product)
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
