namespace ShipLean.TestSupport;

// Every id the seed (db/seed) writes, by name. Format KKKKKKKK-0000-4000-80PP-NNNNNNNNNNNN (see the pinned shapes README):
// kind 1 user, 2 project, 3 state, 4 sprint, 5 work item, 6 import job, 7 board, 8 sprint capacity, 9 project member,
// a Jira connection. PP = project number, N = number. Joel is the exception (0199f000-…-000000000001). Pinned to
// src/ShipLean.Client/src/seedIds.ts by SeedIds_ShouldMatchSeedIdsTs. Revisions have no ids here (keyed work item + rev).
public static class SeedIds
{
    public static class Users
    {
        public static readonly Guid Joel = new("0199f000-0000-7000-8000-000000000001");
        public static readonly Guid Priya = new("00000001-0000-4000-8000-000000000002");
        public static readonly Guid Tom = new("00000001-0000-4000-8000-000000000003");
        public static readonly Guid Mei = new("00000001-0000-4000-8000-000000000004");
        public static readonly Guid Sam = new("00000001-0000-4000-8000-000000000005");
        public static readonly Guid Ana = new("00000001-0000-4000-8000-000000000006");
        public static readonly Guid Ravi = new("00000001-0000-4000-8000-000000000007");
        public static readonly Guid Dana = new("00000001-0000-4000-8000-000000000008");
        public static readonly Guid Lee = new("00000001-0000-4000-8000-000000000009");
        public static readonly Guid Jo = new("00000001-0000-4000-8000-000000000010");
        public static readonly Guid Kai = new("00000001-0000-4000-8000-000000000011");
        public static readonly Guid JiraImport = new("00000001-0000-4000-8000-000000000012");
    }

    public static class Passwords
    {
        // Plaintext dev passwords; the seeded hashes verify against these. Ana must change hers at first sign-in.
        public const string Joel = "joel-dev-password";
        public const string Priya = "priya-dev-password";
        public const string Tom = "tom-dev-password";
        public const string Mei = "mei-dev-password";
        public const string Sam = "sam-dev-password";
        public const string Ana = "ana-dev-password";
        public const string Ravi = "ravi-dev-password";
        public const string Lee = "lee-dev-password";
        public const string Jo = "jo-dev-password";
        public const string Kai = "kai-dev-password";
    }

    public static class Projects
    {
        public static readonly Guid Ship = new("00000002-0000-4000-8000-000000000001");
        public static readonly Guid Pay = new("00000002-0000-4000-8000-000000000002");
        public static readonly Guid Mob = new("00000002-0000-4000-8000-000000000003");
        public static readonly Guid Data = new("00000002-0000-4000-8000-000000000004");
        public static readonly Guid Ops = new("00000002-0000-4000-8000-000000000005");
        public static readonly Guid Part = new("00000002-0000-4000-8000-000000000006");
        public static readonly Guid Arch = new("00000002-0000-4000-8000-000000000007");
        public static readonly Guid Empt = new("00000002-0000-4000-8000-000000000008");
    }

    public static class States
    {
        public static class Ship
        {
            public static readonly Guid EpicNew = new("00000003-0000-4000-8001-000000000101");
            public static readonly Guid EpicActive = new("00000003-0000-4000-8001-000000000102");
            public static readonly Guid EpicResolved = new("00000003-0000-4000-8001-000000000103");
            public static readonly Guid EpicClosed = new("00000003-0000-4000-8001-000000000104");
            public static readonly Guid EpicRemoved = new("00000003-0000-4000-8001-000000000105");
            public static readonly Guid FeatureNew = new("00000003-0000-4000-8001-000000000201");
            public static readonly Guid FeatureActive = new("00000003-0000-4000-8001-000000000202");
            public static readonly Guid FeatureResolved = new("00000003-0000-4000-8001-000000000203");
            public static readonly Guid FeatureClosed = new("00000003-0000-4000-8001-000000000204");
            public static readonly Guid FeatureRemoved = new("00000003-0000-4000-8001-000000000205");
            public static readonly Guid StoryNew = new("00000003-0000-4000-8001-000000000301");
            public static readonly Guid StoryActive = new("00000003-0000-4000-8001-000000000302");
            public static readonly Guid StoryResolved = new("00000003-0000-4000-8001-000000000303");
            public static readonly Guid StoryClosed = new("00000003-0000-4000-8001-000000000304");
            public static readonly Guid StoryRemoved = new("00000003-0000-4000-8001-000000000305");
            public static readonly Guid BugNew = new("00000003-0000-4000-8001-000000000401");
            public static readonly Guid BugTriaged = new("00000003-0000-4000-8001-000000000402");
            public static readonly Guid BugActive = new("00000003-0000-4000-8001-000000000403");
            public static readonly Guid BugResolved = new("00000003-0000-4000-8001-000000000404");
            public static readonly Guid BugClosed = new("00000003-0000-4000-8001-000000000405");
            public static readonly Guid BugRemoved = new("00000003-0000-4000-8001-000000000406");
            public static readonly Guid TaskNew = new("00000003-0000-4000-8001-000000000501");
            public static readonly Guid TaskActive = new("00000003-0000-4000-8001-000000000502");
            public static readonly Guid TaskInReview = new("00000003-0000-4000-8001-000000000503");
            public static readonly Guid TaskClosed = new("00000003-0000-4000-8001-000000000504");
            public static readonly Guid TaskRemoved = new("00000003-0000-4000-8001-000000000505");
        }

