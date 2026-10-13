namespace NotifyHub.Contracts;

/// <summary>
/// Wire-contract versions. The Windows service and agent negotiate against these values; breaking changes
/// require a new version namespace (V2) while the previous version remains supported for one release.
/// </summary>
public static class ContractVersions
{
    public const string ApiV1 = "v1";

    public const string ApiV1RoutePrefix = "/api/v1";
}
