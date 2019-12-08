using System;
using JetBrains.Annotations;

namespace Thinktecture.EntityFrameworkCore.TempTables
{
   /// <summary>
   /// Options required for creation of a temp table
   /// </summary>
   public class TempTableCreationOptions
   {
      /// <summary>
      /// Drops/truncates the temp table if the table exists already.
      /// Default is <c>false</c>.
      /// </summary>
      public bool DropTempTableIfExists { get; set; }

      /// <summary>
      /// Indication whether the table name should be unique.
      /// </summary>
      [Obsolete("'MakeTableNameUnique' will be removed in future version. Use 'TableNameProvider' provider instead.")]
      public bool MakeTableNameUnique
      {
         get => _tableNameProvider is NewGuidTempTableNameProvider;
         set => _tableNameProvider = value ? NewGuidTempTableNameProvider.Instance : DefaultTempTableNameProvider.Instance;
      }

      private ITempTableNameProvider _tableNameProvider;

      /// <summary>
      /// Get or sets the temp table name provider.
      /// Default is <see cref="NewGuidTempTableNameProvider"/>.
      /// </summary>
      [NotNull]
      public ITempTableNameProvider TableNameProvider
      {
         get => _tableNameProvider ?? NewGuidTempTableNameProvider.Instance;
         set => _tableNameProvider = value ?? throw new ArgumentNullException(nameof(value), "The table name provider cannot be null.");
      }
   }
}