        public static class Pay
        {
            public static readonly Guid EpicNew = new("00000003-0000-4000-8002-000000000101");
            public static readonly Guid EpicActive = new("00000003-0000-4000-8002-000000000102");
            public static readonly Guid EpicResolved = new("00000003-0000-4000-8002-000000000103");
            public static readonly Guid EpicClosed = new("00000003-0000-4000-8002-000000000104");
            public static readonly Guid EpicRemoved = new("00000003-0000-4000-8002-000000000105");
            public static readonly Guid FeatureNew = new("00000003-0000-4000-8002-000000000201");
            public static readonly Guid FeatureActive = new("00000003-0000-4000-8002-000000000202");
            public static readonly Guid FeatureResolved = new("00000003-0000-4000-8002-000000000203");
            public static readonly Guid FeatureClosed = new("00000003-0000-4000-8002-000000000204");
            public static readonly Guid FeatureRemoved = new("00000003-0000-4000-8002-000000000205");
            public static readonly Guid StoryNew = new("00000003-0000-4000-8002-000000000301");
            public static readonly Guid StoryActive = new("00000003-0000-4000-8002-000000000302");
            public static readonly Guid StoryResolved = new("00000003-0000-4000-8002-000000000303");
            public static readonly Guid StoryClosed = new("00000003-0000-4000-8002-000000000304");
            public static readonly Guid StoryRemoved = new("00000003-0000-4000-8002-000000000305");
            public static readonly Guid BugNew = new("00000003-0000-4000-8002-000000000401");
            public static readonly Guid BugActive = new("00000003-0000-4000-8002-000000000402");
            public static readonly Guid BugResolved = new("00000003-0000-4000-8002-000000000403");
            public static readonly Guid BugClosed = new("00000003-0000-4000-8002-000000000404");
            public static readonly Guid BugRemoved = new("00000003-0000-4000-8002-000000000405");
            public static readonly Guid TaskNew = new("00000003-0000-4000-8002-000000000501");
            public static readonly Guid TaskActive = new("00000003-0000-4000-8002-000000000502");
            public static readonly Guid TaskResolved = new("00000003-0000-4000-8002-000000000503");
            public static readonly Guid TaskClosed = new("00000003-0000-4000-8002-000000000504");
            public static readonly Guid TaskRemoved = new("00000003-0000-4000-8002-000000000505");
        }

        public static class Mob
        {
            public static readonly Guid EpicNew = new("00000003-0000-4000-8003-000000000101");
            public static readonly Guid EpicActive = new("00000003-0000-4000-8003-000000000102");
            public static readonly Guid EpicResolved = new("00000003-0000-4000-8003-000000000103");
            public static readonly Guid EpicClosed = new("00000003-0000-4000-8003-000000000104");
            public static readonly Guid EpicRemoved = new("00000003-0000-4000-8003-000000000105");
            public static readonly Guid FeatureNew = new("00000003-0000-4000-8003-000000000201");
            public static readonly Guid FeatureActive = new("00000003-0000-4000-8003-000000000202");
            public static readonly Guid FeatureResolved = new("00000003-0000-4000-8003-000000000203");
            public static readonly Guid FeatureClosed = new("00000003-0000-4000-8003-000000000204");
            public static readonly Guid FeatureRemoved = new("00000003-0000-4000-8003-000000000205");
            public static readonly Guid StoryNew = new("00000003-0000-4000-8003-000000000301");
            public static readonly Guid StoryActive = new("00000003-0000-4000-8003-000000000302");
            public static readonly Guid StoryResolved = new("00000003-0000-4000-8003-000000000303");
            public static readonly Guid StoryClosed = new("00000003-0000-4000-8003-000000000304");
            public static readonly Guid StoryRemoved = new("00000003-0000-4000-8003-000000000305");
            public static readonly Guid BugNew = new("00000003-0000-4000-8003-000000000401");
            public static readonly Guid BugActive = new("00000003-0000-4000-8003-000000000402");
            public static readonly Guid BugResolved = new("00000003-0000-4000-8003-000000000403");
            public static readonly Guid BugClosed = new("00000003-0000-4000-8003-000000000404");
            public static readonly Guid BugRemoved = new("00000003-0000-4000-8003-000000000405");
            public static readonly Guid TaskNew = new("00000003-0000-4000-8003-000000000501");
            public static readonly Guid TaskActive = new("00000003-0000-4000-8003-000000000502");
            public static readonly Guid TaskResolved = new("00000003-0000-4000-8003-000000000503");
            public static readonly Guid TaskClosed = new("00000003-0000-4000-8003-000000000504");
            public static readonly Guid TaskRemoved = new("00000003-0000-4000-8003-000000000505");
        }

