using System.Text.RegularExpressions;
using ShipLean.Application.Fakes.Sprints;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Fakes.Projects;

public sealed partial class InMemoryProjectFactory(InMemoryPlanning world, ICurrentUser currentUser) : IProjectFactory
{
    public Task<ProjectDto> CreateAsync(ProjectCreate create, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            if (string.IsNullOrWhiteSpace(create.Name) || !KeyPattern().IsMatch(create.Key))
            {
                throw ApiException.Validation("A project needs a name and a key of two to four capital letters.");
            }
            if (create.WorkingDays.Count == 0 || create.WorkingDays.Any(d => d is < 1 or > 7))
            {
                throw ApiException.Validation("Pick at least one working day.");
            }
            if (world.Projects.Any(p => p.Key == create.Key))
            {
                throw ApiException.Conflict("That key is already taken.");
            }
            var members = (create.MemberEmails ?? []).Select(email => world.Users.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)).User
                ?? throw ApiException.NotFound($"No user has the email {email}.")).ToList();
            var source = create.CopyFromProjectId is { } from ? world.Project(from) : null;
            var id = world.AddProject(create.Key, create.Name.Trim(), withDefaultStates: source is null);
            var project = world.Project(id);
            // A copy keeps the source project's working days and ignores the ones in the request.
            project.WorkingDays = [.. (source?.WorkingDays ?? [.. create.WorkingDays]).Order()];
            project.VelocityWindow = source?.VelocityWindow ?? project.VelocityWindow;
            project.Members[currentUser.Id] = ProjectRole.Admin;
            foreach (var member in members.Where(m => m.Id != currentUser.Id))
            {
                project.Members[member.Id] = ProjectRole.Member;
            }
            if (source is not null)
            {
                CopyStates(source.Id, id);
            }
            if (create.FirstSprint is { } first)
            {
                InMemorySprintStore.Open(world, id, first);
            }
            return Task.FromResult(new ProjectDto(
                id, project.Key, project.Name, project.Description, ProjectRole.Admin, project.Members.Count, project.WorkingDays,
                project.VelocityWindow, project.VelocityOverride, project.NextItemNumber));
        }
    }

    public Task<KeyCheckDto> CheckKeyAsync(string key, string name, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            key ??= string.Empty;
            if (!KeyPattern().IsMatch(key))
            {
                return Task.FromResult(new KeyCheckDto(key, false, KeyCheckReason.Invalid, []));
            }
            var existing = world.Projects.FirstOrDefault(p => p.Key == key);
            if (existing is null)
            {
                return Task.FromResult(new KeyCheckDto(key, true, null, []));
            }
            var reason = existing.Members.ContainsKey(currentUser.Id) ? KeyCheckReason.Taken : KeyCheckReason.TakenByAnotherProject;
            var alternatives = Enumerable.Range('A', 26).Select(c => key[..^1] + (char)c).Where(k => world.Projects.All(p => p.Key != k)).Take(3).ToList();
            return Task.FromResult(new KeyCheckDto(key, false, reason, alternatives));
        }
    }

    private void CopyStates(Guid from, Guid to)
    {
        foreach (var state in world.States.Where(s => s.ProjectId == from && !s.Archived).ToList())
        {
            world.States.Add(new InMemoryPlanning.StateRow { Id = Guid.NewGuid(), ProjectId = to, Type = state.Type, Name = state.Name, Category = state.Category, Order = state.Order });
        }
    }

    [GeneratedRegex("^[A-Z]{2,4}$")]
    private static partial Regex KeyPattern();
}
