using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Thinktecture.TestDatabaseContext;
using Xunit;
using Xunit.Abstractions;

namespace Thinktecture.Extensions.DbContextExtensionsTests
{
   // ReSharper disable once InconsistentNaming
   [Collection("BulkInsertTempTableAsync")]
   public class BulkInsertIntoTempTableAsync : IntegrationTestsBase
   {
      public BulkInsertIntoTempTableAsync(ITestOutputHelper testOutputHelper)
         : base(testOutputHelper, true)
      {
      }

      [Fact]
      public async Task Should_insert_queryType()
      {
         ConfigureModel = builder => builder.ConfigureTempTableEntity<CustomTempTable>().Property(t => t.Column2).HasMaxLength(100).IsRequired();

         var entities = new List<CustomTempTable> { new CustomTempTable(1, "value") };
         var query = await ActDbContext.BulkInsertIntoTempTableAsync(entities);

         var tempTable = await query.Query.ToListAsync();
         tempTable.Should()
                  .HaveCount(1).And
                  .BeEquivalentTo(new CustomTempTable(1, "value"));
      }

      [Fact]
      public async Task Should_insert_entityType_without_touching_real_table()
      {
         var entity = new TestEntity
                      {
                         Id = new Guid("577BFD36-21BC-4F9E-97B4-367B8F29B730"),
                         Name = "Name",
                         Count = 42,
                         ConvertibleClass = new ConvertibleClass(43)
                      };
         ArrangeDbContext.TestEntities.Add(entity);
         await ArrangeDbContext.SaveChangesAsync();

         var entities = new List<TestEntity> { entity };
         var query = await ActDbContext.BulkInsertIntoTempTableAsync(entities);

         var tempTable = await query.Query.ToListAsync();
         tempTable.Should()
                  .HaveCount(1).And
                  .BeEquivalentTo(new TestEntity
                                  {
                                     Id = new Guid("577BFD36-21BC-4F9E-97B4-367B8F29B730"),
                                     Name = "Name",
                                     Count = 42,
                                     ConvertibleClass = new ConvertibleClass(43)
                                  });
      }

      [Fact]
      public async Task Should_return_disposable_query()
      {
         var tempTableQuery = await ActDbContext.BulkInsertIntoTempTableAsync(Array.Empty<TestEntity>());
         tempTableQuery.Dispose();

         tempTableQuery.Awaiting(t => t.Query.ToListAsync())
                       .Should().Throw<SqlException>().Where(ex => ex.Message.StartsWith("Invalid object name '#TestEntities", StringComparison.Ordinal));
      }
   }
}
