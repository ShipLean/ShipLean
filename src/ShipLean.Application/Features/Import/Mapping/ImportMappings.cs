using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Features.Import.Mapping;

// IImportMappings over the Jira client. A previous job's mapping (IImportJobs.GetMappingAsync) supplies the choices for every row it covered;
// the rows it did not cover are new, with the defaults. The states the statuses land in are read from the previous job's project when it has one,
// otherwise they are the Agile defaults a new project starts with.
public sealed class ImportMappings(
    IJiraConnections connections, IJiraClient jira, IImportJobs jobs, IStateCatalog states, IUserDirectory users) : IImportMappings
{
    private const string NoPointsField = "";

    public async Task<ImportMappingDto> SuggestAsync(Guid connectionId, string jiraProjectKey, Guid? previousJobId, CancellationToken cancellationToken = default)
    {
        var credentials = await connections.GetCredentialsAsync(connectionId, cancellationToken);
        var previous = previousJobId is { } jobId ? await jobs.GetMappingAsync(jobId, cancellationToken) : null;
        IReadOnlyList<StateDto>? projectStates = null;
        if (previousJobId is { } id && (await jobs.GetAsync(id, cancellationToken)).ProjectId is { } projectId)
        {
            projectStates = await states.ListAsync(projectId, cancellationToken);
        }

        var issueTypes = await jira.GetIssueTypesAsync(credentials, jiraProjectKey, cancellationToken);
        var jiraStatuses = await jira.GetStatusesAsync(credentials, jiraProjectKey, cancellationToken);
        var jiraFields = await jira.GetFieldsAsync(credentials, cancellationToken);
        var candidates = jiraFields.Where(f => f.Custom && f.SchemaType == "number").ToList();
        var survey = await JiraSurvey.ReadAsync(
            jira, credentials, jiraProjectKey, [.. candidates.Select(f => f.Id)], previous?.Fields.IncludeComments ?? true, cancellationToken);

        var typeRows = TypeRows(issueTypes, survey, previous, out var typeWarnings);
        var statusRows = StatusRows(issueTypes, jiraStatuses, survey, typeRows, previous, projectStates, out var statusWarnings);
        var userRows = await UserRowsAsync(survey, previous, cancellationToken);
        var fields = FieldsFor(candidates, survey, previous, out var fieldWarnings);

        var warnings = typeWarnings.Concat(statusWarnings).Concat(UserWarnings(userRows)).Concat(fieldWarnings).ToList();
        var included = typeRows.Where(t => t.Target != ImportTypeTarget.Skip).Sum(t => t.Count);
        var totals = new ImportTotalsDto(
            included,
            fields.IncludeComments ? survey.Comments : 0,
            fields.IncludeAttachments ? survey.Attachments : 0,
            fields.IncludeChangelog ? survey.Revisions : 0,
            survey.SprintCount,
            userRows.Count);
        return new ImportMappingDto(typeRows, statusRows, userRows, fields, totals, warnings);
    }

    private static List<IssueTypeMappingDto> TypeRows(
        IReadOnlyList<JiraIssueType> issueTypes, JiraSurvey survey, ImportMappingDto? previous, out List<ImportWarningDto> warnings)
    {
        warnings = [];
        var rows = new List<IssueTypeMappingDto>();
        foreach (var type in issueTypes)
        {
            var count = survey.TypeCount(type.Name);
            var kept = previous?.IssueTypes.FirstOrDefault(t => t.JiraType == type.Name);
            var (target, note, recognised) = TypeDefaults.For(type);
            if (kept is not null)
            {
                rows.Add(new IssueTypeMappingDto(type.Name, count, kept.Target, kept.Note, false));
            }
            else
            {
                rows.Add(new IssueTypeMappingDto(type.Name, count, target, note, previous is not null));
                if (!recognised)
                {
                    warnings.Add(new ImportWarningDto(ImportWarningSeverity.Warning, ImportMappingSection.Types, $"Jira type {type.Name} has no ShipLean equivalent and will be imported as a User Story: change it if that is wrong."));
                }
            }
        }

        foreach (var skipped in rows.Where(r => r.Target == ImportTypeTarget.Skip && r.Count > 0))
        {
            warnings.Add(new ImportWarningDto(ImportWarningSeverity.Warning, ImportMappingSection.Types, $"{skipped.Count} {skipped.JiraType} issues will not be imported."));
        }

        return rows;
    }

    private static List<StatusMappingDto> StatusRows(
        IReadOnlyList<JiraIssueType> issueTypes, IReadOnlyList<JiraStatus> jiraStatuses, JiraSurvey survey, List<IssueTypeMappingDto> typeRows,
        ImportMappingDto? previous, IReadOnlyList<StateDto>? projectStates, out List<ImportWarningDto> warnings)
    {
        warnings = [];
        var rows = new List<StatusMappingDto>();
        foreach (var typeName in issueTypes.Select(t => t.Name))
        {
            var statuses = jiraStatuses.Where(s => s.IssueTypes.Count == 0 || s.IssueTypes.Contains(typeName)).ToList();
            var target = TargetTypes.WorkItemType(typeRows.Single(t => t.JiraType == typeName).Target);
            var existing = target is { } t ? StatusSuggestions.ExistingFor(t, projectStates) : [];
            var suggested = StatusSuggestions.Suggest(statuses, existing).ToDictionary(s => s.Name);
            foreach (var status in statuses)
            {
                var count = survey.StatusCount(typeName, status.Name);
                var kept = previous?.Statuses.FirstOrDefault(s => s.JiraType == typeName && s.JiraStatus == status.Name);
                var (_, targetState, category, creates) = suggested[status.Name];
                if (target is null)
                {
                    rows.Add(new StatusMappingDto(typeName, status.Name, status.Category, count, status.Name, TargetTypes.CategoryOf(status.Category), false, kept is null && previous is not null));
                    continue;
                }

                if (kept is not null)
                {
                    (targetState, category) = (kept.TargetState, kept.TargetCategory);
                    creates = !StatusSuggestions.Exists(existing, targetState);
                }

                rows.Add(new StatusMappingDto(typeName, status.Name, status.Category, count, targetState, category, creates, kept is null && previous is not null));
                if (creates)
                {
                    warnings.Add(new ImportWarningDto(
                        ImportWarningSeverity.Warning, ImportMappingSection.Statuses,
                        $"{typeName} status {status.Name} has no matching state: a new {target} state {targetState} ({category}, from Jira's category) will be created and given a board column."));
                }
            }
        }

        return rows;
    }

    private async Task<List<UserMappingDto>> UserRowsAsync(JiraSurvey survey, ImportMappingDto? previous, CancellationToken cancellationToken)
    {
        var rows = new List<UserMappingDto>();
        foreach (var (user, references) in survey.Users.OrderByDescending(u => u.References).ThenBy(u => u.User.DisplayName, StringComparer.Ordinal))
        {
            var kept = previous?.Users.FirstOrDefault(u => u.JiraAccountId == user.AccountId);
            if (kept is not null)
            {
                rows.Add(new UserMappingDto(user.AccountId, user.DisplayName, user.Email, references, kept.Resolution, kept.User, user.IsBot, false));
                continue;
            }

            var isNew = previous is not null;
            if (user.IsBot || string.IsNullOrWhiteSpace(user.Email))
            {
                rows.Add(new UserMappingDto(user.AccountId, user.DisplayName, user.Email, references, UserResolution.JiraImportUser, null, user.IsBot, isNew));
            }
            else if (await users.FindByEmailAsync(user.Email, cancellationToken) is { } found)
            {
                rows.Add(new UserMappingDto(user.AccountId, user.DisplayName, user.Email, references, UserResolution.Matched, new UserRef(found.Id, found.Name, found.Initials, found.AvatarColour, found.PhotoUrl), false, isNew));
            }
            else
            {
                rows.Add(new UserMappingDto(user.AccountId, user.DisplayName, user.Email, references, UserResolution.Create, null, false, isNew));
            }
        }

        return rows;
    }

    private static IEnumerable<ImportWarningDto> UserWarnings(List<UserMappingDto> rows)
    {
        var created = rows.Count(r => r.Resolution == UserResolution.Create);
        if (created > 0)
        {
            yield return new ImportWarningDto(ImportWarningSeverity.Info, ImportMappingSection.Users, $"{created} people will be created without a password; an admin issues each a one-time password.");
        }

        foreach (var hidden in rows.Where(r => !r.IsBot && string.IsNullOrWhiteSpace(r.Email) && r.Resolution == UserResolution.JiraImportUser))
        {
            yield return new ImportWarningDto(ImportWarningSeverity.Warning, ImportMappingSection.Users, $"{hidden.Name} hides their email, so they cannot be matched: their work is attributed to the Jira import user.");
        }

        var bots = rows.Count(r => r.IsBot);
        if (bots > 0)
        {
            yield return new ImportWarningDto(ImportWarningSeverity.Info, ImportMappingSection.Users, $"{bots} automation accounts are attributed to the Jira import user.");
        }
    }

    private static ImportFieldsDto FieldsFor(List<JiraField> candidates, JiraSurvey survey, ImportMappingDto? previous, out List<ImportWarningDto> warnings)
    {
        warnings = [];
        var rows = candidates.Select(f => new StoryPointsFieldDto(f.Id, f.Name, survey.Populated(f.Id), survey.Issues - survey.Populated(f.Id))).ToList();
        var chosen = previous?.Fields.StoryPointsFieldId
            ?? candidates.FirstOrDefault(f => f.Name.Contains("story point", StringComparison.OrdinalIgnoreCase))?.Id
            ?? rows.OrderByDescending(r => r.Populated).FirstOrDefault()?.FieldId
            ?? NoPointsField;
        if (chosen == NoPointsField)
        {
            warnings.Add(new ImportWarningDto(ImportWarningSeverity.Warning, ImportMappingSection.Fields, "No number field was found for story points: items will be imported without points."));
        }
        else if (rows.FirstOrDefault(r => r.FieldId == chosen) is { Missing: > 0 } field)
        {
            warnings.Add(new ImportWarningDto(ImportWarningSeverity.Info, ImportMappingSection.Fields, $"{field.Missing} issues have no value in {field.Name}."));
        }

        var fields = previous?.Fields;
        return new ImportFieldsDto(
            chosen, rows,
            fields?.IncludeComponents ?? true, fields?.IncludeComments ?? true, fields?.IncludeAttachments ?? true, fields?.IncludeChangelog ?? true);
    }
}
