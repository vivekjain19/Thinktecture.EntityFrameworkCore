using System;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;
using Thinktecture.EntityFrameworkCore.Migrations;

// ReSharper disable once CheckNamespace
namespace Thinktecture
{
   /// <summary>
   /// Extensions for <see cref="OperationBuilder{TOperation}"/>.
   /// </summary>
   public static class OperationBuilderExtensions
   {
      /// <summary>
      /// Annotation key for "IfNotExists".
      /// </summary>
      // ReSharper disable once ConvertToConstant.Global
      public static readonly string IfNotExistsKey = "Thinktecture:OperationBuilderExtensions:IfNotExists";

      /// <summary>
      /// Annotation key for "IfExists".
      /// </summary>
      // ReSharper disable once ConvertToConstant.Global
      public static readonly string IfExistsKey = "Thinktecture:OperationBuilderExtensions:IfExists";

      /// <summary>
      /// Flags the migration that it should be executed if corresponding entity (table, index, etc.) does not exist yet.
      /// </summary>
      /// <remarks>
      /// The <see cref="ThinktectureSqlServerMigrationsSqlGenerator"/> must be used so the annotations have some effect!
      /// Use the extension method "<see cref="SqlServerDbContextOptionsBuilderExtensions.UseThinktectureSqlServerMigrationsSqlGenerator"/>" to change the Migration SQL generator.
      /// </remarks>
      /// <param name="builder">An operation builder.</param>
      /// <typeparam name="T">Type of the migration operation.</typeparam>
      /// <exception cref="ArgumentNullException">Operation builder is <c>null</c>.</exception>
      public static void IfNotExists<T>(this OperationBuilder<T> builder)
         where T : MigrationOperation
      {
         if (builder == null)
            throw new ArgumentNullException(nameof(builder));

         builder.Annotation(IfNotExistsKey, true);
      }

      /// <summary>
      /// Flags the migration that it should be executed if corresponding entity (table, index, etc.) exists.
      /// </summary>
      /// <remarks>
      /// The <see cref="ThinktectureSqlServerMigrationsSqlGenerator"/> must be used so the annotations have some effect!
      /// Use the extension method "<see cref="SqlServerDbContextOptionsBuilderExtensions.UseThinktectureSqlServerMigrationsSqlGenerator"/>" to change the Migration SQL generator.
      /// </remarks>
      /// <param name="builder">An operation builder.</param>
      /// <typeparam name="T">Type of the migration operation.</typeparam>
      /// <exception cref="ArgumentNullException">Operation builder is <c>null</c>.</exception>
      public static void IfExists<T>(this OperationBuilder<T> builder)
         where T : MigrationOperation
      {
         if (builder == null)
            throw new ArgumentNullException(nameof(builder));

         builder.Annotation(IfExistsKey, true);
      }
   }
}
