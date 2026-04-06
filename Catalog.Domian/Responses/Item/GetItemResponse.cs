using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domain.Responses.Item
{
    public class GetItemResponse
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public GenreInfo? Genre { get; set; }
        public ArtistInfo? Artist { get; set; }

        public class GenreInfo
        {
            public Guid GenreId { get; set; }
            public string? GenreDescription { get; set; }
        }

        public class ArtistInfo
        {
            public Guid ArtistId { get; set; }
            public string? ArtistName { get; set; }
        }
    }
}
