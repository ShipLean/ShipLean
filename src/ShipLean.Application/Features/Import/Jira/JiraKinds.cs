using ShipLean.Contracts.Import;

namespace ShipLean.Application.Features.Import.Jira;

// jira_connection.kind: 'cloud' or 'server'.
public static class JiraKinds
{
    public static JiraKind Parse(string column) => column == "server" ? JiraKind.Server : JiraKind.Cloud;

    public static string Column(JiraKind kind) => kind == JiraKind.Server ? "server" : "cloud";
}
