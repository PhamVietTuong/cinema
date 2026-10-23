namespace Cinema.Business.Contracts;

/// <summary>An image attached to an HTML email and referenced from its body as <c>cid:{ContentId}</c>.</summary>
public sealed record EmailInlineImage(string ContentId, byte[] Content, string MediaType, string FileName);
