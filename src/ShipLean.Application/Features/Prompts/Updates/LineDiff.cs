namespace ShipLean.Application.Features.Prompts.Updates;

/// <summary>Myers' O(ND) line diff. Deletions are preferred before insertions, so a replaced line reads "-old" then "+new", as git writes it.</summary>
internal static class LineDiff
{
    public static IReadOnlyList<LineEdit> Edits(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        // Equal ends are never part of a minimal change; trimming them keeps D small for the usual small edit.
        var prefix = 0;
        while (prefix < a.Count && prefix < b.Count && a[prefix] == b[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < a.Count - prefix && suffix < b.Count - prefix && a[a.Count - 1 - suffix] == b[b.Count - 1 - suffix])
        {
            suffix++;
        }

        var middle = Middle(a, b, prefix, a.Count - prefix - suffix, b.Count - prefix - suffix);
        var edits = new List<LineEdit>(prefix + middle.Count + suffix);
        for (var i = 0; i < prefix; i++)
        {
            edits.Add(new LineEdit(' ', i, i));
        }

        edits.AddRange(middle);
        for (var i = suffix; i > 0; i--)
        {
            edits.Add(new LineEdit(' ', a.Count - i, b.Count - i));
        }

        return edits;
    }

    private static List<LineEdit> Middle(IReadOnlyList<string> a, IReadOnlyList<string> b, int offset, int n, int m)
    {
        var result = new List<LineEdit>();
        if (n == 0 || m == 0)
        {
            for (var i = 0; i < n; i++)
            {
                result.Add(new LineEdit('-', offset + i, offset));
            }

            for (var j = 0; j < m; j++)
            {
                result.Add(new LineEdit('+', offset, offset + j));
            }

            return result;
        }

        var trace = Forward(a, b, offset, n, m);
        Backtrack(trace, offset, n, m, result);
        return result;
    }

    // trace[d] holds the furthest x per diagonal k after round d, for k in -d..d (index k + d).
    private static List<int[]> Forward(IReadOnlyList<string> a, IReadOnlyList<string> b, int offset, int n, int m)
    {
        var trace = new List<int[]>();
        var previous = Array.Empty<int>();
        for (var d = 0; d <= n + m; d++)
        {
            var current = new int[(2 * d) + 1];
            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (d == 0)
                {
                    x = 0;
                }
                else if (k == -d || (k != d && previous[k - 1 + d - 1] < previous[k + 1 + d - 1]))
                {
                    x = previous[k + 1 + d - 1]; // step down: an insertion
                }
                else
                {
                    x = previous[k - 1 + d - 1] + 1; // step right: a deletion
                }

                var y = x - k;
                while (x < n && y < m && a[offset + x] == b[offset + y])
                {
                    x++;
                    y++;
                }

                current[k + d] = x;
                if (x >= n && y >= m)
                {
                    trace.Add(current);
                    return trace;
                }
            }

            trace.Add(current);
            previous = current;
        }

        throw new InvalidOperationException("Myers diff always ends by round n + m.");
    }

    private static void Backtrack(List<int[]> trace, int offset, int n, int m, List<LineEdit> result)
    {
        var x = n;
        var y = m;
        for (var d = trace.Count - 1; d > 0; d--)
        {
            var previous = trace[d - 1];
            var k = x - y;
            var down = k == -d || (k != d && previous[k - 1 + d - 1] < previous[k + 1 + d - 1]);
            var previousK = down ? k + 1 : k - 1;
            var previousX = previous[previousK + d - 1];
            var previousY = previousX - previousK;

            while (x > previousX && y > previousY)
            {
                x--;
                y--;
                result.Add(new LineEdit(' ', offset + x, offset + y));
            }

            if (down)
            {
                y--;
                result.Add(new LineEdit('+', offset + x, offset + y));
            }
            else
            {
                x--;
                result.Add(new LineEdit('-', offset + x, offset + y));
            }
        }

        // Round 0 is only the leading diagonal.
        while (x > 0 && y > 0)
        {
            x--;
            y--;
            result.Add(new LineEdit(' ', offset + x, offset + y));
        }

        result.Reverse();
    }
}
