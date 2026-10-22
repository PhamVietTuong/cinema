namespace Cinema.Business.Managers;

internal sealed record PriceOverrideLine(string Kind, Guid ItemId, string Description, int Quantity, double ListUnitPrice, double AppliedUnitPrice);
