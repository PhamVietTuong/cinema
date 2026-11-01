namespace Cinema.Business.DTO.Requests;

public class ModerateCommentRequest
{
    public Guid CommentId { get; set; }

    public bool Approved { get; set; }
}
