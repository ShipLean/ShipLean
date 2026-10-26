using ShipLean.Application.Features.Search.Index.Chunking;

namespace ShipLean.UnitTest.Search.Chunking;

[TestFixture]
public class TextChunkerTests
{
    private const int Max = 1200;
    private const int Overlap = 150;

    [Test]
    public void Chunk_WhenShort_ShouldBeOneChunk()
    {
        var chunks = TextChunker.Chunk("A short description.\n\nWith two paragraphs.", null, Max, Overlap);

        chunks.ShouldBe(["A short description.\n\nWith two paragraphs."]);
    }

    [Test]
    public void Chunk_WhenLong_ShouldSplitOnParagraphs()
    {
        var paragraphs = Enumerable.Range(1, 6).Select(n => $"Paragraph {n}. " + new string('x', 480)).ToList();

        var chunks = TextChunker.Chunk(string.Join("\n\n", paragraphs), null, Max, Overlap);

        chunks.Count.ShouldBe(3);
        chunks[0].ShouldBe($"{paragraphs[0]}\n\n{paragraphs[1]}");
        // Every paragraph starts a chunk or sits whole inside one: none is cut.
        foreach (var paragraph in paragraphs)
        {
            chunks.ShouldContain(c => c.Contains(paragraph));
        }
    }

    [Test]
    public void Chunk_WhenParagraphTooLong_ShouldSplitOnSentences()
    {
        var sentences = Enumerable.Range(1, 40).Select(n => $"Sentence number {n} says something quite ordinary.").ToList();

        var chunks = TextChunker.Chunk(string.Join(' ', sentences), null, Max, Overlap);

        chunks.Count.ShouldBeGreaterThan(1);
        chunks.ShouldAllBe(c => c.Length <= Max + Overlap + 2);
        foreach (var sentence in sentences)
        {
            chunks.ShouldContain(c => c.Contains(sentence));
        }
        chunks[0].ShouldEndWith(".");
    }

    [Test]
    public void Chunk_WhenSentenceTooLong_ShouldCutAtTheLimit()
    {
        var chunks = TextChunker.Chunk(new string('y', 2500), null, Max, 0);

        chunks.Select(c => c.Length).ShouldBe([1200, 1200, 100]);
    }

    [Test]
    public void Chunk_ShouldOverlapAndPrefixTitle()
    {
        var paragraphs = Enumerable.Range(1, 4).Select(n => $"P{n} " + string.Join(' ', Enumerable.Repeat($"word{n}", 100))).ToList();

        var chunks = TextChunker.Chunk(string.Join("\n\n", paragraphs), "Checkout fails", Max, Overlap);

        chunks.Count.ShouldBeGreaterThan(1);
        chunks.ShouldAllBe(c => c.StartsWith("Checkout fails\n"));
        var first = chunks[0]["Checkout fails\n".Length..];
        var second = chunks[1]["Checkout fails\n".Length..];
        // The next chunk begins with the end of the previous one, from a word start and no more than the overlap.
        var carried = second[..second.IndexOf("\n\n", StringComparison.Ordinal)];
        carried.Length.ShouldBeLessThanOrEqualTo(Overlap);
        first.ShouldEndWith(carried);
        carried.ShouldNotBeEmpty();
    }

    [Test]
    public void Chunk_WhenBlank_ShouldBeNoChunks()
    {
        TextChunker.Chunk(" \n\n ", "Title", Max, Overlap).ShouldBeEmpty();
    }

    [Test]
    public void Chunk_WhenEmojiStraddlesTheLimit_ShouldNotSplitIt()
    {
        // The emoji is two UTF-16 chars at indexes 1199 and 1200: a cut at 1200 would separate them.
        var text = new string('a', 1199) + "\U0001F600" + new string('b', 100);

        var chunks = TextChunker.Chunk(text, null, Max, 0);

        chunks.ShouldBe([new string('a', 1199), "\U0001F600" + new string('b', 100)]);
    }

    [Test]
    public void Chunk_WhenOverlapTailStartsInAnEmoji_ShouldKeepItWhole()
    {
        var chunks = TextChunker.Chunk(string.Concat(Enumerable.Repeat("\U0001F600", 2000)), null, Max, 151);

        chunks.Count.ShouldBeGreaterThan(2);
        chunks.ShouldAllBe(c => IsWellFormed(c));
    }

    private static bool IsWellFormed(string text)
    {
        try
        {
            new System.Text.UTF8Encoding(false, true).GetBytes(text);
            return true;
        }
        catch (System.Text.EncoderFallbackException)
        {
            return false;
        }
    }
}
