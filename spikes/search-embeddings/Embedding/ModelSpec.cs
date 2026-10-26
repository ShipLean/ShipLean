namespace SearchEmbeddings.Embedding;

// One model as the spike runs it. Directories are under ~/.cache/shiplean/models (never the repo); --model-dir overrides.
public sealed record ModelSpec(string DefaultDir, string QueryPrefix, string PassagePrefix, int MaxTokens)
{
    private static readonly string Models = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache/shiplean/models");

    // The production file: intfloat's onnx/model_qint8_avx512_vnni.onnx.
    public static ModelSpec E5Int8 { get; } = new(Path.Combine(Models, "multilingual-e5-small"), "query: ", "passage: ", 512);

    public static ModelSpec E5Fp32 { get; } = new(Path.Combine(Models, "spike/e5-small-fp32"), "query: ", "passage: ", 512);

    // sentence-transformers' max_seq_length is 128; longer chunks are cut there, which is part of what this model costs.
    public static ModelSpec MiniLm { get; } = new(Path.Combine(Models, "spike/minilm-l12-multilingual"), "", "", 128);

    // A5: the same model read to 256 tokens, to see how much of MiniLM's gap is the 128-token cut.
    public static ModelSpec MiniLm256 { get; } = MiniLm with { MaxTokens = 256 };
}
