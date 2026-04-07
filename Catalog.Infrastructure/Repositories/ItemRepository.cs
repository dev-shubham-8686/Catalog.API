using Catalog.Domain.Entities;
using Catalog.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Infrastructure.Repositories
{
    public class ItemRepository : IItemRepository
    {
        private readonly CatalogContext _context;
        public IUnitOfWork UnitOfWork
        {
            get
            {
                return _context;
            }
        }
        public ItemRepository(CatalogContext context)
        {
            _context = context ?? throw new
             ArgumentNullException(nameof(context));
        }
        public async Task<IEnumerable<Item>> GetAsync(CancellationToken cancellationToken = default)
        {
            return await _context
                            .Items
                            .AsNoTracking()
                            .ToListAsync(cancellationToken);
        }
        public async Task<Item?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Items
                            .AsNoTracking()
                            .Where(x => x.Id == id)
                            .Include(x => x.Genre).Include(x => x.Artist).FirstOrDefaultAsync(cancellationToken);
        }
        public async Task<long> CountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Items.LongCountAsync(cancellationToken);
        }
        public async Task<Item> AddAsync(Item item, CancellationToken cancellationToken = default)
        {
            var createdItem =  await _context.Items.AddAsync(item, cancellationToken);

            return createdItem.Entity;
        }
        public Item Update(Item item, CancellationToken cancellationToken = default)
        {
           _context.Entry(item).State = EntityState.Modified;

            return item;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = await FindItemAsync(id, cancellationToken);
            if (item is null)
                return;

            _context.Entry(item).State = EntityState.Deleted;
        }

        public async Task<Item?> FindItemAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Items.FindAsync([id], cancellationToken);
        }

        public async Task<IEnumerable<Item>> GetAsync(int pageSize, int pageIndex, CancellationToken cancellationToken = default)
        {
            return await _context
                            .Items
                            .AsNoTracking()
                            .OrderBy(x => x.Name)
                            .Skip(pageSize * pageIndex)
                            .Take(pageSize)
                            .ToListAsync(cancellationToken);
        }
    }

}