        public static class Data
        {
            public static readonly Guid EpicNew = new("00000003-0000-4000-8004-000000000101");
            public static readonly Guid EpicActive = new("00000003-0000-4000-8004-000000000102");
            public static readonly Guid EpicResolved = new("00000003-0000-4000-8004-000000000103");
            public static readonly Guid EpicClosed = new("00000003-0000-4000-8004-000000000104");
            public static readonly Guid EpicRemoved = new("00000003-0000-4000-8004-000000000105");
            public static readonly Guid FeatureNew = new("00000003-0000-4000-8004-000000000201");
            public static readonly Guid FeatureActive = new("00000003-0000-4000-8004-000000000202");
            public static readonly Guid FeatureResolved = new("00000003-0000-4000-8004-000000000203");
            public static readonly Guid FeatureClosed = new("00000003-0000-4000-8004-000000000204");
            public static readonly Guid FeatureRemoved = new("00000003-0000-4000-8004-000000000205");
            public static readonly Guid StoryNew = new("00000003-0000-4000-8004-000000000301");
            public static readonly Guid StoryActive = new("00000003-0000-4000-8004-000000000302");
            public static readonly Guid StoryResolved = new("00000003-0000-4000-8004-000000000303");
            public static readonly Guid StoryClosed = new("00000003-0000-4000-8004-000000000304");
            public static readonly Guid StoryRemoved = new("00000003-0000-4000-8004-000000000305");
            public static readonly Guid BugNew = new("00000003-0000-4000-8004-000000000401");
            public static readonly Guid BugActive = new("00000003-0000-4000-8004-000000000402");
            public static readonly Guid BugResolved = new("00000003-0000-4000-8004-000000000403");
            public static readonly Guid BugClosed = new("00000003-0000-4000-8004-000000000404");
            public static readonly Guid BugRemoved = new("00000003-0000-4000-8004-000000000405");
            public static readonly Guid TaskNew = new("00000003-0000-4000-8004-000000000501");
            public static readonly Guid TaskActive = new("00000003-0000-4000-8004-000000000502");
            public static readonly Guid TaskResolved = new("00000003-0000-4000-8004-000000000503");
            public static readonly Guid TaskClosed = new("00000003-0000-4000-8004-000000000504");
            public static readonly Guid TaskRemoved = new("00000003-0000-4000-8004-000000000505");
        }

        public static class Ops
        {
            public static readonly Guid EpicNew = new("00000003-0000-4000-8005-000000000101");
            public static readonly Guid EpicActive = new("00000003-0000-4000-8005-000000000102");
            public static readonly Guid EpicResolved = new("00000003-0000-4000-8005-000000000103");
            public static readonly Guid EpicClosed = new("00000003-0000-4000-8005-000000000104");
            public static readonly Guid EpicRemoved = new("00000003-0000-4000-8005-000000000105");
            public static readonly Guid FeatureNew = new("00000003-0000-4000-8005-000000000201");
            public static readonly Guid FeatureActive = new("00000003-0000-4000-8005-000000000202");
            public static readonly Guid FeatureResolved = new("00000003-0000-4000-8005-000000000203");
            public static readonly Guid FeatureClosed = new("00000003-0000-4000-8005-000000000204");
            public static readonly Guid FeatureRemoved = new("00000003-0000-4000-8005-000000000205");
            public static readonly Guid StoryNew = new("00000003-0000-4000-8005-000000000301");
            public static readonly Guid StoryActive = new("00000003-0000-4000-8005-000000000302");
            public static readonly Guid StoryResolved = new("00000003-0000-4000-8005-000000000303");
            public static readonly Guid StoryClosed = new("00000003-0000-4000-8005-000000000304");
            public static readonly Guid StoryRemoved = new("00000003-0000-4000-8005-000000000305");
            public static readonly Guid BugNew = new("00000003-0000-4000-8005-000000000401");
            public static readonly Guid BugActive = new("00000003-0000-4000-8005-000000000402");
            public static readonly Guid BugResolved = new("00000003-0000-4000-8005-000000000403");
            public static readonly Guid BugClosed = new("00000003-0000-4000-8005-000000000404");
            public static readonly Guid BugRemoved = new("00000003-0000-4000-8005-000000000405");
            public static readonly Guid TaskNew = new("00000003-0000-4000-8005-000000000501");
            public static readonly Guid TaskActive = new("00000003-0000-4000-8005-000000000502");
            public static readonly Guid TaskResolved = new("00000003-0000-4000-8005-000000000503");
            public static readonly Guid TaskClosed = new("00000003-0000-4000-8005-000000000504");
            public static readonly Guid TaskRemoved = new("00000003-0000-4000-8005-000000000505");
        }

