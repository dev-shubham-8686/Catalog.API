using Catalog.Domian.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domian.Requests.Item
{
    public class AddItemRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
       
        
    }
}
