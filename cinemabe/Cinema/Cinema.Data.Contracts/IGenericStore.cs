namespace Cinema.Data.Contracts
{
    public interface IGenericStore<Entity> where Entity : class
    {
        Task<Entity> CreateAsync(Entity entity);
    }
}