        public static class Part
        {
            public static readonly Guid EpicNew = new("00000003-0000-4000-8006-000000000101");
            public static readonly Guid EpicActive = new("00000003-0000-4000-8006-000000000102");
            public static readonly Guid EpicResolved = new("00000003-0000-4000-8006-000000000103");
            public static readonly Guid EpicClosed = new("00000003-0000-4000-8006-000000000104");
            public static readonly Guid EpicRemoved = new("00000003-0000-4000-8006-000000000105");
            public static readonly Guid FeatureNew = new("00000003-0000-4000-8006-000000000201");
            public static readonly Guid FeatureActive = new("00000003-0000-4000-8006-000000000202");
            public static readonly Guid FeatureResolved = new("00000003-0000-4000-8006-000000000203");
            public static readonly Guid FeatureClosed = new("00000003-0000-4000-8006-000000000204");
            public static readonly Guid FeatureRemoved = new("00000003-0000-4000-8006-000000000205");
            public static readonly Guid StoryNew = new("00000003-0000-4000-8006-000000000301");
            public static readonly Guid StoryActive = new("00000003-0000-4000-8006-000000000302");
            public static readonly Guid StoryResolved = new("00000003-0000-4000-8006-000000000303");
            public static readonly Guid StoryClosed = new("00000003-0000-4000-8006-000000000304");
            public static readonly Guid StoryRemoved = new("00000003-0000-4000-8006-000000000305");
            public static readonly Guid BugNew = new("00000003-0000-4000-8006-000000000401");
            public static readonly Guid BugActive = new("00000003-0000-4000-8006-000000000402");
            public static readonly Guid BugResolved = new("00000003-0000-4000-8006-000000000403");
            public static readonly Guid BugClosed = new("00000003-0000-4000-8006-000000000404");
            public static readonly Guid BugRemoved = new("00000003-0000-4000-8006-000000000405");
            public static readonly Guid TaskNew = new("00000003-0000-4000-8006-000000000501");
            public static readonly Guid TaskActive = new("00000003-0000-4000-8006-000000000502");
            public static readonly Guid TaskResolved = new("00000003-0000-4000-8006-000000000503");
            public static readonly Guid TaskClosed = new("00000003-0000-4000-8006-000000000504");
            public static readonly Guid TaskRemoved = new("00000003-0000-4000-8006-000000000505");
        }

        public static class Arch
        {
            public static readonly Guid EpicNew = new("00000003-0000-4000-8007-000000000101");
            public static readonly Guid EpicActive = new("00000003-0000-4000-8007-000000000102");
            public static readonly Guid EpicResolved = new("00000003-0000-4000-8007-000000000103");
            public static readonly Guid EpicClosed = new("00000003-0000-4000-8007-000000000104");
            public static readonly Guid EpicRemoved = new("00000003-0000-4000-8007-000000000105");
            public static readonly Guid FeatureNew = new("00000003-0000-4000-8007-000000000201");
            public static readonly Guid FeatureActive = new("00000003-0000-4000-8007-000000000202");
            public static readonly Guid FeatureResolved = new("00000003-0000-4000-8007-000000000203");
            public static readonly Guid FeatureClosed = new("00000003-0000-4000-8007-000000000204");
            public static readonly Guid FeatureRemoved = new("00000003-0000-4000-8007-000000000205");
            public static readonly Guid StoryNew = new("00000003-0000-4000-8007-000000000301");
            public static readonly Guid StoryActive = new("00000003-0000-4000-8007-000000000302");
            public static readonly Guid StoryResolved = new("00000003-0000-4000-8007-000000000303");
            public static readonly Guid StoryClosed = new("00000003-0000-4000-8007-000000000304");
            public static readonly Guid StoryRemoved = new("00000003-0000-4000-8007-000000000305");
            public static readonly Guid BugNew = new("00000003-0000-4000-8007-000000000401");
            public static readonly Guid BugActive = new("00000003-0000-4000-8007-000000000402");
            public static readonly Guid BugResolved = new("00000003-0000-4000-8007-000000000403");
            public static readonly Guid BugClosed = new("00000003-0000-4000-8007-000000000404");
            public static readonly Guid BugRemoved = new("00000003-0000-4000-8007-000000000405");
            public static readonly Guid TaskNew = new("00000003-0000-4000-8007-000000000501");
            public static readonly Guid TaskActive = new("00000003-0000-4000-8007-000000000502");
            public static readonly Guid TaskResolved = new("00000003-0000-4000-8007-000000000503");
            public static readonly Guid TaskClosed = new("00000003-0000-4000-8007-000000000504");
            public static readonly Guid TaskRemoved = new("00000003-0000-4000-8007-000000000505");
        }

