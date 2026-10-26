using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Embedding;

// multilingual-e5 on ONNX Runtime: one InferenceSession for the process, mean pooling over the attention mask, L2 normalised.
// Calls may overlap (InferenceSession.Run is thread-safe). Load throws when the directory is unusable; EmbedderServices turns that into NoEmbedder.
public sealed class OnnxEmbedder : IEmbedder, IDisposable
{
    private const string ModelFile = "model.onnx";
    private const string HiddenOutput = "last_hidden_state";
    private const string TokenizerFile = "sentencepiece.bpe.model";

    private readonly InferenceSession _session;
    private readonly E5Tokenizer _tokenizer;
    private readonly ModelManifest _manifest;
    private readonly int _batch;
    private readonly bool _hasTokenTypes;

    private OnnxEmbedder(InferenceSession session, E5Tokenizer tokenizer, ModelManifest manifest, int batch)
    {
        _session = session;
        _tokenizer = tokenizer;
        _manifest = manifest;
        _batch = batch;
        _hasTokenTypes = session.InputMetadata.ContainsKey("token_type_ids");
        ModelId = new EmbeddingModelId(manifest.Id, manifest.Revision);
    }

    public EmbeddingModelId? ModelId { get; }

    public int Dimensions => _manifest.Dimensions;

    public EmbedderStatus Status { get; } = new(EmbedderMode.Semantic);

    public static OnnxEmbedder Load(string directory, SearchOptions options)
    {
        var manifest = ModelManifest.Read(directory);
        var tokenizer = new E5Tokenizer(Path.Combine(directory, TokenizerFile));
        using var sessionOptions = new SessionOptions { IntraOpNumThreads = options.Threads, InterOpNumThreads = 1 };
        var session = new InferenceSession(Path.Combine(directory, ModelFile), sessionOptions);
        try
        {
            var embedder = new OnnxEmbedder(session, tokenizer, manifest, options.Batch);
            // Fail at load, not on the first search, if the model does not produce the stated size.
            var probe = embedder.Embed([manifest.PassagePrefix + "probe"])[0];
            return probe.Length == manifest.Dimensions
                ? embedder
                : throw new InvalidDataException($"The model produced {probe.Length} dimensions; {ModelManifest.FileName} says {manifest.Dimensions}.");
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
        => Task.Run(() => Embed([_manifest.QueryPrefix + text])[0], cancellationToken);

    public Task<IReadOnlyList<float[]>> EmbedPassagesAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<float[]>>(() => EmbedAll(texts.Select(t => _manifest.PassagePrefix + t).ToList(), cancellationToken), cancellationToken);

    public void Dispose() => _session.Dispose();

    private List<float[]> EmbedAll(List<string> texts, CancellationToken cancellationToken)
    {
        var vectors = new List<float[]>(texts.Count);
        for (var start = 0; start < texts.Count; start += _batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            vectors.AddRange(Embed(texts.GetRange(start, Math.Min(_batch, texts.Count - start))));
        }
        return vectors;
    }

    private List<float[]> Embed(List<string> texts)
    {
        var encoded = texts.Select(t => _tokenizer.Encode(t, _manifest.MaxTokens)).ToList();
        var length = encoded.Max(ids => ids.Count);
        var shape = new[] { encoded.Count, length };
        // Plain arrays: the Tensor indexer allocates per element.
        var ids = new long[encoded.Count * length];
        var masks = new long[ids.Length];
        for (var row = 0; row < encoded.Count; row++)
        {
            var offset = row * length;
            Array.Fill(ids, E5Tokenizer.PadId, offset, length);
            for (var col = 0; col < encoded[row].Count; col++)
            {
                ids[offset + col] = encoded[row][col];
                masks[offset + col] = 1;
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, shape)),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(masks, shape)),
        };
        if (_hasTokenTypes)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[ids.Length], shape)));
        }

        using var results = _session.Run(inputs);
        var hidden = results.First(r => r.Name == HiddenOutput).AsTensor<float>();
        var size = hidden.Dimensions[2];
        var flat = ((DenseTensor<float>)hidden).Buffer.Span;
        var vectors = new List<float[]>(encoded.Count);
        for (var row = 0; row < encoded.Count; row++)
        {
            vectors.Add(MeanPool(flat, (row * length) * size, encoded[row].Count, size));
        }
        return vectors;
    }

    // Padding positions are skipped, so a text's vector does not depend on what else is in its batch.
    private static float[] MeanPool(ReadOnlySpan<float> flat, int start, int tokens, int size)
    {
        var vector = new float[size];
        for (var token = 0; token < tokens; token++)
        {
            var source = flat.Slice(start + token * size, size);
            for (var d = 0; d < size; d++)
            {
                vector[d] += source[d];
            }
        }
        var sum = 0f;
        foreach (var x in vector)
        {
            sum += x * x;
        }
        var norm = MathF.Sqrt(sum);
        for (var d = 0; d < size; d++)
        {
            vector[d] = norm > 0 ? vector[d] / norm : 0;
        }
        return vector;
    }
}
