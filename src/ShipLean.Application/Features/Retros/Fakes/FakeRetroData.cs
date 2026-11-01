using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Fakes;

// The fixed values every Fake* seam in this folder returns.
// TODO: delete this file with the last fake that uses it.
internal static class FakeRetroData
{
    public static readonly SprintRef Sprint = new(new Guid("00000004-0000-4000-8001-000000000024"), "Sprint 24", SprintStatus.Current);

    public static readonly RetroThemeRefDto Theme = new(new Guid("00000010-0000-4000-8001-000000000002"), "Screenshot baselines need CI", RetroTone.Bad);

    public static readonly RetroItemRefDto Item = new(
        new Guid("00000005-0000-4000-8001-000000000141"), "SHIP-141", new TypeRef(WorkItemTypeId.Story, "User Story"), "Pay with saved card");

    public static readonly RetroActionRefDto Action = new(
        new Guid("00000005-0000-4000-8001-000000000188"), "SHIP-188", "Common brief: screenshot baselines come from the CI report",
        new StateRef(new Guid("00000003-0000-4000-8001-000000000601"), "New", StateCategory.Proposed), null, 1m);

    public static readonly RetroAuthorDto Author = new(
        new UserRef(new Guid("00000001-0000-4000-8000-000000000002"), "Priya Shah", "PS", "#2f7fd8", null), "Claude Code · lane A", RetroRole.SubAgent);

    public static readonly RetroFailureLogDto FailureLog = new(["The lock was held for 300 s."], ["CI baselines match local ones."], ["Two builds retried."]);

    public static RetroPointDto Point() => new(
        new Guid("00000011-0000-4000-8001-000000000003"), new Guid("00000012-0000-4000-8001-000000000301"), RetroCategory.Improve,
        "Say in the brief which baselines must come from CI.", null, Theme, RetroThemeSetBy.Auto, Author, Item, Sprint, false, true, Action, false);

    public static RetroCardDto Card() => new(
        new Guid("00000012-0000-4000-8001-000000000301"), Item, Sprint, Author, false, new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero), "20 Oct", [], [Point()], FailureLog);
}
