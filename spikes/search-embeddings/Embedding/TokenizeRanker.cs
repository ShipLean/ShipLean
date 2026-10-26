using System.Text.Json;
using SearchEmbeddings.Corpus;

namespace SearchEmbeddings.Embedding;

// parity.py's door into the C# tokenizer (Program.cs has no tokenizer flag): --inputs texts.json holds a JSON array of strings, --model-dir the
// directory with sentencepiece.bpe.model, --chunk-size the max token count. The ids go to texts.json.ids.json; the returned rankings are empty.
public sealed class TokenizeRanker : IRanker
{
    public string Label => "tokenize";

    public Rankings Rank(IReadOnlyList<CorpusFile> corpus, IReadOnlyList<EvalQuery> queries, RankerOptions options)
    {
        var input = options.Inputs.Single();
        var texts = JsonSerializer.Deserialize<string[]>(File.ReadAllText(input)) ?? throw new InvalidDataException(input);
        var tokenizer = new XlmrTokenizer(Path.Combine(options.ModelDir ?? throw new ArgumentException("--model-dir is required"), "sentencepiece.bpe.model"));
        File.WriteAllText(input + ".ids.json", JsonSerializer.Serialize(texts.Select(t => tokenizer.Encode(t, options.ChunkSize))));
        return new Rankings(Label, [], []);
    }
}
