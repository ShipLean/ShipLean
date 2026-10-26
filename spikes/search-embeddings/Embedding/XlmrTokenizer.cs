using Microsoft.ML.Tokenizers;

namespace SearchEmbeddings.Embedding;

// The XLM-R tokenizer shared by multilingual-e5 and paraphrase-multilingual-MiniLM: the sentencepiece model with fairseq's id
// layout (<s>=0, <pad>=1, </s>=2, <unk>=3, every other piece shifted up by one). Same recipe as PR B's E5Tokenizer; parity.py pins it.
public sealed class XlmrTokenizer
{
    public const int BeginId = 0;
    public const int PadId = 1;
    public const int EndId = 2;
    public const int UnknownId = 3;

    private readonly SentencePieceTokenizer _tokenizer;

    public XlmrTokenizer(string modelPath)
    {
        using var stream = File.OpenRead(modelPath);
        _tokenizer = SentencePieceTokenizer.Create(stream, addBeginningOfSentence: false, addEndOfSentence: false);
    }

    // <s> pieces </s>, cut to maxTokens with </s> kept.
    public int[] Encode(string text, int maxTokens)
    {
        var pieces = _tokenizer.EncodeToIds(text);
        var count = Math.Min(pieces.Count, maxTokens - 2);
        var ids = new int[count + 2];
        ids[0] = BeginId;
        for (var i = 0; i < count; i++)
        {
            ids[i + 1] = pieces[i] == 0 ? UnknownId : pieces[i] + 1;
        }
        ids[count + 1] = EndId;
        return ids;
    }
}
