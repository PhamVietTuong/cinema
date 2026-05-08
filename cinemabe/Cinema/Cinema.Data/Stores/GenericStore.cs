using Cinema.Data.Contracts;
using log4net;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Cinema.Data.Stores
{
    public class GenericStore<Entity> : IGenericStore<Entity> where Entity : class
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(IGenericStore<Entity>));
        internal DbSet<Entity> DbSet;
        public DbContext Context { get; set; }

        public GenericStore(DbContext context)
        {
            Context = context;
            DbSet = Context.Set<Entity>();
        }

        public async Task<Entity> CreateAsync(Entity entity)
        {
            int result = 0;

            try
            {
                var creationDateProperty = typeof(Entity).GetProperty("CreationTime");
                if (creationDateProperty != null && creationDateProperty.GetValue(entity) != null) creationDateProperty.SetValue(entity, DateTime.UtcNow);

                var IdProperty = typeof(Entity).GetProperty("Id");
                if (IdProperty != null && IdProperty.GetValue(entity) != null && IdProperty.PropertyType.Name != "Int32") IdProperty.SetValue(entity, Guid.Empty);

                var StatusProperty = typeof(Entity).GetProperty("Status");
                if (StatusProperty != null && (int)StatusProperty.GetValue(entity) == 0) StatusProperty.SetValue(entity, 1);

                await DbSet.AddAsync(entity);
                result = await Context.SaveChangesAsync();
            }
            catch (System.Exception e)
            {
                throw e;
            }
            return entity;
        }
    }
}
