namespace Core.Execution.Domain.WorkOrders;

public enum WorkOrderState { Draft, Released, InProgress, Completed, Aborted }

public sealed record WorkOrderEvent(Guid OrderId, string Type, DateTimeOffset OccurredAt);

/// <summary>
/// Ordem de produção — o agregado central do domínio de execução.
/// Toda transição válida devolve o evento correspondente; o chamador grava
/// agregado + evento na MESMA transação (outbox) — nunca um sem o outro.
/// Transição inválida é recusada com exceção de domínio, não ignorada.
/// </summary>
public sealed class WorkOrder
{
    public Guid Id { get; }
    public string Line { get; }
    public string Product { get; }
    public int Quantity { get; }
    public WorkOrderState State { get; private set; }

    public WorkOrder(Guid id, string line, string product, int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantidade deve ser positiva.");
        Id = id;
        Line = line;
        Product = product;
        Quantity = quantity;
        State = WorkOrderState.Draft;
    }

    public WorkOrderEvent Release(DateTimeOffset now) =>
        Transition(WorkOrderState.Draft, WorkOrderState.Released, "ordem.liberada", now);

    public WorkOrderEvent Start(DateTimeOffset now) =>
        Transition(WorkOrderState.Released, WorkOrderState.InProgress, "ordem.iniciada", now);

    public WorkOrderEvent Complete(DateTimeOffset now) =>
        Transition(WorkOrderState.InProgress, WorkOrderState.Completed, "ordem.concluida", now);

    /// <summary>Abortar é permitido de qualquer estado não-terminal — parada de linha não espera fluxo feliz.</summary>
    public WorkOrderEvent Abort(DateTimeOffset now)
    {
        if (State is WorkOrderState.Completed or WorkOrderState.Aborted)
            throw new DomainException($"Ordem em estado terminal ({State}) não pode ser abortada.");
        State = WorkOrderState.Aborted;
        return new WorkOrderEvent(Id, "ordem.abortada", now);
    }

    private WorkOrderEvent Transition(WorkOrderState from, WorkOrderState to, string eventType, DateTimeOffset now)
    {
        if (State != from)
            throw new DomainException($"Transição inválida: {State} → {to} (exigia {from}).");
        State = to;
        return new WorkOrderEvent(Id, eventType, now);
    }
}

public sealed class DomainException(string message) : Exception(message);
