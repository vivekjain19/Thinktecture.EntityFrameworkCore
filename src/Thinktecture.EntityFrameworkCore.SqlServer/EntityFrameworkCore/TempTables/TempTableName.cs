using System;
using JetBrains.Annotations;

namespace Thinktecture.EntityFrameworkCore.TempTables
{
   internal class TempTableName : ITempTableNameLease
   {
      public string Name { get; }

      public TempTableName([NotNull] string name)
      {
         Name = name ?? throw new ArgumentNullException(nameof(name));
      }

      public void Dispose()
      {
         // Nothing to dispose.
      }
   }
}
