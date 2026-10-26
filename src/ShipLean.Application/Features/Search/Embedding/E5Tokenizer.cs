using Microsoft.ML.Tokenizers;

namespace ShipLean.Application.Features.Search.Embedding;

// The XLM-R tokenizer of multilingual-e5: the sentencepiece model with fairseq's id layout (<s>=0, <pad>=1, </s>=2, <unk>=3, every
// other piece shifted up by one). The ids are pinned against Python transformers by E5TokenIds.
public sealed class E5Tokenizer
{
    public const int BeginId = 0;
    public const int PadId = 1;
    public const int EndId = 2;
    public const int UnknownId = 3;

    private readonly SentencePieceTokenizer _tokenizer;

    public E5Tokenizer(string modelPath)
    {
        using var stream = File.OpenRead(modelPath);
        _tokenizer = SentencePieceTokenizer.Create(stream, addBeginningOfSentence: false, addEndOfSentence: false);
    }

    // <s> pieces </s>, cut to maxTokens with </s> kept.
    public IReadOnlyList<int> Encode(string text, int maxTokens)
    {
        var pieces = _tokenizer.EncodeToIds(text);
        var count = Math.Min(pieces.Count, maxTokens - 2);
        var ids = new int[count + 2];
        ids[0] = BeginId;
        for (var i = 0; i < count; i++)
        {
            // sentencepiece's <unk> is 0 and its ids 1 and 2 are <s> and </s>, which never come out of text.
            ids[i + 1] = pieces[i] == 0 ? UnknownId : pieces[i] + 1;
        }
        ids[count + 1] = EndId;
        return ids;
    }
}
