namespace NotifyHub.Domain.Common;

/// <summary>Identifier generation. Version 7 UUIDs are time-ordered, which keeps B-tree inserts local.</summary>
public static class Ids
{
    public static Guid New() => Guid.CreateVersion7();
}
