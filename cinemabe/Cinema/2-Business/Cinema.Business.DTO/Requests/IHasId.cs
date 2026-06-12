namespace Cinema.Business.DTO.Requests;

/// <summary>Implemented by update requests so generic managers can locate the target entity.</summary>
public interface IHasId
{
    Guid Id { get; }
}
