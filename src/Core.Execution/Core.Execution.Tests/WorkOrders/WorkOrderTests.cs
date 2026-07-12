using Core.Execution.Domain.WorkOrders;
using Xunit;

namespace Core.Execution.Tests.WorkOrders;

public class WorkOrderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    private static WorkOrder NovaOrdem() => new(Guid.NewGuid(), line: "2", product: "peça-x", quantity: 100);

    [Fact]
    public void Fluxo_feliz_percorre_todos_os_estados_emitindo_eventos()
    {
        var ordem = NovaOrdem();
        Assert.Equal(WorkOrderState.Draft, ordem.State);

        var liberada = ordem.Release(Now);
        Assert.Equal("ordem.liberada", liberada.Type);
        Assert.Equal(WorkOrderState.Released, ordem.State);

        var iniciada = ordem.Start(Now);
        Assert.Equal("ordem.iniciada", iniciada.Type);

        var concluida = ordem.Complete(Now);
        Assert.Equal("ordem.concluida", concluida.Type);
        Assert.Equal(WorkOrderState.Completed, ordem.State);
    }

    [Fact]
    public void Iniciar_sem_liberar_e_recusado()
    {
        var ordem = NovaOrdem();
        Assert.Throws<DomainException>(() => ordem.Start(Now));
        Assert.Equal(WorkOrderState.Draft, ordem.State); // estado não muda em transição inválida
    }

    [Fact]
    public void Abortar_e_permitido_de_qualquer_estado_nao_terminal()
    {
        var emRascunho = NovaOrdem();
        Assert.Equal("ordem.abortada", emRascunho.Abort(Now).Type);

        var emExecucao = NovaOrdem();
        emExecucao.Release(Now);
        emExecucao.Start(Now);
        Assert.Equal("ordem.abortada", emExecucao.Abort(Now).Type);
    }

    [Fact]
    public void Abortar_ordem_concluida_e_recusado()
    {
        var ordem = NovaOrdem();
        ordem.Release(Now);
        ordem.Start(Now);
        ordem.Complete(Now);

        Assert.Throws<DomainException>(() => ordem.Abort(Now));
    }

    [Fact]
    public void Quantidade_nao_positiva_e_recusada_na_criacao()
    {
        Assert.Throws<DomainException>(() => new WorkOrder(Guid.NewGuid(), "2", "peça-x", 0));
    }
}
