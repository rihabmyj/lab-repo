using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace ECommerce.Ordering.Api.Tests;

public sealed class CreateOrderTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly OrderingApiFactory _factory;
    private readonly HttpClient _client;

    public CreateOrderTests(PostgresFixture postgres)
    {
        _factory = new OrderingApiFactory(postgres.NewDatabase());
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task ValidOrder_Returns201WithLocationAndTotal()
    {
        var response = await PostOrderAsync("""{"customer":"Ada","items":[{"productId":1,"quantity":2}]}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal("Ada", order.Customer);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(239.98m, order.Total);
    }

    [Fact]
    public async Task EmptyItems_Returns400()
    {
        var response = await PostOrderAsync("""{"customer":"Ada","items":[]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownProduct_Returns400NamingTheProduct()
    {
        var response = await PostOrderAsync("""{"customer":"Ada","items":[{"productId":99,"quantity":1}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("99", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EmptyCustomer_Returns400()
    {
        var response = await PostOrderAsync("""{"customer":"","items":[{"productId":1,"quantity":1}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ZeroQuantity_Returns400()
    {
        var response = await PostOrderAsync("""{"customer":"Ada","items":[{"productId":1,"quantity":0}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingItemsProperty_Returns400()
    {
        // Before the fix, Items is null and the endpoint throws: HTTP 500.
        var response = await PostOrderAsync("""{"customer":"Ada"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The exact JSON a client sends, written out so each scenario reads at a glance.
    private Task<HttpResponseMessage> PostOrderAsync(string json) =>
        _client.PostAsync("/api/orders", new StringContent(json, Encoding.UTF8, "application/json"));

    // The consumer's view of the contract: Status travels as a string.
    private sealed record OrderResponse(int Id, string Customer, string Status, decimal Total);
}
