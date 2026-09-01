namespace WorkOrderService.Api.Persistence;

/// <summary>
/// Shared by the EF configurations and the request validators, so a value the API accepts can
/// never be one the column refuses. Keeping two copies of these numbers is how you get a 500 on
/// input that passed validation.
/// </summary>
public static class FieldLengths
{
    public const int ExternalId = 64;
    public const int SiteCode = 32;
    public const int Description = 1000;
    public const int Details = 1000;
    public const int StatusName = 20;
    public const int OutcomeName = 24;
}
