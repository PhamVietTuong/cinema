namespace Cinema.Data.Enums;

/// <summary>What a customer complained about. Stored as int; never renumber.</summary>
public enum ComplaintCategory
{
    Other = 0,
    Service = 1,
    Booking = 2,
    Payment = 3,
    Projection = 4,
    Sound = 5,
    FoodAndDrink = 6,
    Facilities = 7,
    Staff = 8
}

/// <summary>Lifecycle of a complaint: Open, then InReview, then Resolved or Rejected. Stored as int; never renumber.</summary>
public enum ComplaintStatus
{
    Open = 0,
    InReview = 1,
    Resolved = 2,
    Rejected = 3
}

/// <summary>How a resolved complaint was settled. None while unresolved/rejected. Stored as int; never renumber.</summary>
public enum ComplaintResolution
{
    None = 0,
    /// <summary>The whole linked invoice is refunded through the staff refund flow.</summary>
    Refund = 1,
    /// <summary>A gift card is issued to the customer's email.</summary>
    GiftCard = 2,
    /// <summary>Loyalty points are added to the customer's account.</summary>
    Points = 3,
    /// <summary>No compensation, only an apology.</summary>
    Apology = 4
}
