using ShipLean.LocalEnvironment;

namespace ShipLean.TestSupport;

// Finds the repository from wherever the tests run, so nothing depends on the working directory.
public static class RepoPaths
{
    public static string Root => RepoLayout.Root;

    public static string WebApiProject => RepoLayout.WebApiProject;

    public static string ArtifactsDirectory => Path.Combine(Root, "artifacts");
}
