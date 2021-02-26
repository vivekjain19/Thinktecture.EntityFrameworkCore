using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Thinktecture.TestDatabaseContext;
using Xunit;
using Xunit.Abstractions;

namespace Thinktecture.Extensions.DbContextExtensionsTests
{
   // ReSharper disable once InconsistentNaming
   public class TruncateTableAsync : IntegrationTestsBase
   {
      public TruncateTableAsync(ITestOutputHelper testOutputHelper)
         : base(testOutputHelper, true)
      {
      }

      [Fact]
      public void Should_not_throw_if_table_is_empty()
      {
         ActDbContext.Awaiting(ctx => ctx.TruncateTableAsync<TestEntity>())
                     .Should().NotThrow();
      }

      [Fact]
      public async Task Should_delete_entities()
      {
         ArrangeDbContext.Add(new TestEntity
                              {
                                 Id = new Guid("40B5CA93-5C02-48AD-B8A1-12BC13313866"),
                                 Name = "Name",
                                 Count = 42
                              });
         await ArrangeDbContext.SaveChangesAsync();

         await ActDbContext.TruncateTableAsync<TestEntity>();

         var loadedEntities = await AssertDbContext.TestEntities.ToListAsync();
         loadedEntities.Should().BeEmpty();
      }
   }
}