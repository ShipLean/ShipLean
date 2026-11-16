namespace ShipLean.UnitTest.Architecture;

// C# source with comments and string contents blanked out (newlines kept), so a scan of Code only sees code.
// Interpolation holes stay in Code. Plain literals are kept in Literals with where they start; interpolation holes that
// have a format clause are kept in HoleFormats with the literal text straight after the hole. Handles regular, verbatim,
// raw and interpolated strings (nested in holes too) and char literals. A pure lexer for ViewerTimeZoneScanner.
public sealed class CSharpMask
{
    private const int AfterLength = 4;

    private readonly string _text;
    private readonly char[] _code;
    private readonly List<(int Start, string Value)> _literals = [];
    private readonly List<(string Format, string After)> _holeFormats = [];
    private int _i;

    private CSharpMask(string text)
    {
        _text = text;
        _code = text.ToCharArray();
    }

    public string Code => new(_code);

    public IReadOnlyList<(int Start, string Value)> Literals => _literals;

    public IReadOnlyList<(string Format, string After)> HoleFormats => _holeFormats;

    public static CSharpMask Read(string text)
    {
        var mask = new CSharpMask(text);
        mask.ReadCode(inHole: false);
        return mask;
    }

    // Reads code up to the end, or in a hole up to its ',' (alignment), ':' (format) or '}' at depth zero.
    private void ReadCode(bool inHole)
    {
        var depth = 0;
        while (_i < _text.Length)
        {
            if (SkipComment() || ReadString() || SkipChar())
            {
                continue;
            }

            var c = _text[_i];
            if (inHole && depth == 0 && c is ',' or ':' or '}')
            {
                return;
            }

            depth += c switch { '(' or '[' or '{' => 1, ')' or ']' or '}' => -1, _ => 0 };
            _i++;
        }
    }

    private bool SkipComment()
    {
        if (At("//"))
        {
            var end = _text.IndexOf('\n', _i);
            end = end < 0 ? _text.Length : end;
            Blank(_i, end);
            _i = end;
            return true;
        }

        if (At("/*"))
        {
            var end = _text.IndexOf("*/", _i + 2, StringComparison.Ordinal);
            end = end < 0 ? _text.Length : end + 2;
            Blank(_i, end);
            _i = end;
            return true;
        }

        return false;
    }

    private bool SkipChar()
    {
        if (_text[_i] != '\'')
        {
            return false;
        }

        var start = _i++;
        while (_i < _text.Length && _text[_i] != '\'')
        {
            _i += _text[_i] == '\\' ? 2 : 1;
        }

        _i++;
        Blank(start, _i);
        return true;
    }

    // A string literal starting at _i: prefixes $ (any number) and @ in either order, then " or a raw """ run.
    private bool ReadString()
    {
        var start = _i;
        var p = _i;
        var dollars = 0;
        var verbatim = false;
        while (p < _text.Length && (_text[p] == '$' || _text[p] == '@'))
        {
            dollars += _text[p] == '$' ? 1 : 0;
            verbatim |= _text[p] == '@';
            p++;
        }

        if (p >= _text.Length || _text[p] != '"')
        {
            return false;
        }

        _i = p;
        var quotes = CountRun(p, '"');
        if (quotes >= 3)
        {
            ReadRawString(start, quotes, dollars);
        }
        else
        {
            ReadQuotedString(start, verbatim, dollars > 0);
        }

        return true;
    }

    private void ReadRawString(int start, int quotes, int dollars)
    {
        Blank(start, _i + quotes);
        _i += quotes;
        var content = _i;
        var closer = new string('"', quotes);
        while (_i < _text.Length && !At(closer))
        {
            if (dollars > 0 && CountRun(_i, '{') >= dollars)
            {
                Blank(_i, _i + dollars);
                _i += dollars;
                ReadHole(dollars);
                continue;
            }

            BlankOne();
        }

        AddLiteral(start, content, dollars);
        Blank(_i, Math.Min(_i + quotes, _text.Length));
        _i += quotes;
    }

    private void ReadQuotedString(int start, bool verbatim, bool interpolated)
    {
        Blank(start, _i + 1);
        _i++;
        var content = _i;
        while (_i < _text.Length && !EndsQuoted(verbatim))
        {
            if (interpolated && _text[_i] == '{' && !At("{{"))
            {
                BlankOne();
                ReadHole(1);
                continue;
            }

            var width = (verbatim && At("\"\"")) || (!verbatim && _text[_i] == '\\') || (interpolated && (At("{{") || At("}}"))) ? 2 : 1;
            Blank(_i, Math.Min(_i + width, _text.Length));
            _i += width;
        }

        AddLiteral(start, content, interpolated ? 1 : 0);
        BlankOne();
    }

    private bool EndsQuoted(bool verbatim) => _text[_i] == '"' && !(verbatim && At("\"\""));

    // _i is just inside the hole's opening brace(s); reads to just after its closing brace(s).
    private void ReadHole(int braces)
    {
        ReadCode(inHole: true);
        if (_i < _text.Length && _text[_i] == ',')
        {
            while (_i < _text.Length && _text[_i] is not (':' or '}'))
            {
                _i++;
            }
        }

        if (_i < _text.Length && _text[_i] == ':')
        {
            var formatStart = ++_i;
            while (_i < _text.Length && _text[_i] != '}')
            {
                _i++;
            }

            var format = _text[formatStart.._i];
            Blank(formatStart - 1, _i);
            var afterEnd = Math.Min(_i + braces + AfterLength, _text.Length);
            _holeFormats.Add((format, _text[Math.Min(_i + braces, _text.Length)..afterEnd]));
        }

        Blank(_i, Math.Min(_i + braces, _text.Length));
        _i += braces;
    }

    private void AddLiteral(int start, int content, int interpolated)
    {
        if (interpolated == 0)
        {
            _literals.Add((start, _text[content..Math.Min(_i, _text.Length)]));
        }
    }

    private int CountRun(int from, char c)
    {
        var n = 0;
        while (from + n < _text.Length && _text[from + n] == c)
        {
            n++;
        }

        return n;
    }

    private bool At(string s) => string.CompareOrdinal(_text, _i, s, 0, s.Length) == 0;

    private void BlankOne()
    {
        Blank(_i, Math.Min(_i + 1, _text.Length));
        _i++;
    }

    private void Blank(int from, int to)
    {
        for (var k = from; k < Math.Min(to, _code.Length); k++)
        {
            if (_code[k] != '\n')
            {
                _code[k] = ' ';
            }
        }
    }
}
