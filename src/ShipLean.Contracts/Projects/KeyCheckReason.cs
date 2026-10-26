namespace ShipLean.Contracts.Projects;

// Taken: a project the caller is in has the key. TakenByAnotherProject: one the caller is not in does. Invalid: not 2-4 uppercase letters.
public enum KeyCheckReason
{
    Taken,
    TakenByAnotherProject,
    Invalid,
}
