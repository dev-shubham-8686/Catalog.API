using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domian.Responses.Item
{
    public class GetItemsResponse
    {
      public IEnumerable<GetItemResponse> Items { get; set; } = Enumerable.Empty<GetItemResponse>();

    }

}
