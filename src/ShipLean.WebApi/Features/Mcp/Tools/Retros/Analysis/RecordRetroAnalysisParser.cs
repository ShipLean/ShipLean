using System.Text;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// Turns the arguments of record_retro_analysis into the request the writer takes, refusing what is wrong with the request itself, each naming its field. What needs the database
// (a point in scope and undecided, a pattern of this project) is the writer's to refuse. The schema marks most fields required, but the generator lists every string as nullable.
internal static class RecordRetroAnalysisParser
{
    public const int MaxReportBytes = 200 * 1024;

    public static RecordRetroAnalysisRequest Parse(RecordRetroAnalysisInput input, DecideScope scope, IReadOnlyList<Guid?> existing)
    {
        var embeddingModel = Required(input.EmbeddingModel, "embedding_model");
        var report = Required(input.ReportMarkdown, "report_markdown");
        if (Encoding.UTF8.GetByteCount(report) > MaxReportBytes)
        {
            throw ApiException.Validation("report_markdown is over 200 KB.", "report_markdown");
        }

        var points = Points(input.Points ?? throw ApiException.Validation("Pass points: every point you looked at. Or upload them with create_payload_upload and pass payload.", "points"));
        var patterns = Patterns(input.Patterns ?? [], points);
        var fixes = Fixes(input.Fixes ?? [], patterns, existing);
        for (var index = 0; index < points.Count; index++)
        {
            if (points[index].Outcome == RetroPointOutcome.Clustered && patterns.Count(pattern => pattern.PointIds.Contains(points[index].Id)) != 1)
            {
                throw ApiException.Validation("A clustered point is in exactly one pattern.", $"points[{index}].outcome");
            }
        }

        return new RecordRetroAnalysisRequest(scope, input.ExcludeCovered ?? true, embeddingModel, string.IsNullOrWhiteSpace(input.RewriteModel) ? null : input.RewriteModel.Trim(), points, patterns, fixes, report);
    }

    private static List<RetroAnalysisPoint> Points(IReadOnlyList<RetroAnalysisPointInput> inputs)
    {
        var seen = new HashSet<Guid>();
        var points = new List<RetroAnalysisPoint>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i] ?? throw ApiException.Validation($"points[{i}] is required.", $"points[{i}]");
            var outcome = Choice<RetroPointOutcome>(input.Outcome, $"points[{i}].outcome", "noise, folded, oneOff, clustered or unclustered");
            if (!seen.Add(input.Id))
            {
                throw ApiException.Validation("A point appears once.", $"points[{i}].id");
            }

            if (outcome == RetroPointOutcome.Folded && input.FoldedInto is null)
            {
                throw ApiException.Validation("A folded point names the point it repeats in folded_into.", $"points[{i}].folded_into");
            }

            if (outcome is RetroPointOutcome.Clustered or RetroPointOutcome.Unclustered or RetroPointOutcome.OneOff && string.IsNullOrWhiteSpace(input.Generic))
            {
                throw ApiException.Validation("A clustered, unclustered or oneOff point carries its generic rewrite.", $"points[{i}].generic");
            }

            points.Add(new RetroAnalysisPoint(input.Id, outcome, input.FoldedInto, string.IsNullOrWhiteSpace(input.Generic) ? null : input.Generic));
        }

        return points;
    }

    private static List<RetroAnalysisPattern> Patterns(IReadOnlyList<RetroAnalysisPatternInput> inputs, List<RetroAnalysisPoint> points)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var patterns = new List<RetroAnalysisPattern>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i] ?? throw ApiException.Validation($"patterns[{i}] is required.", $"patterns[{i}]");
            var reference = Required(input.Ref, $"patterns[{i}].ref");
            if (!refs.Add(reference))
            {
                throw ApiException.Validation($"The ref \"{reference}\" is used twice.", $"patterns[{i}].ref");
            }

            var kind = Choice<RetroPatternKind>(input.Kind, $"patterns[{i}].kind", "problem or wentWell");
            if (input.PointIds is not { Count: > 0 })
            {
                throw ApiException.Validation("Name at least one point.", $"patterns[{i}].point_ids");
            }

            foreach (var id in input.PointIds)
            {
                if (points.SingleOrDefault(point => point.Id == id)?.Outcome != RetroPointOutcome.Clustered)
                {
                    throw ApiException.Validation($"{id} is not a point of this run with outcome clustered.", $"patterns[{i}].point_ids");
                }

                if (patterns.Any(earlier => earlier.PointIds.Contains(id)) || input.PointIds.Count(other => other == id) > 1)
                {
                    throw ApiException.Validation($"{id} is already in another pattern.", $"patterns[{i}].point_ids");
                }
            }

            patterns.Add(new RetroAnalysisPattern(
                reference, kind, Required(input.Title, $"patterns[{i}].title"), Required(input.Lesson, $"patterns[{i}].lesson"), input.Asks ?? [], input.PointIds, input.Centroid, input.Continues));
        }

        return patterns;
    }

    private static List<RetroAnalysisFix> Fixes(IReadOnlyList<RetroAnalysisFixInput> inputs, List<RetroAnalysisPattern> patterns, IReadOnlyList<Guid?> existing)
    {
        var fixes = new List<RetroAnalysisFix>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i] ?? throw ApiException.Validation($"fixes[{i}] is required.", $"fixes[{i}]");
            var reference = Required(input.PatternRef, $"fixes[{i}].pattern_ref");
            if (patterns.SingleOrDefault(pattern => pattern.Ref == reference)?.Kind != RetroPatternKind.Problem)
            {
                throw ApiException.Validation($"\"{reference}\" is not a problem pattern of this run.", $"fixes[{i}].pattern_ref");
            }

            var fixType = Choice<RetroFixType>(input.FixType, $"fixes[{i}].fix_type", "fix, structure, analyzer, test, skill, guide, product or other");
            var enforcement = Choice<RetroEnforcement>(input.Enforcement, $"fixes[{i}].enforcement", "notCaught, partly, fully or discarded");
            if (enforcement is RetroEnforcement.Partly or RetroEnforcement.Fully && string.IsNullOrWhiteSpace(input.EnforcedBy))
            {
                throw ApiException.Validation("Say what already does it in enforced_by.", $"fixes[{i}].enforced_by");
            }

            fixes.Add(new RetroAnalysisFix(
                reference, Required(input.Title, $"fixes[{i}].title"), fixType, enforcement, string.IsNullOrWhiteSpace(input.EnforcedBy) ? null : input.EnforcedBy, Required(input.Reason, $"fixes[{i}].reason"), existing[i]));
        }

        return fixes;
    }

    private static string Required(string? value, string field)
        => string.IsNullOrWhiteSpace(value) ? throw ApiException.Validation($"{field} is required.", field) : value.Trim();

    private static T Choice<T>(string? value, string field, string choices)
        where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : throw ApiException.Validation($"{field} is {choices}.", field);
}