        public static class Empt
        {
            public static readonly Guid EpicNew = new("00000003-0000-4000-8008-000000000101");
            public static readonly Guid EpicActive = new("00000003-0000-4000-8008-000000000102");
            public static readonly Guid EpicResolved = new("00000003-0000-4000-8008-000000000103");
            public static readonly Guid EpicClosed = new("00000003-0000-4000-8008-000000000104");
            public static readonly Guid EpicRemoved = new("00000003-0000-4000-8008-000000000105");
            public static readonly Guid FeatureNew = new("00000003-0000-4000-8008-000000000201");
            public static readonly Guid FeatureActive = new("00000003-0000-4000-8008-000000000202");
            public static readonly Guid FeatureResolved = new("00000003-0000-4000-8008-000000000203");
            public static readonly Guid FeatureClosed = new("00000003-0000-4000-8008-000000000204");
            public static readonly Guid FeatureRemoved = new("00000003-0000-4000-8008-000000000205");
            public static readonly Guid StoryNew = new("00000003-0000-4000-8008-000000000301");
            public static readonly Guid StoryActive = new("00000003-0000-4000-8008-000000000302");
            public static readonly Guid StoryResolved = new("00000003-0000-4000-8008-000000000303");
            public static readonly Guid StoryClosed = new("00000003-0000-4000-8008-000000000304");
            public static readonly Guid StoryRemoved = new("00000003-0000-4000-8008-000000000305");
            public static readonly Guid BugNew = new("00000003-0000-4000-8008-000000000401");
            public static readonly Guid BugActive = new("00000003-0000-4000-8008-000000000402");
            public static readonly Guid BugResolved = new("00000003-0000-4000-8008-000000000403");
            public static readonly Guid BugClosed = new("00000003-0000-4000-8008-000000000404");
            public static readonly Guid BugRemoved = new("00000003-0000-4000-8008-000000000405");
            public static readonly Guid TaskNew = new("00000003-0000-4000-8008-000000000501");
            public static readonly Guid TaskActive = new("00000003-0000-4000-8008-000000000502");
            public static readonly Guid TaskResolved = new("00000003-0000-4000-8008-000000000503");
            public static readonly Guid TaskClosed = new("00000003-0000-4000-8008-000000000504");
            public static readonly Guid TaskRemoved = new("00000003-0000-4000-8008-000000000505");
        }
    }

    public static class Sprints
    {
        public static readonly Guid Sprint19 = new("00000004-0000-4000-8001-000000000019");
        public static readonly Guid Sprint20 = new("00000004-0000-4000-8001-000000000020");
        public static readonly Guid Sprint21 = new("00000004-0000-4000-8001-000000000021");
        public static readonly Guid Sprint22 = new("00000004-0000-4000-8001-000000000022");
        public static readonly Guid Sprint23 = new("00000004-0000-4000-8001-000000000023");
        public static readonly Guid Sprint24 = new("00000004-0000-4000-8001-000000000024");
        public static readonly Guid Sprint25 = new("00000004-0000-4000-8001-000000000025");
        public static readonly Guid Sprint26 = new("00000004-0000-4000-8001-000000000026");
    }

