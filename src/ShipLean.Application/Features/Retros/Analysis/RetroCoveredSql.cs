namespace ShipLean.Application.Features.Retros.Analysis;

// The covered rule of C1 and C2 (D22) as SQL: a point is covered when a live analysis of the same project recorded it as noise, folded or oneOff.
// Unclustered and clustered points are not covered, because a later run may still find their pattern. Used by the export and by the analysis writer, so the text is built once.
public static class RetroCoveredSql
{
    // A boolean expression: the point (alias) is covered in the project the parameter names.
    public static string Covered(string point, string projectIdParameter = "@projectId")
        => $"""
            EXISTS (SELECT 1 FROM retro_analysis_point rcp
                    JOIN retro_analysis rca ON rca.id = rcp.analysis_id
                    WHERE rcp.point_id = {point}.id AND rca.project_id = {projectIdParameter} AND rca.archived_at IS NULL
                      AND rcp.outcome IN ('noise', 'folded', 'oneOff'))
            """;
}
