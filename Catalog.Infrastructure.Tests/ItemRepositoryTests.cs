using Catalog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Infrastructure.Tests
{
    public class ItemRepositoryTests
    {
        [Fact]
        public async Task should_get_data()
        {
            var options = new DbContextOptionsBuilder<CatalogContext>()
               .UseInMemoryDatabase(databaseName: "should_get_data")
               .Options;
            await using var context = new TestCatalogContext(options);
            context.Database.EnsureCreated();

            var sut = new ItemRepository(context);
            var result = await sut.GetAsync();
            
            Assert.NotNull(result);
        }

        [Fact]
        public async Task should_returns_null_with_id_not_present()
        {
            var options = new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase(databaseName:
                    "should_returns_null_with_id_not_present")
                .Options;
            await using var context = new TestCatalogContext(options);
            context.Database.EnsureCreated();
            var sut = new ItemRepository(context);
            var result = await sut.GetAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        [Theory]
        [InlineData("b5b05534-9263-448c-a69e-0bbd8b3eb90e")]
        public async Task should_return_record_by_id(string guid)
        {
            var options = new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase(databaseName:
                    "should_return_record_by_id")
                .Options;

            await using var context = new TestCatalogContext(options);
            var id = Guid.Parse(guid);
            context.Database.EnsureCreated();
            var sut = new ItemRepository(context);
            var result = await sut.GetAsync(new Guid(guid));


            Assert.NotNull(result);
            Assert.Equal(id, result!.Id);
        }


    }
}