    public static class WorkItems
    {
        public static readonly Guid Ship90 = new("00000005-0000-4000-8001-000000000090");
        public static readonly Guid Ship91 = new("00000005-0000-4000-8001-000000000091");
        public static readonly Guid Ship92 = new("00000005-0000-4000-8001-000000000092");
        public static readonly Guid Ship93 = new("00000005-0000-4000-8001-000000000093");
        public static readonly Guid Ship101 = new("00000005-0000-4000-8001-000000000101");
        public static readonly Guid Ship102 = new("00000005-0000-4000-8001-000000000102");
        public static readonly Guid Ship103 = new("00000005-0000-4000-8001-000000000103");
        public static readonly Guid Ship104 = new("00000005-0000-4000-8001-000000000104");
        public static readonly Guid Ship108 = new("00000005-0000-4000-8001-000000000108");
        public static readonly Guid Ship118 = new("00000005-0000-4000-8001-000000000118");
        public static readonly Guid Ship120 = new("00000005-0000-4000-8001-000000000120");
        public static readonly Guid Ship124 = new("00000005-0000-4000-8001-000000000124");
        public static readonly Guid Ship126 = new("00000005-0000-4000-8001-000000000126");
        public static readonly Guid Ship129 = new("00000005-0000-4000-8001-000000000129");
        public static readonly Guid Ship131 = new("00000005-0000-4000-8001-000000000131");
        public static readonly Guid Ship138 = new("00000005-0000-4000-8001-000000000138");
        public static readonly Guid Ship141 = new("00000005-0000-4000-8001-000000000141");
        public static readonly Guid Ship144 = new("00000005-0000-4000-8001-000000000144");
        public static readonly Guid Ship147 = new("00000005-0000-4000-8001-000000000147");
        public static readonly Guid Ship150 = new("00000005-0000-4000-8001-000000000150");
        public static readonly Guid Ship151 = new("00000005-0000-4000-8001-000000000151");
        public static readonly Guid Ship153 = new("00000005-0000-4000-8001-000000000153");
        public static readonly Guid Ship156 = new("00000005-0000-4000-8001-000000000156");
        public static readonly Guid Ship158 = new("00000005-0000-4000-8001-000000000158");
        public static readonly Guid Ship160 = new("00000005-0000-4000-8001-000000000160");
        public static readonly Guid Ship162 = new("00000005-0000-4000-8001-000000000162");
        public static readonly Guid Ship163 = new("00000005-0000-4000-8001-000000000163");
        public static readonly Guid Ship165 = new("00000005-0000-4000-8001-000000000165");
        public static readonly Guid Ship166 = new("00000005-0000-4000-8001-000000000166");
        public static readonly Guid Ship170 = new("00000005-0000-4000-8001-000000000170");
        public static readonly Guid Ship171 = new("00000005-0000-4000-8001-000000000171");
        public static readonly Guid Ship172 = new("00000005-0000-4000-8001-000000000172");
        public static readonly Guid Ship173 = new("00000005-0000-4000-8001-000000000173");
        public static readonly Guid Ship174 = new("00000005-0000-4000-8001-000000000174");
        public static readonly Guid Ship175 = new("00000005-0000-4000-8001-000000000175");
        public static readonly Guid Ship176 = new("00000005-0000-4000-8001-000000000176");
        public static readonly Guid Ship177 = new("00000005-0000-4000-8001-000000000177");
        public static readonly Guid Ship178 = new("00000005-0000-4000-8001-000000000178");
        public static readonly Guid Ship179 = new("00000005-0000-4000-8001-000000000179");
        public static readonly Guid Ship180 = new("00000005-0000-4000-8001-000000000180");
        public static readonly Guid Ship181 = new("00000005-0000-4000-8001-000000000181");
        public static readonly Guid Ship182 = new("00000005-0000-4000-8001-000000000182");
        public static readonly Guid Ship183 = new("00000005-0000-4000-8001-000000000183");
        public static readonly Guid Ship30 = new("00000005-0000-4000-8001-000000000030");
        public static readonly Guid Ship31 = new("00000005-0000-4000-8001-000000000031");
        public static readonly Guid Ship32 = new("00000005-0000-4000-8001-000000000032");
        public static readonly Guid Ship33 = new("00000005-0000-4000-8001-000000000033");
        public static readonly Guid Ship34 = new("00000005-0000-4000-8001-000000000034");
        public static readonly Guid Ship35 = new("00000005-0000-4000-8001-000000000035");
        public static readonly Guid Ship36 = new("00000005-0000-4000-8001-000000000036");
        public static readonly Guid Ship37 = new("00000005-0000-4000-8001-000000000037");
        public static readonly Guid Ship38 = new("00000005-0000-4000-8001-000000000038");
        public static readonly Guid Ship39 = new("00000005-0000-4000-8001-000000000039");
        public static readonly Guid Ship40 = new("00000005-0000-4000-8001-000000000040");
        public static readonly Guid Ship41 = new("00000005-0000-4000-8001-000000000041");
        public static readonly Guid Ship42 = new("00000005-0000-4000-8001-000000000042");
        public static readonly Guid Ship43 = new("00000005-0000-4000-8001-000000000043");
        public static readonly Guid Ship44 = new("00000005-0000-4000-8001-000000000044");
        public static readonly Guid Ship45 = new("00000005-0000-4000-8001-000000000045");
        public static readonly Guid Ship46 = new("00000005-0000-4000-8001-000000000046");
        public static readonly Guid Ship47 = new("00000005-0000-4000-8001-000000000047");
        public static readonly Guid Ship48 = new("00000005-0000-4000-8001-000000000048");
        public static readonly Guid Ship49 = new("00000005-0000-4000-8001-000000000049");
        public static readonly Guid Ship50 = new("00000005-0000-4000-8001-000000000050");
        public static readonly Guid Ship51 = new("00000005-0000-4000-8001-000000000051");
        public static readonly Guid Ship52 = new("00000005-0000-4000-8001-000000000052");
        public static readonly Guid Arch1 = new("00000005-0000-4000-8007-000000000001");
        public static readonly Guid Arch2 = new("00000005-0000-4000-8007-000000000002");
        public static readonly Guid Arch3 = new("00000005-0000-4000-8007-000000000003");
        public static readonly Guid Arch4 = new("00000005-0000-4000-8007-000000000004");
        public static readonly Guid Arch5 = new("00000005-0000-4000-8007-000000000005");
    }

