namespace ShipLean.TestSupport.Contracts.Import;

// What an arranged Jira world is broken by: nothing, a token Jira refuses, Jira not answering, or attachment downloads that
// redirect to another host (on top of Cloud's own 303 to the media service) which answers 401 to any request that carries an Authorization header.
public enum JiraFault
{
    None,
    WrongToken,
    Down,
    RedirectOffSite,
}
