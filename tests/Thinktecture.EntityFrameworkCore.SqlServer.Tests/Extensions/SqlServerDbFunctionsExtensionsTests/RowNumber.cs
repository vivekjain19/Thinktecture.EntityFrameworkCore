using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Thinktecture.TestDatabaseContext;
using Xunit;
using Xunit.Abstractions;

namespace Thinktecture.Extensions.SqlServerDbFunctionsExtensionsTests
{
   // ReSharper disable once InconsistentNaming
   public class RowNumber : IntegrationTestsBase
   {
      public RowNumber(ITestOutputHelper testOutputHelper)
         : base(testOutputHelper, true)
      {
      }

      [Fact]
      public void Generates_RowNumber_with_orderby_and_one_column()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1" });
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("18C13F68-0981-4853-92FC-FB7B2551F70A"), Name = "2" });
         ArrangeDbContext.SaveChanges();

         var result = ActDbContext.TestEntities
                                  .Select(e => new
                                               {
                                                  e.Name,
                                                  RowNumber = EF.Functions.RowNumber(EF.Functions.OrderBy(e.Name))
                                               })
                                  .ToList();

         result.First(t => t.Name == "1").RowNumber.Should().Be(1);
         result.First(t => t.Name == "2").RowNumber.Should().Be(2);
      }

      [Fact]
      public void Generates_RowNumber_with_orderby_and_one_struct_column()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("18CF65B3-F53D-4F45-8DF5-DD62DCC8B2EB") });
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("28CF65B3-F53D-4F45-8DF5-DD62DCC8B2EB") });
         ArrangeDbContext.SaveChanges();

         var result = ActDbContext.TestEntities
                                  .Select(e => new
                                               {
                                                  e.Id,
                                                  RowNumber = EF.Functions.RowNumber(EF.Functions.OrderBy(e.Id))
                                               })
                                  .ToList();

         result.First(t => t.Id == new Guid("18CF65B3-F53D-4F45-8DF5-DD62DCC8B2EB")).RowNumber.Should().Be(1);
         result.First(t => t.Id == new Guid("28CF65B3-F53D-4F45-8DF5-DD62DCC8B2EB")).RowNumber.Should().Be(2);
      }

      [Fact]
      public void Generates_RowNumber_with_orderby_desc_and_one_column()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1" });
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("18C13F68-0981-4853-92FC-FB7B2551F70A"), Name = "2" });
         ArrangeDbContext.SaveChanges();

         var result = ActDbContext.TestEntities
                                  .Select(e => new
                                               {
                                                  e.Name,
                                                  RowNumber = EF.Functions.RowNumber(EF.Functions.OrderByDescending(e.Name))
                                               })
                                  .ToList();

         result.First(t => t.Name == "1").RowNumber.Should().Be(2);
         result.First(t => t.Name == "2").RowNumber.Should().Be(1);
      }

      [Fact]
      public void Generates_RowNumber_with_orderby_and_two_columns()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1", Count = 1 });
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("18C13F68-0981-4853-92FC-FB7B2551F70A"), Name = "1", Count = 2 });
         ArrangeDbContext.SaveChanges();

         var result = ActDbContext.TestEntities
                                  .Select(e => new
                                               {
                                                  e.Count,
                                                  RowNumber = EF.Functions.RowNumber(EF.Functions.OrderBy(e.Name).ThenBy(e.Count))
                                               })
                                  .ToList();

         result.First(t => t.Count == 1).RowNumber.Should().Be(1);
         result.First(t => t.Count == 2).RowNumber.Should().Be(2);
      }

      [Fact]
      public void Generates_RowNumber_with_orderby_desc_and_two_columns()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1", Count = 1 });
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("18C13F68-0981-4853-92FC-FB7B2551F70A"), Name = "1", Count = 2 });
         ArrangeDbContext.SaveChanges();

         var result = ActDbContext.TestEntities
                                  .Select(e => new
                                               {
                                                  e.Count,
                                                  RowNumber = EF.Functions.RowNumber(EF.Functions.OrderBy(e.Name).ThenByDescending(e.Count))
                                               })
                                  .ToList();

         result.First(t => t.Count == 1).RowNumber.Should().Be(2);
         result.First(t => t.Count == 2).RowNumber.Should().Be(1);
      }

      [Fact]
      public void Generates_RowNumber_with_partitionby_and_orderby_and_one_column()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1" });
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("18C13F68-0981-4853-92FC-FB7B2551F70A"), Name = "2" });
         ArrangeDbContext.SaveChanges();

         var result = ActDbContext.TestEntities
                                  .Select(e => new
                                               {
                                                  e.Name,
                                                  RowNumber = EF.Functions.RowNumber(e.Name, EF.Functions.OrderBy(e.Name))
                                               })
                                  .ToList();

         result.First(t => t.Name == "1").RowNumber.Should().Be(1);
         result.First(t => t.Name == "2").RowNumber.Should().Be(1);
      }

      [Fact]
      public void Generates_RowNumber_with_partitionby_and_orderby_and_two_columns()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1", Count = 1 });
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("18C13F68-0981-4853-92FC-FB7B2551F70A"), Name = "1", Count = 2 });
         ArrangeDbContext.SaveChanges();

         var result = ActDbContext.TestEntities
                                  .Select(e => new
                                               {
                                                  e.Count,
                                                  RowNumber = EF.Functions.RowNumber(e.Name, e.Count,
                                                                                     EF.Functions.OrderBy(e.Name).ThenBy(e.Count))
                                               })
                                  .ToList();

         result.First(t => t.Count == 1).RowNumber.Should().Be(1);
         result.First(t => t.Count == 2).RowNumber.Should().Be(1);
      }

      [Fact]
      public void Throws_if_RowNumber_contains_NewExpression()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1", Count = 1 });
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("18C13F68-0981-4853-92FC-FB7B2551F70A"), Name = "1", Count = 2 });
         ArrangeDbContext.SaveChanges();

         var query = ActDbContext.TestEntities
                                 .Select(e => new
                                              {
                                                 e.Count,
                                                 RowNumber = EF.Functions.RowNumber(new { e.Name, e.Count },
                                                                                    EF.Functions.OrderBy(e.Name).ThenBy(e.Count))
                                              });
         query.Invoking(q => q.ToList()).Should().Throw<NotSupportedException>()
              .WithMessage("The EF function 'RowNumber' contains some expressions not supported by the Entity Framework. One of the reason is the creation of new objects like: 'new { e.MyProperty, e.MyOtherProperty }'.");
      }

      [Fact]
      public void Should_throw_if_accessing_RowNumber_not_within_subquery()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1" });
         ArrangeDbContext.SaveChanges();

         var query = ActDbContext.TestEntities
                                 .Select(e => new
                                              {
                                                 e.Name,
                                                 RowNumber = EF.Functions.RowNumber(EF.Functions.OrderBy(e.Name))
                                              })
                                 .Where(i => i.RowNumber == 1);

         query.Invoking(q => q.ToList())
              .Should()
              .Throw<SqlException>();
      }

      [Fact]
      public void Should_be_able_to_fetch_whole_entity()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1" });
         ArrangeDbContext.SaveChanges();

         var query = ActDbContext.TestEntities
                                 .Select(e => new
                                              {
                                                 e,
                                                 RowNumber = EF.Functions.RowNumber(EF.Functions.OrderBy(e.Name))
                                              });

         var entities = query.ToList();
         entities.Should().HaveCount(1);
         entities[0].Should().BeEquivalentTo(new
                                             {
                                                e = new TestEntity
                                                    {
                                                       Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"),
                                                       Name = "1"
                                                    },
                                                RowNumber = 1
                                             });
      }

      [Fact]
      public void Should_filter_for_RowNumber_if_accessing_within_subquery()
      {
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("4883F7E0-FC8C-45FF-A579-DF351A3E79BF"), Name = "1" });
         ArrangeDbContext.TestEntities.Add(new TestEntity { Id = new Guid("18C13F68-0981-4853-92FC-FB7B2551F70A"), Name = "2" });
         ArrangeDbContext.SaveChanges();

         var result = ActDbContext.TestEntities
                                  .Select(e => new
                                               {
                                                  e.Name,
                                                  RowNumber = EF.Functions.RowNumber(EF.Functions.OrderBy(e.Name))
                                               })
                                  .AsSubQuery()
                                  .Where(i => i.RowNumber == 1)
                                  .ToList();

         result.Should().HaveCount(1);
         result[0].Name.Should().Be("1");
      }
   }
}