namespace NICEDA.ApiService.Crm;

public sealed record CustomerProfile(
    string CustomerId, string CustomerName, string AccountOwner, string Region,
    string Classification, string HealthSummary, string RenewalContext,
    string SourceSystem, string SourceId);

public static class CustomerProfiles
{
    // NICEDA-PRACTICE-3: curate business concepts from the original entry
    // fields; retain provenance instead of inventing another source of facts.
    public static CustomerProfile Project(CrmCustomer customer) => new(
        customer.Id, customer.CompanyName, customer.AccountManager, customer.Region,
        "Confidential", $"{customer.SalesNotes} {customer.SupportNotes}",
        customer.RenewalDateText, "CRM", customer.Id);
}
