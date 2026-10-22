namespace Cinema.Service.WebApiHost.Controllers;

public record ModerateCommentRequest(Guid CommentId, bool Approved);
