namespace ShipLean.Application.Features.Prompts.Updates;

/// <summary>
/// Myers' line diff in linear space (middle snake, divide and conquer). Inside a changed run every deletion comes before the insertions,
/// so a replaced line reads "-old" then "+new", as git writes it.
/// </summary>
internal static class LineDiff
{
    // Work bound: texts are admin input up to 64 KB, so a pair that shares almost nothing must not cost much. When one
    // sub-problem needs more than this many edits, its middle is written as "delete all, insert all": still a correct edit
    // script, only larger, and Merge then reports overlapping changes as a conflict (safe). Each search costs at most
    // MaxEditsPerSearch squared steps, so the worst case is well under a second.
    private const int MaxEditsPerSearch = 1000;

    public static IReadOnlyList<LineEdit> Edits(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var builder = new Builder(a, b);
        builder.Solve(0, a.Count, 0, b.Count);
        return builder.Finish();
    }

    private sealed class Builder(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        private readonly List<LineEdit> edits = [];
        private int ia;
        private int ib;
        private int pendingDeletes;
        private int pendingInserts;

        public IReadOnlyList<LineEdit> Finish()
        {
            Flush();
            return edits;
        }

        public void Solve(int aLo, int aHi, int bLo, int bHi)
        {
            // Equal ends are never part of a minimal change; trimming them keeps the search small for the usual small edit.
            while (aLo < aHi && bLo < bHi && a[aLo] == b[bLo])
            {
                Equal();
                aLo++;
                bLo++;
            }

            var tail = 0;
            while (aLo < aHi && bLo < bHi && a[aHi - 1] == b[bHi - 1])
            {
                aHi--;
                bHi--;
                tail++;
            }

            if (aLo == aHi || bLo == bHi)
            {
                Replace(aHi - aLo, bHi - bLo);
            }
            else if (MiddleSnake(aLo, aHi, bLo, bHi) is { } snake)
            {
                Solve(aLo, aLo + snake.X0, bLo, bLo + snake.Y0);
                for (var i = snake.X0; i < snake.X1; i++)
                {
                    Equal();
                }

                Solve(aLo + snake.X1, aHi, bLo + snake.Y1, bHi);
            }
            else
            {
                Replace(aHi - aLo, bHi - bLo);
            }

            for (var i = 0; i < tail; i++)
            {
                Equal();
            }
        }

        private void Replace(int deletes, int inserts)
        {
            pendingDeletes += deletes;
            pendingInserts += inserts;
            ia += deletes;
            ib += inserts;
            // Positions are tracked here so Flush can stamp each edit with where it starts.
        }

        private void Equal()
        {
            Flush();
            edits.Add(new LineEdit(' ', ia, ib));
            ia++;
            ib++;
        }

        private void Flush()
        {
            var a0 = ia - pendingDeletes;
            var b0 = ib - pendingInserts;
            for (var i = 0; i < pendingDeletes; i++)
            {
                edits.Add(new LineEdit('-', a0 + i, b0));
            }

            for (var j = 0; j < pendingInserts; j++)
            {
                edits.Add(new LineEdit('+', a0 + pendingDeletes, b0 + j));
            }

            pendingDeletes = 0;
            pendingInserts = 0;
        }

        // Forward and reverse searches advance one edit at a time until they meet; the meeting diagonal's snake splits the problem
        // into two halves with about half the edits each. Returns null when the budget is spent.
        private Snake? MiddleSnake(int aLo, int aHi, int bLo, int bHi)
        {
            var n = aHi - aLo;
            var m = bHi - bLo;
            var delta = n - m;
            var odd = (delta & 1) != 0;
            var limit = Math.Min((n + m + 1) / 2, MaxEditsPerSearch);
            var offset = limit + 1;
            var vf = new int[(2 * limit) + 3];
            var vb = new int[(2 * limit) + 3];

            for (var d = 0; d <= limit; d++)
            {
                for (var k = -d; k <= d; k += 2)
                {
                    var x = k == -d || (k != d && vf[offset + k - 1] < vf[offset + k + 1]) ? vf[offset + k + 1] : vf[offset + k - 1] + 1;
                    var y = x - k;
                    var x0 = x;
                    var y0 = y;
                    while (x < n && y < m && a[aLo + x] == b[bLo + y])
                    {
                        x++;
                        y++;
                    }

                    vf[offset + k] = x;
                    var back = delta - k;
                    if (odd && back >= -(d - 1) && back <= d - 1 && x + vb[offset + back] >= n)
                    {
                        return new Snake(x0, y0, x, y);
                    }
                }

                for (var k = -d; k <= d; k += 2)
                {
                    var x = k == -d || (k != d && vb[offset + k - 1] < vb[offset + k + 1]) ? vb[offset + k + 1] : vb[offset + k - 1] + 1;
                    var y = x - k;
                    var x0 = x;
                    var y0 = y;
                    while (x < n && y < m && a[aHi - 1 - x] == b[bHi - 1 - y])
                    {
                        x++;
                        y++;
                    }

                    vb[offset + k] = x;
                    var forward = delta - k;
                    if (!odd && forward >= -d && forward <= d && x + vf[offset + forward] >= n)
                    {
                        return new Snake(n - x, m - y, n - x0, m - y0);
                    }
                }
            }

            return null;
        }
    }

    private readonly record struct Snake(int X0, int Y0, int X1, int Y1);
}
