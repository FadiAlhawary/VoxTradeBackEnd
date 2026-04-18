using Npgsql;

namespace VoxTrade.Data
{
    public interface IDbConnectionFactory
    {
        NpgsqlConnection CreateConnection();
    }

    public sealed class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly IConfiguration _configuration;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public NpgsqlConnection CreateConnection()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            return new NpgsqlConnection(connectionString);
        }
    }
}