    public static class Comments
    {
        public static readonly Guid Ship90Note = new("00000005-0000-4000-8002-000000000901");
        public static readonly Guid Ship90NoteToArchive = new("00000005-0000-4000-8002-000000000902");
    }

    // Rows only: the bytes live in no blob store. A test that reads them pins a blob store holding SeedAttachmentBlobs.
    public static class Attachments
    {
        public static readonly Guid Ship90Scope = new("00000005-0000-4000-8003-000000000901");
        public static readonly Guid Ship90ScopeToArchive = new("00000005-0000-4000-8003-000000000902");
    }

    // On Ship101 (a Feature). Rows only: the first carries preview image and favicon keys, whose bytes are in SeedAttachmentBlobs.
    public static class Links
    {
        public static readonly Guid Ship101Pull = new("00000005-0000-4000-8004-000000000101");
        public static readonly Guid Ship101PageToArchive = new("00000005-0000-4000-8004-000000000102");
    }

    public static class ImportJobs
    {
        public static readonly Guid MobImport = new("00000006-0000-4000-8000-000000000001");
    }

    public static class Boards
    {
        public static readonly Guid ShipStories = new("00000007-0000-4000-8001-000000000001");
        public static readonly Guid ShipFeatures = new("00000007-0000-4000-8001-000000000002");
        public static readonly Guid ShipEpics = new("00000007-0000-4000-8001-000000000003");
        public static readonly Guid ShipTaskboard = new("00000007-0000-4000-8001-000000000004");
        public static readonly Guid PayStories = new("00000007-0000-4000-8002-000000000001");
        public static readonly Guid PayFeatures = new("00000007-0000-4000-8002-000000000002");
        public static readonly Guid PayEpics = new("00000007-0000-4000-8002-000000000003");
        public static readonly Guid PayTaskboard = new("00000007-0000-4000-8002-000000000004");
        public static readonly Guid MobStories = new("00000007-0000-4000-8003-000000000001");
        public static readonly Guid MobFeatures = new("00000007-0000-4000-8003-000000000002");
        public static readonly Guid MobEpics = new("00000007-0000-4000-8003-000000000003");
        public static readonly Guid MobTaskboard = new("00000007-0000-4000-8003-000000000004");
        public static readonly Guid DataStories = new("00000007-0000-4000-8004-000000000001");
        public static readonly Guid DataFeatures = new("00000007-0000-4000-8004-000000000002");
        public static readonly Guid DataEpics = new("00000007-0000-4000-8004-000000000003");
        public static readonly Guid DataTaskboard = new("00000007-0000-4000-8004-000000000004");
        public static readonly Guid OpsStories = new("00000007-0000-4000-8005-000000000001");
        public static readonly Guid OpsFeatures = new("00000007-0000-4000-8005-000000000002");
        public static readonly Guid OpsEpics = new("00000007-0000-4000-8005-000000000003");
        public static readonly Guid OpsTaskboard = new("00000007-0000-4000-8005-000000000004");
        public static readonly Guid PartStories = new("00000007-0000-4000-8006-000000000001");
        public static readonly Guid PartFeatures = new("00000007-0000-4000-8006-000000000002");
        public static readonly Guid PartEpics = new("00000007-0000-4000-8006-000000000003");
        public static readonly Guid PartTaskboard = new("00000007-0000-4000-8006-000000000004");
        public static readonly Guid ArchStories = new("00000007-0000-4000-8007-000000000001");
        public static readonly Guid ArchFeatures = new("00000007-0000-4000-8007-000000000002");
        public static readonly Guid ArchEpics = new("00000007-0000-4000-8007-000000000003");
        public static readonly Guid ArchTaskboard = new("00000007-0000-4000-8007-000000000004");
        public static readonly Guid EmptStories = new("00000007-0000-4000-8008-000000000001");
        public static readonly Guid EmptFeatures = new("00000007-0000-4000-8008-000000000002");
        public static readonly Guid EmptEpics = new("00000007-0000-4000-8008-000000000003");
        public static readonly Guid EmptTaskboard = new("00000007-0000-4000-8008-000000000004");
    }

