using Cinema.Business.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Business
{
    public static class Bootstrap
    {
        public static void InjectBusinessAccessLayer(this IServiceCollection services)
        {
            services.AddScoped(typeof(IUserManager), typeof(UserManager));
        }
    }
}
