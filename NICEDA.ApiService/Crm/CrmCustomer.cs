namespace NICEDA.ApiService.Crm;

/// <summary>
/// The CRM's "Before" shape: optimized for a human filling out a form, not
/// for an agent reading it. Important signal (churn risk, renewal timing)
/// lives inside free-text notes instead of structured fields, and several
/// fields that should be typed (dates, money) are loose strings because
/// that's what a sales rep actually typed. Do not "fix" this shape here —
/// the "after" projection (NICEDA-PRACTICE-3, built in a later slice)
/// is what demonstrates the improvement.
/// </summary>
public sealed record CrmCustomer(
    string Id,
    string CompanyName,
    string PrimaryContactName,
    string PrimaryContactTitle,
    string PrimaryContactEmail,
    string PrimaryContactPhone,
    string SecondaryContactName,
    string SecondaryContactEmail,
    string BillingAddressLine1,
    string BillingAddressLine2,
    string BillingCity,
    string BillingPostalCode,
    string BillingCountry,
    string AccountManager,
    string Industry,
    string Region,
    string Tier,
    string SignupDateText,
    string LastContactDateText,
    string RenewalDateText,
    string ContractValueText,
    string Status,
    string SalesNotes,
    string SupportNotes,
    string Website,
    string PreferredLanguage,
    string Misc1,
    string Misc2);
