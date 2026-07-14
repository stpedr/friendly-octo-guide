using System.Net.Http.Json;
using PactNet;
using Xunit;

namespace Contracts.Tests;

/// <summary>
/// Contrato consumidor-dirigido (Pact) entre um consumidor (o Gateway/BFF, que
/// agrega respostas dos serviços) e o provider Core.Execution. O consumidor
/// declara AQUI o que espera de GET /v1/core/ordens/{id}; o pact gerado é o
/// contrato que o provider verifica no pipeline dele. Se o Core mudar o shape da
/// ordem, a verificação do provider quebra — o contrato pega antes da integração.
/// </summary>
public class CoreExecutionContractTests
{
    private readonly IPactBuilderV4 _pact = Pact.V4("gateway-bff", "core-execution",
        new PactConfig { PactDir = Path.Combine("..", "..", "..", "..", "pacts") }).WithHttpInteractions();

    [Fact]
    public async Task Ordem_existente_devolve_o_shape_esperado()
    {
        _pact
            .UponReceiving("uma consulta de ordem existente")
                .Given("a ordem 3f2504e0-4f89-41d3-9a0c-0305e82c3301 existe")
                .WithRequest(HttpMethod.Get, "/v1/core/ordens/3f2504e0-4f89-41d3-9a0c-0305e82c3301")
            .WillRespond()
                .WithStatus(System.Net.HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new
                {
                    id = "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
                    line = "linha-2",
                    product = "peça-x",
                    quantity = 100,
                    state = "Released",
                });

        await _pact.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            var dto = await client.GetFromJsonAsync<OrderDto>("/v1/core/ordens/3f2504e0-4f89-41d3-9a0c-0305e82c3301");

            Assert.NotNull(dto);
            Assert.Equal("linha-2", dto!.Line);
            Assert.Equal(100, dto.Quantity);
            Assert.Equal("Released", dto.State);
        });
    }

    private sealed record OrderDto(string Id, string Line, string Product, int Quantity, string State);
}
