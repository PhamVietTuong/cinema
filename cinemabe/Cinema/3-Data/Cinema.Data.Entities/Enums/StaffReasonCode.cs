namespace Cinema.Data.Enums;

/// <summary>Reportable reason for a sensitive staff action (the free-text note stays in the audit/reason field).</summary>
public enum StaffReasonCode
{
    Other = 0,
    CustomerRequest = 1,
    WrongShowtime = 2,
    DuplicateSale = 3,
    ServiceFailure = 4,
    TechnicalIssue = 5,
    PriceMatch = 6,
    Compensation = 7
}
