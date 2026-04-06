using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domain.Entities
{
    public class Genre
    {
        public Guid GenreId { get; set; }
        public string? GenreDescription { get; set; }
        public ICollection<Item> Items { get; set; } = [];
    }
}
