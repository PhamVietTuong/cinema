namespace Cinema.Business.DTO.Requests;

public class SetGiftCardActiveRequest
{
    public Guid Id { get; set; }

    public bool Active { get; set; }
}
