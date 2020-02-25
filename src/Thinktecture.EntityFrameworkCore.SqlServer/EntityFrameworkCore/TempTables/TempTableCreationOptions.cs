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
      /// Truncates/drops the temp table before "creation" if the table exists already.
      /// Default is <c>false</c>.
      /// </summary>
      /// <remarks>
      /// If the database supports "truncate" then the table is going to be truncated otherwise the table is dropped.
      /// If the property is set to <c>false</c> then the temp table is considered a "new table", i.e. no "EXISTS" checks are made.
      /// </remarks>
      public bool TruncateTableIfExists { get; set; }

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

      /// <summary>
      /// Indication whether to drop the temp table on dispose of <see cref="ITempTableQuery{T}"/>.
      /// Default is <c>true</c>.
      /// </summary>
      /// <remarks>
      /// Set to <c>false</c> for more performance if the same temp table is re-used very often.
      /// Set <see cref="TruncateTableIfExists"/> to <c>true</c> on re-use.
      /// </remarks>
      public bool DropTableOnDispose { get; set; } = true;

      /// <summary>
      /// Adds "COLLATE database_default" to columns so the collation matches with the one of the user database instead of the master db.
      /// </summary>
      public bool UseDefaultDatabaseCollation { get; set; } = false;
   }
}
