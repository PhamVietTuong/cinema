using Cinema.Data.Entities;
using log4net;
using Microsoft.EntityFrameworkCore;
using SoftFluent.EntityFrameworkCore.DataEncryption;
using SoftFluent.EntityFrameworkCore.DataEncryption.Providers;

namespace Cinema.Data.Contexts
{
    public class CinemaContext : DbContext
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CinemaContext));
        private readonly byte[] _encryptionKey = { 62, 45, 30, 48, 15, 94, 27, 89, 34, 87, 79, 97, 23, 217, 47, 49, 79, 43, 49, 45, 32, 74, 39, 92, 49, 42, 48, 94, 34, 84, 82, 32 };
        private readonly byte[] _encryptionIV = { 56, 80, 43, 3, 71, 84, 43, 74, 8, 45, 76, 34, 67, 44, 63, 32, 251, 35, 65, 89, 43, 66, 53, 35, 54, 89, 67, 78, 12, 54, 56, 78 };
        private readonly IEncryptionProvider _provider;

        public CinemaContext(DbContextOptions<CinemaContext> options) : base(options)
        {
            _provider = new AesProvider(_encryptionKey, _encryptionIV);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.UseEncryption(_provider);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            Logger.InfoFormat("Configuring DbContext");
        }
        public DbSet<User> User { get; set; }
    }
}
