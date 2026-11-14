using System.Net;
using System.Net.Http.Json;
using Prueba.Tests.Helpers;

namespace Prueba.Tests;

/// <summary>Regla 3 desde la API: reintentos del usuario con el mismo externalReference.</summary>
public class RequestControllerTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public RequestControllerTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static object Body(string externalReference)
    {
        return new
        {
            externalReference, customerId = "C1", serviceCode = "S1", description = "Instalación",
        };
    }

    [Fact]
    public async Task Post_crea_solicitud_Pending()
    {
        var res = await _client.PostAsJsonAsync("/api/requests", Body("OP-1"));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Pending\"", json);
    }

    [Fact]
    public async Task Post_repetido_devuelve_409()
    {
        await _client.PostAsJsonAsync("/api/requests", Body("OP-1"));
        var res = await _client.PostAsJsonAsync("/api/requests", Body("OP-1"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Posts_simultaneos_con_mismo_externalReference_crean_una_sola_solicitud()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.PostAsJsonAsync("/api/requests", Body("OP-RACE")));
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var list = await _client.GetFromJsonAsync<List<object>>("/api/requests");
        Assert.Single(list!);
    }

    [Fact]
    public async Task Post_con_campos_vacios_devuelve_400()
    {
        var res = await _client.PostAsJsonAsync("/api/requests",
            new { externalReference = "", customerId = "", serviceCode = "", description = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_filtra_por_estado_y_rechaza_estado_invalido()
    {
        await _client.PostAsJsonAsync("/api/requests", Body("OP-1"));

        var pending = await _client.GetFromJsonAsync<List<object>>("/api/requests?status=Pending");
        var completed = await _client.GetFromJsonAsync<List<object>>("/api/requests?status=Completed");
        var invalid = await _client.GetAsync("/api/requests?status=Foo");

        Assert.Single(pending!);
        Assert.Empty(completed!);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
