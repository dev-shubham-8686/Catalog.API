using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domain.Responses
{
    public class PaginatedItemResponseModel<TEntity> where TEntity : class
    {
        public PaginatedItemResponseModel(int pageIndex, int pageSize, long total, IEnumerable<TEntity> data)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            Total = total;
            Items = data;
        }

        public int PageIndex { get; }

        public int PageSize { get; }

        public long Total { get; }

        public IEnumerable<TEntity> Items { get; }
    }
}
