using System.IO.Compression;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;

namespace Data.Archiver.Worker;

/// <summary>
/// Escrita no data lake (MinIO ou qualquer S3): JSONL comprimido com gzip.
/// PutObject é atômico no S3 — ou o objeto inteiro aparece, ou nada aparece;
/// não existe objeto meio-escrito pra engine analítica tropeçar.
/// </summary>
public sealed class S3ObjectStore(IAmazonS3 s3, string bucket)
{
    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        var buckets = await s3.ListBucketsAsync(ct);
        if (buckets.Buckets?.Any(b => b.BucketName == bucket) != true)
            await s3.PutBucketAsync(bucket, ct);
    }

    /// <summary>Comprime e grava o lote; devolve os bytes escritos (pra métrica).</summary>
    public async Task<long> PutGzipJsonLinesAsync(string key, IReadOnlyList<string> lines, CancellationToken ct)
    {
        using var payload = new MemoryStream();
        using (var gzip = new GZipStream(payload, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8))
        {
            foreach (var line in lines)
                await writer.WriteLineAsync(line.AsMemory(), ct);
        }

        payload.Position = 0;
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = payload,
            ContentType = "application/gzip",
        }, ct);
        return payload.Length;
    }
}
