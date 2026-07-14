using System.Net;
using System.Text;
using Predictive.Domain.Registry;
using Predictive.Domain.Scoring;
using Predictive.Worker;
using Xunit;

namespace Predictive.Tests.Registry;

/// <summary>
/// O MlflowClient testado sem MLflow: um handler fake responde à API REST. O que
/// está sob teste é o CONTRATO — os endpoints chamados e a extração da resposta.
/// </summary>
public class MlflowClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static MlflowClient Client(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://mlflow.test") });

    [Fact]
    public async Task RegisterRun_cria_loga_e_finaliza_o_run()
    {
        var handler = new FakeHandler(req =>
            req.RequestUri!.AbsolutePath.EndsWith("runs/create", StringComparison.Ordinal)
                ? Json("""{"run":{"info":{"run_id":"run-42"}}}""")
                : Json("{}"));

        var run = RecalibrationPlanner.Build(0.1, 1.0, 30, 500, new DriftReading(1.7, true));
        var runId = await Client(handler).RegisterRunAsync("exp-1", run, default);

        Assert.Equal("run-42", runId);
        Assert.Contains(handler.Paths, p => p.Contains("runs/create", StringComparison.Ordinal));
        Assert.Contains(handler.Paths, p => p.Contains("runs/log-batch", StringComparison.Ordinal));
        Assert.Contains(handler.Paths, p => p.Contains("runs/update", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActiveModelVersion_extrai_a_versao_em_producao()
    {
        var client = Client(new FakeHandler(_ =>
            Json("""{"model_versions":[{"version":"7"}]}""")));
        Assert.Equal("7", await client.ActiveModelVersionAsync("predictive-anomaly", default));
    }

    [Fact]
    public async Task ActiveModelVersion_sem_producao_devolve_null()
    {
        var client = Client(new FakeHandler(_ => Json("""{"model_versions":[]}""")));
        Assert.Null(await client.ActiveModelVersionAsync("predictive-anomaly", default));
    }

    [Fact]
    public async Task ActiveModelVersion_registry_fora_devolve_null()
    {
        var client = Client(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.Null(await client.ActiveModelVersionAsync("x", default));
    }
}
