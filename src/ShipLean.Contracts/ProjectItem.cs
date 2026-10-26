namespace ShipLean.Contracts;

// The kinds of thing inside a project that IProjectAccess can resolve from an id alone.
public enum ProjectItem
{
    WorkItem,
    Sprint,
    Board,
    Comment,
    Attachment,
    ImportJob,
    JiraConnection,
    Link,
}
