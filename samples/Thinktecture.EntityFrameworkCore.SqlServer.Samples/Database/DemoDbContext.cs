using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Thinktecture.EntityFrameworkCore;
using Thinktecture.EntityFrameworkCore.ValueConversion;

namespace Thinktecture.Database
{
   public class DemoDbContext : DbContext, IDbDefaultSchema
   {
      /// <inheritdoc />
      public string? Schema { get; }

#nullable disable
      public DbSet<Customer> Customers { get; set; }
      public DbSet<Product> Products { get; set; }
      public DbSet<Order> Orders { get; set; }
      public DbSet<OrderItem> OrderItems { get; set; }
#nullable enable

      public DemoDbContext(DbContextOptions<DemoDbContext> options, IDbDefaultSchema? schema = null)
         : base(options)
      {
         Schema = schema?.Schema;
      }

      /// <inheritdoc />
      protected override void OnModelCreating(ModelBuilder modelBuilder)
      {
         base.OnModelCreating(modelBuilder);

         modelBuilder.ConfigureTempTable<Guid>();
         modelBuilder.ConfigureTempTable<Guid, Guid>();

         modelBuilder.Entity<Customer>()
                     .Property(c => c.RowVersion)
                     .IsRowVersion()
                     .HasConversion(RowVersionValueConverter.Instance);

         modelBuilder.Entity<OrderItem>().HasKey(i => new { i.OrderId, i.ProductId });
      }
   }
}
