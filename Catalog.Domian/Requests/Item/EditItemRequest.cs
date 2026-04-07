using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domain.Requests.Item
{
    public class EditItemRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }

    }
}