    public static class SprintCapacities
    {
        public static readonly Guid Sprint21Priya = new("00000008-0000-4000-8001-000000002102");
        public static readonly Guid Sprint21Tom = new("00000008-0000-4000-8001-000000002103");
        public static readonly Guid Sprint21Mei = new("00000008-0000-4000-8001-000000002104");
        public static readonly Guid Sprint21Sam = new("00000008-0000-4000-8001-000000002105");
        public static readonly Guid Sprint21Ana = new("00000008-0000-4000-8001-000000002106");
        public static readonly Guid Sprint22Priya = new("00000008-0000-4000-8001-000000002202");
        public static readonly Guid Sprint22Tom = new("00000008-0000-4000-8001-000000002203");
        public static readonly Guid Sprint22Mei = new("00000008-0000-4000-8001-000000002204");
        public static readonly Guid Sprint22Sam = new("00000008-0000-4000-8001-000000002205");
        public static readonly Guid Sprint22Ana = new("00000008-0000-4000-8001-000000002206");
        public static readonly Guid Sprint23Priya = new("00000008-0000-4000-8001-000000002302");
        public static readonly Guid Sprint23Tom = new("00000008-0000-4000-8001-000000002303");
        public static readonly Guid Sprint23Mei = new("00000008-0000-4000-8001-000000002304");
        public static readonly Guid Sprint23Sam = new("00000008-0000-4000-8001-000000002305");
        public static readonly Guid Sprint23Ana = new("00000008-0000-4000-8001-000000002306");
        public static readonly Guid Sprint24Priya = new("00000008-0000-4000-8001-000000002402");
        public static readonly Guid Sprint24Tom = new("00000008-0000-4000-8001-000000002403");
        public static readonly Guid Sprint24Mei = new("00000008-0000-4000-8001-000000002404");
        public static readonly Guid Sprint24Sam = new("00000008-0000-4000-8001-000000002405");
        public static readonly Guid Sprint24Ana = new("00000008-0000-4000-8001-000000002406");
        public static readonly Guid Sprint25Priya = new("00000008-0000-4000-8001-000000002502");
        public static readonly Guid Sprint25Tom = new("00000008-0000-4000-8001-000000002503");
        public static readonly Guid Sprint25Mei = new("00000008-0000-4000-8001-000000002504");
        public static readonly Guid Sprint25Sam = new("00000008-0000-4000-8001-000000002505");
        public static readonly Guid Sprint25Ana = new("00000008-0000-4000-8001-000000002506");
    }

    public static class ProjectMembers
    {
        public static readonly Guid ShipJoel = new("00000009-0000-4000-8001-000000000001");
        public static readonly Guid ShipPriya = new("00000009-0000-4000-8001-000000000002");
        public static readonly Guid ShipTom = new("00000009-0000-4000-8001-000000000003");
        public static readonly Guid ShipMei = new("00000009-0000-4000-8001-000000000004");
        public static readonly Guid ShipSam = new("00000009-0000-4000-8001-000000000005");
        public static readonly Guid ShipAna = new("00000009-0000-4000-8001-000000000006");
        public static readonly Guid PayJoel = new("00000009-0000-4000-8002-000000000001");
        public static readonly Guid PayPriya = new("00000009-0000-4000-8002-000000000002");
        public static readonly Guid PayTom = new("00000009-0000-4000-8002-000000000003");
        public static readonly Guid MobJoel = new("00000009-0000-4000-8003-000000000001");
        public static readonly Guid MobPriya = new("00000009-0000-4000-8003-000000000002");
        public static readonly Guid MobTom = new("00000009-0000-4000-8003-000000000003");
        public static readonly Guid DataJoel = new("00000009-0000-4000-8004-000000000001");
        public static readonly Guid DataPriya = new("00000009-0000-4000-8004-000000000002");
        public static readonly Guid DataTom = new("00000009-0000-4000-8004-000000000003");
        public static readonly Guid OpsJoel = new("00000009-0000-4000-8005-000000000001");
        public static readonly Guid OpsPriya = new("00000009-0000-4000-8005-000000000002");
        public static readonly Guid OpsTom = new("00000009-0000-4000-8005-000000000003");
        public static readonly Guid PartPriya = new("00000009-0000-4000-8006-000000000002");
        public static readonly Guid PartTom = new("00000009-0000-4000-8006-000000000003");
        public static readonly Guid PayRavi = new("00000009-0000-4000-8002-000000000004");
        public static readonly Guid MobDana = new("00000009-0000-4000-8003-000000000004");
        public static readonly Guid MobJo = new("00000009-0000-4000-8003-000000000005");
        public static readonly Guid OpsLee = new("00000009-0000-4000-8005-000000000004");
        public static readonly Guid PartDana = new("00000009-0000-4000-8006-000000000004");
        public static readonly Guid ArchLee = new("00000009-0000-4000-8007-000000000001");
        public static readonly Guid ArchMei = new("00000009-0000-4000-8007-000000000002");
        public static readonly Guid EmptJoel = new("00000009-0000-4000-8008-000000000001");
        public static readonly Guid EmptPriya = new("00000009-0000-4000-8008-000000000002");
    }

    public static class JiraConnections
    {
        public static readonly Guid Mob = new("0000000a-0000-4000-8003-000000000001");
    }
}
