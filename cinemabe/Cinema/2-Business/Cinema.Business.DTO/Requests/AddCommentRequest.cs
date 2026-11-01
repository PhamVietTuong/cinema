namespace Cinema.Business.DTO.Requests;

public class AddCommentRequest
{
    public string Content { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }
}
