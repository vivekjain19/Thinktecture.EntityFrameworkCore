using System;
using System.Collections.Generic;
using System.Data.Common;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Thinktecture.EntityFrameworkCore.TempTables.NameSuffixing
{
#pragma warning disable CA1812

   internal class TempTableSuffixLeasing
   {
      private readonly Dictionary<(DbConnection, IEntityType), TempTableSuffixes> _leasing;

      public TempTableSuffixLeasing()
      {
         _leasing = new Dictionary<(DbConnection, IEntityType), TempTableSuffixes>();
      }

      public TempTableSuffixLease Lease([NotNull] DbConnection connection, [NotNull] IEntityType entityType)
      {
         if (connection == null)
            throw new ArgumentNullException(nameof(connection));
         if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));

         if (!_leasing.TryGetValue((connection, entityType), out var suffixes))
         {
            suffixes = new TempTableSuffixes();
            _leasing.Add((connection, entityType), suffixes);
         }

         return suffixes.Lease();
      }
   }
}
