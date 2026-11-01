namespace ShipLean.LocalEnvironment;

// Finds the repository from wherever the process runs, so nothing depends on the working directory.
public static class RepoLayout
{
    public static string Root { get; } = FindRoot();

    public static string WebApiProject => Path.Combine(Root, "src", "ShipLean.WebApi");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "src", "ShipLean.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
               ?? throw new InvalidOperationException("Could not find src/ShipLean.slnx above " + AppContext.BaseDirectory);
    }
}
