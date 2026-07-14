namespace Data.Archiver.Domain.Lineage;

/// <summary>
/// Evento OpenLineage de um objeto arquivado no data lake — a linhagem que torna o
/// histórico auditável: de qual tópico/partição veio (input), pra qual objeto S3 foi
/// (output), com que versão de schema e por qual serviço. Modelo no formato do spec
/// OpenLineage (RunEvent) pra o Marquez consumir sem tradução.
/// </summary>
public sealed record OpenLineageEvent(
    string EventType,
    string EventTime,
    RunNode Run,
    JobNode Job,
    IReadOnlyList<Dataset> Inputs,
    IReadOnlyList<Dataset> Outputs,
    string Producer);

public sealed record RunNode(string RunId);
public sealed record JobNode(string Namespace, string Name);
public sealed record Dataset(string Namespace, string Name, DatasetFacets Facets);
public sealed record DatasetFacets(SchemaFacet? Schema = null, ProducerFacet? DataSource = null, long? RecordCount = null);
public sealed record SchemaFacet(string Version);
public sealed record ProducerFacet(string Service);

/// <summary>
/// Constrói o evento de linhagem de um objeto. Puro e determinístico (tempo e runId
/// entram por parâmetro): mesmo objeto arquivado → mesma linhagem.
/// </summary>
public static class LineageBuilder
{
    public const string Namespace = "plataforma-linha";
    public const string JobName = "data-archiver";
    private const string ProducerUri = "https://github.com/stpedr/friendly-octo-guide/tree/main/src/Data.Archiver";

    public static OpenLineageEvent ForArchivedObject(
        string sourceTopic, int partition, long firstOffset,
        string bucket, string objectKey,
        long recordCount, string schemaVersion, string producerService,
        Guid runId, DateTimeOffset eventTime)
    {
        var input = new Dataset(
            Namespace: $"kafka://{sourceTopic}",
            Name: $"{sourceTopic}/partition={partition}/offset={firstOffset}",
            Facets: new DatasetFacets(DataSource: new ProducerFacet(producerService)));

        var output = new Dataset(
            Namespace: $"s3://{bucket}",
            Name: objectKey,
            Facets: new DatasetFacets(
                Schema: new SchemaFacet(schemaVersion),
                DataSource: new ProducerFacet(producerService),
                RecordCount: recordCount));

        return new OpenLineageEvent(
            EventType: "COMPLETE",
            EventTime: eventTime.ToUniversalTime().ToString("O"),
            Run: new RunNode(runId.ToString()),
            Job: new JobNode(Namespace, JobName),
            Inputs: [input],
            Outputs: [output],
            Producer: ProducerUri);
    }
}
