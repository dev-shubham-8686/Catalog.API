using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domian.Entities
{
    public class Artist
    {
        public Guid ArtistId { get; set; }

        public string? ArtistName { get; set; }

        public ICollection<Item> Items { get; set; } = new List<Item>();
    }
}
