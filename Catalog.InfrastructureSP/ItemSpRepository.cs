using Catalog.Domain.Entities;
using Catalog.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Data;
namespace Catalog.InfrastructureSP
{
    public class ItemSpRepository
    {
        private readonly IDbConnectionFactory _sqlConnection;
        public ItemSpRepository(IDbConnectionFactory sqlConnection)
        {
            _sqlConnection = sqlConnection;
        }
        public Item? Add(Item item)
        {
            using var conn = _sqlConnection.CreateConnection();

            return conn.ExecuteScalar<Item>
            ("InsertItem", item, commandType: CommandType.StoredProcedure);
        }

        public async Task<IEnumerable<Item>> GetAsync()
        {

            using var conn = await _sqlConnection.CreateConnectionAsync();

            var result = await conn.QueryAsync<Item>
                 ("GetAllItems", commandType: CommandType.StoredProcedure);
            return result.AsList();
        }

        public async Task<Item?> GetAsync(Guid id)
        {
            using var conn = await _sqlConnection.CreateConnectionAsync();

            return await conn.ExecuteScalarAsync<Item>
                ("GetAllItems", new { Id = id.ToString() }, commandType: CommandType.StoredProcedure);
        }

        public Item? Update(Item item)
        {
            using var conn = _sqlConnection.CreateConnection();

            return conn.ExecuteScalar<Item>
                ("UpdateItem", item, commandType: CommandType.StoredProcedure);
        }
    }
}
