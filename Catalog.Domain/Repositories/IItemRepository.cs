using Catalog.Domain.Entities;
using Catalog.Domain.Responses;
using Catalog.Domain.Responses.Item;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domain.Repositories
{
    public interface IItemRepository: IRepository
    {
        Task<IEnumerable<Item>> GetAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Item>> GetAsync(int pageSize, int pageIndex, CancellationToken cancellationToken = default);
        Task<long> CountAsync(CancellationToken cancellationToken = default);
        Task<Item?> GetAsync(Guid id, CancellationToken cancellation = default);
        Task<Item> AddAsync(Item item, CancellationToken cancellationToken = default);
        Item Update(Item item, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Item?> FindItemAsync(Guid id, CancellationToken cancellationToken = default);

    }
}
