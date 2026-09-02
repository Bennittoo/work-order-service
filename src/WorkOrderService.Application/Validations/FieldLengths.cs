namespace WorkOrderService.Application.Validations;

/// <summary>
/// Maximum stored lengths, shared by the validators and the EF Core configurations.
/// </summary>
/// <remarks>
/// Deliberately one definition. Two copies of these numbers is how you end up with a 500 on input
/// that passed validation, because the column refuses what the API accepted.
/// </remarks>
public static class FieldLengths
{
    /// <summary>Maximum length of a work order external identifier.</summary>
    public const int ExternalId = 64;

    /// <summary>Maximum length of a site code.</summary>
    public const int SiteCode = 32;

    /// <summary>Maximum length of a work order description.</summary>
    public const int Description = 1000;

    /// <summary>Maximum length of the free text stored against a status change.</summary>
    public const int Details = 1000;

    /// <summary>Column width for a persisted status or source name.</summary>
    public const int StatusName = 20;

    /// <summary>Column width for a persisted processing outcome name.</summary>
    public const int OutcomeName = 24;
}
