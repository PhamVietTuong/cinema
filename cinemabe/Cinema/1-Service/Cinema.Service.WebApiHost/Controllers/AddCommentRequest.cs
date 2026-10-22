namespace Cinema.Service.WebApiHost.Controllers;

public record AddCommentRequest(string Content, Guid? ParentId);
