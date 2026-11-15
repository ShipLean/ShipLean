using ShipLean.Application.Features.Inspection.Query;

namespace ShipLean.UnitTest.Inspection;

[TestFixture]
public class QueryTextTests
{
    private static readonly string[] Hostile =
    [
        "SHIP-1\" || true",
        "SHIP-1\\",
        "SHIP-1\\\" }",
        "SHIP-1\nSHIP-2",
        "SHIP-1\r\t}",
        "} { \"a\"=\"b",
        "plain",
    ];

    [TestCaseSource(nameof(Hostile))]
    public void TraceQlString_WhenTheValueIsHostile_ShouldStayOneStringThatReadsBackAsTheValue(string value)
    {
        var literal = QueryText.TraceQlString(value);

        ReadLiteral(literal).ShouldBe(value);
    }

    [TestCaseSource(nameof(Hostile))]
    public void LogQlString_WhenTheValueIsHostile_ShouldStayOneStringThatReadsBackAsTheValue(string value)
    {
        var literal = QueryText.LogQlString(value);

        ReadLiteral(literal).ShouldBe(value);
    }

    [Test]
    public void TraceQlString_WhenTheValueHasABackslashThenAQuote_ShouldEscapeBoth()
    {
        QueryText.TraceQlString("a\\\"b").ShouldBe("\"a\\\\\\\"b\"");
    }

    [TestCase("otel")]
    [TestCase("otel_traces")]
    [TestCase("_x1")]
    public void Identifier_WhenValid_ShouldQuoteIt(string name)
    {
        QueryText.Identifier(name).ShouldBe($"`{name}`");
    }

    [TestCase("otel;DROP")]
    [TestCase("otel traces")]
    [TestCase("1otel")]
    [TestCase("")]
    [TestCase("a`b")]
    [TestCase("a\n")]
    [TestCase("a.b")]
    public void Identifier_WhenItCouldBreakOut_ShouldBeRefused(string name)
    {
        Should.Throw<ArgumentException>(() => QueryText.Identifier(name));
    }

    [TestCase("shiplean.item", "'shiplean.item'")]
    [TestCase("it's", "'it\\'s'")]
    [TestCase("a\\b", "'a\\\\b'")]
    [TestCase("a\\'b", "'a\\\\\\'b'")]
    [TestCase("x' OR 1=1 --", "'x\\' OR 1=1 --'")]
    public void ClickHouseString_WhenTheValueHasAQuoteOrBackslash_ShouldEscapeThem(string value, string expected)
    {
        QueryText.ClickHouseString(value).ShouldBe(expected);
    }

    [Test]
    public void ClickHouseString_WhenTheValueIsEmpty_ShouldBeRefused()
    {
        Should.Throw<ArgumentException>(() => QueryText.ClickHouseString(""));
    }

    [TestCase("a\nb")]
    [TestCase("a\rb")]
    [TestCase("a\0b")]
    [TestCase("a\tb")]
    public void ClickHouseString_WhenTheValueHasAControlCharacter_ShouldBeRefused(string value)
    {
        Should.Throw<ArgumentException>(() => QueryText.ClickHouseString(value));
    }

    // Reads a double-quoted literal the way TraceQL and LogQL do: it must start and end with an unescaped quote and hold no
    // other unescaped quote, so nothing in the value can end the string early.
    private static string ReadLiteral(string literal)
    {
        literal.ShouldStartWith("\"");
        literal.ShouldEndWith("\"");
        var value = new System.Text.StringBuilder();
        var body = literal[1..^1];
        var at = 0;
        while (at < body.Length)
        {
            var c = body[at++];
            if (c == '"')
            {
                throw new ShouldAssertException($"Unescaped quote in {literal}");
            }
            if (c != '\\')
            {
                value.Append(c);
                continue;
            }
            var escaped = body[at++];
            if (escaped == 'u')
            {
                value.Append((char)Convert.ToInt32(body.Substring(at, 4), 16));
                at += 4;
                continue;
            }
            value.Append(escaped switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => escaped });
        }
        return value.ToString();
    }
}
