using Catalog.Domain.Entities;
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
        Task<Item?> GetAsync(Guid id, CancellationToken cancellation = default);
        Task<Item> AddAsync(Item item, CancellationToken cancellationToken = default);
        Task<Item> UpdateAsync(Item item, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Item?> GetItemAsync(Guid id, CancellationToken cancellationToken = default);

    }
}
