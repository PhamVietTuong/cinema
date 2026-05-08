using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Stores;
using Microsoft.Extensions.Configuration;

namespace Cinema.Data
{
    public class ApplicationUnitOfWork : IApplicationUnitOfWork
    {
        public IUserStore UserStore { get; private set; }

        public ApplicationUnitOfWork(CinemaContext gupolContext, IConfigurationRoot config)
        {
            UserStore = new UserStore(gupolContext);
        }

        public void Commit()
        {
        }

        public void Rollback()
        {
        }
    }
}
