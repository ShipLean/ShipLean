using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// What the server tells an agent when it connects. Program.cs adds the MCP server and its tools; this adds to it.
public sealed class McpServerServices : IServiceModule
{
    public const string Instructions =
        "ShipLean is where work lives from the first idea through delivery: it is the record, not local files on your machine. "
        + "When someone has an idea or asks you to research something that might become work, search for its Feature or create "
        + "one in a Proposed state with create_feature, put findings on it with attach_markdown, and end each session with comment and set_status. "
        + "When you work on a plan's items, put progress, reports, decisions and brief changes back into ShipLean. "
        + "Name things the way the team does: a work item by its key (SHIP-123), a sprint as current, next or by its name, "
        + "a state by its name, a person by name or email, and a project by its key. Leave project out on a read and you get "
        + "the user's most recent project; a write that cannot tell the project from an item key needs it when the user is in "
        + "more than one. Anything that moves many items returns a preview and a plan first: look at the preview, then call "
        + "again with that plan to apply exactly it. A tool error comes back as {ok:false, error:{code, message, field, "
        + "candidates}}: when the code is ambiguous, pick one of the candidates and call again. For the full flow from idea to "
        + "done, call planning_guide with part work; before you break down a feature or epic for several agents, call "
        + "planning_guide. To run a captured plan, call get_brief on the Feature for its waves, then get_brief on each task "
        + "and give that brief to the agent doing it, and have each agent call report_task when it is done. "
        + "Every result is one summary line, then the Markdown for a brief or a guide part, then a JSON object with the full "
        + "result (the same object as the structured content). If you see only the summary line, your client is hiding the "
        + "rest: do not search saved output, transcripts or ShipLean's source for it; tell the person, and carry on with the "
        + "tools whose summary line has what you need. Agents reach ShipLean only through these tools; there is no API token, "
        + "CLI or header to use instead.";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddMcpServer(options => options.ServerInstructions = Instructions);
}
