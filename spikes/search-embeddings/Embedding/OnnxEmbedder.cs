using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SearchEmbeddings.Embedding;

// One InferenceSession; mean pooling over the attention mask or the hidden state at position 0 (CLS), then L2. Padding is skipped, so a
// text's vector does not depend on its batch (up to int8's per-tensor quantisation drift).
public sealed class OnnxEmbedder : IDisposable
{
    private readonly InferenceSession _session;
    private readonly XlmrTokenizer _tokenizer;
    private readonly ModelSpec _spec;
    private readonly Pooling _pooling;
    private readonly bool _hasTokenTypes;

    public OnnxEmbedder(string directory, ModelSpec spec, Pooling pooling, int threads = 2)
    {
        _spec = spec;
        _pooling = pooling;
        _tokenizer = new XlmrTokenizer(Path.Combine(directory, "sentencepiece.bpe.model"));
        using var options = new SessionOptions { IntraOpNumThreads = threads, InterOpNumThreads = 1 };
        _session = new InferenceSession(Path.Combine(directory, "model.onnx"), options);
        _hasTokenTypes = _session.InputMetadata.ContainsKey("token_type_ids");
    }

    public float[] EmbedQuery(string text) => Embed([_spec.QueryPrefix + text])[0];

    // Batches of similar length keep padding small; vectors come back in input order.
    public float[][] EmbedPassages(IReadOnlyList<string> texts, int batch = 16)
    {
        var encoded = texts.Select(t => _tokenizer.Encode(_spec.PassagePrefix + t, _spec.MaxTokens)).ToArray();
        var order = Enumerable.Range(0, encoded.Length).OrderBy(i => encoded[i].Length).ToArray();
        var vectors = new float[encoded.Length][];
        for (var start = 0; start < order.Length; start += batch)
        {
            var slice = order.Skip(start).Take(batch).ToArray();
            var pooled = Run(slice.Select(i => encoded[i]).ToArray());
            for (var k = 0; k < slice.Length; k++)
            {
                vectors[slice[k]] = pooled[k];
            }
        }
        return vectors;
    }

    public void Dispose() => _session.Dispose();

    private float[][] Embed(List<string> texts) => Run([.. texts.Select(t => _tokenizer.Encode(t, _spec.MaxTokens))]);

    private float[][] Run(int[][] encoded)
    {
        var length = encoded.Max(ids => ids.Length);
        var shape = new[] { encoded.Length, length };
        var ids = new long[encoded.Length * length];
        var masks = new long[ids.Length];
        for (var row = 0; row < encoded.Length; row++)
        {
            var offset = row * length;
            Array.Fill(ids, XlmrTokenizer.PadId, offset, length);
            for (var col = 0; col < encoded[row].Length; col++)
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
        var hidden = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();
        var size = hidden.Dimensions[2];
        var flat = ((DenseTensor<float>)hidden).Buffer.Span;
        var vectors = new float[encoded.Length][];
        for (var row = 0; row < encoded.Length; row++)
        {
            vectors[row] = Pool(flat, row * length * size, encoded[row].Length, size);
        }
        return vectors;
    }

    private float[] Pool(ReadOnlySpan<float> flat, int start, int tokens, int size)
    {
        var vector = new float[size];
        var used = _pooling == Pooling.Cls ? 1 : tokens;
        for (var token = 0; token < used; token++)
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
