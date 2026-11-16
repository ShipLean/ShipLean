using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Reads.Page;

// Turns git unified diff text into one DiffFileDto per file. Patch is the file's hunks (from the first @@ line on; empty for a binary
// file or a pure rename) and PatchId the lower-case hex sha-1 of that text. OpenComments is left at 0 for the caller to fill.
public static partial class GitDiffSplitter
{
    public static IReadOnlyList<DiffFileDto> Split(string unifiedDiff)
    {
        var starts = FileStart().Matches(unifiedDiff).Select(m => m.Index).ToList();
        var files = new List<DiffFileDto>(starts.Count);
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1] : unifiedDiff.Length;
            files.Add(File(unifiedDiff[starts[i]..end]));
        }

        return files;
    }

    private static DiffFileDto File(string block)
    {
        var hunkAt = block.StartsWith("@@", StringComparison.Ordinal) ? 0 : block.IndexOf("\n@@", StringComparison.Ordinal) + 1;
        var header = hunkAt > 0 ? block[..hunkAt] : block;
        var patch = hunkAt > 0 ? block[hunkAt..] : string.Empty;
        string? renameFrom = null;
        string? renameTo = null;
        string? oldName = null;
        string? newName = null;
        var status = "modified";
        foreach (var line in header.Split('\n'))
        {
            if (line.StartsWith("new file mode", StringComparison.Ordinal))
            {
                status = "added";
            }
            else if (line.StartsWith("deleted file mode", StringComparison.Ordinal))
            {
                status = "deleted";
            }
            else if (line.StartsWith("rename from ", StringComparison.Ordinal))
            {
                renameFrom = line["rename from ".Length..];
            }
            else if (line.StartsWith("rename to ", StringComparison.Ordinal))
            {
                renameTo = line["rename to ".Length..];
            }
            else if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                oldName = Name(line[4..], "a/");
            }
            else if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                newName = Name(line[4..], "b/");
            }
        }

        if (renameFrom is not null && renameTo is not null)
        {
            status = "renamed";
        }

        var path = renameTo ?? newName ?? oldName ?? FromGitLine(header);
        var oldPath = status == "renamed" ? renameFrom : null;
        var additions = 0;
        var deletions = 0;
        foreach (var line in patch.Split('\n'))
        {
            if (line.StartsWith('+'))
            {
                additions++;
            }
            else if (line.StartsWith('-'))
            {
                deletions++;
            }
        }

        return new DiffFileDto(path, oldPath, status, additions, deletions, patch, PatchId(patch), 0);
    }

    // The contract's PatchId is a sha-1 of the file's patch text; it is a change fingerprint, not a security hash.
    private static string PatchId(string patch)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(Encoding.UTF8.GetBytes(patch));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    // "a/src/x.ts" -> "src/x.ts"; /dev/null -> null. A trailing tab (git adds one after a name with spaces) is dropped.
    private static string? Name(string raw, string prefix)
    {
        var name = raw.TrimEnd('\t');
        if (name == "/dev/null")
        {
            return null;
        }

        return name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
    }

    // A binary or mode-only file has no ---/+++ lines: its name sits in "diff --git a/X b/X", with the same X on both sides.
    private static string FromGitLine(string header)
    {
        var line = header.Split('\n')[0]["diff --git ".Length..];
        var length = (line.Length - 5) / 2;
        return length > 0 && line.Length == (2 * length) + 5 ? line[2..(2 + length)] : line;
    }

    [GeneratedRegex("^diff --git ", RegexOptions.Multiline)]
    private static partial Regex FileStart();
}
