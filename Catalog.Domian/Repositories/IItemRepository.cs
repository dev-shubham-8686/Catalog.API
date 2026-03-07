using Catalog.Domian.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domian.Repositories
{
    public interface IItemRepository
    {
        Task<IEnumerable<Item>> GetAsync();
        Task<Item?> GetAsync(Guid id);
        Item Add(Item item);
        Item Update(Item item);

    }
}
