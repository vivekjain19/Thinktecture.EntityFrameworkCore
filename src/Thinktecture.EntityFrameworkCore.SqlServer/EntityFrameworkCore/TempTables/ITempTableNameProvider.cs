using System;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Thinktecture.EntityFrameworkCore.TempTables
{
   /// <summary>
   /// Provides the name of the temp table.
   /// </summary>
   public interface ITempTableNameProvider
   {
      /// <summary>
      /// Leases the name for a temp table for provided <paramref name="entityType"/>.
      /// The instance of <see cref="ITempTableNameLease"/> should be disposed of to free the name for re-use.
      /// </summary>
      /// <param name="ctx">Database context.</param>
      /// <param name="entityType">Entity type to get the name of temp table for.</param>
      /// <returns>The name of the temp table.</returns>
      [NotNull]
      ITempTableNameLease LeaseName([NotNull] DbContext ctx, [NotNull] IEntityType entityType);
   }
}
