using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Thinktecture.EntityFrameworkCore.Data;
using Thinktecture.EntityFrameworkCore.TempTables;
using Thinktecture.Internal;

namespace Thinktecture.EntityFrameworkCore.BulkOperations
{
   /// <summary>
   /// Executes bulk operations.
   /// </summary>
   [SuppressMessage("ReSharper", "EF1001")]
   public sealed class SqlServerBulkOperationExecutor
      : IBulkInsertExecutor, ITempTableBulkInsertExecutor, IBulkUpdateExecutor,
        IBulkInsertOrUpdateExecutor, ITruncateTableExecutor
   {
      private readonly DbContext _ctx;
      private readonly IDiagnosticsLogger<SqlServerDbLoggerCategory.BulkOperation> _logger;
      private readonly ISqlGenerationHelper _sqlGenerationHelper;

      private static class EventIds
      {
         public static readonly EventId Inserting = 0;
         public static readonly EventId Inserted = 1;
      }

      /// <summary>
      /// Initializes new instance of <see cref="SqlServerBulkOperationExecutor"/>.
      /// </summary>
      /// <param name="ctx">Current database context.</param>
      /// <param name="logger">Logger.</param>
      /// <param name="sqlGenerationHelper">SQL generation helper.</param>
      public SqlServerBulkOperationExecutor(
         ICurrentDbContext ctx,
         IDiagnosticsLogger<SqlServerDbLoggerCategory.BulkOperation> logger,
         ISqlGenerationHelper sqlGenerationHelper)
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

         _ctx = ctx.Context;
         _logger = logger ?? throw new ArgumentNullException(nameof(logger));
         _sqlGenerationHelper = sqlGenerationHelper ?? throw new ArgumentNullException(nameof(sqlGenerationHelper));
      }

      /// <inheritdoc />
      IBulkInsertOptions IBulkInsertExecutor.CreateOptions(IEntityPropertiesProvider? propertiesToInsert)
      {
         return new SqlServerBulkInsertOptions { PropertiesToInsert = propertiesToInsert };
      }

      /// <inheritdoc />
      ITempTableBulkInsertOptions ITempTableBulkInsertExecutor.CreateOptions(IEntityPropertiesProvider? propertiesToInsert)
      {
         return new SqlServerTempTableBulkInsertOptions { PropertiesToInsert = propertiesToInsert };
      }

      /// <inheritdoc />
      IBulkUpdateOptions IBulkUpdateExecutor.CreateOptions(IEntityPropertiesProvider? propertiesToUpdate, IEntityPropertiesProvider? keyProperties)
      {
         return new SqlServerBulkUpdateOptions
                {
                   PropertiesToUpdate = propertiesToUpdate,
                   KeyProperties = keyProperties
                };
      }

      /// <inheritdoc />
      IBulkInsertOrUpdateOptions IBulkInsertOrUpdateExecutor.CreateOptions(
         IEntityPropertiesProvider? propertiesToInsert,
         IEntityPropertiesProvider? propertiesToUpdate,
         IEntityPropertiesProvider? keyProperties)
      {
         return new SqlServerBulkInsertOrUpdateOptions
                {
                   PropertiesToInsert = propertiesToInsert,
                   PropertiesToUpdate = propertiesToUpdate,
                   KeyProperties = keyProperties
                };
      }

      /// <inheritdoc />
      public Task BulkInsertAsync<T>(
         IEnumerable<T> entities,
         IBulkInsertOptions options,
         CancellationToken cancellationToken = default)
         where T : class
      {
         var entityType = _ctx.Model.GetEntityType(typeof(T));

         return BulkInsertAsync(entityType, entities, entityType.GetSchema(), entityType.GetTableName(), options, cancellationToken);
      }

      private async Task BulkInsertAsync<T>(
         IEntityType entityType,
         IEnumerable<T> entities,
         string? schema,
         string tableName,
         IBulkInsertOptions options,
         CancellationToken cancellationToken = default)
         where T : class
      {
         if (entities == null)
            throw new ArgumentNullException(nameof(entities));
         if (tableName == null)
            throw new ArgumentNullException(nameof(tableName));
         if (options == null)
            throw new ArgumentNullException(nameof(options));

         if (options is not ISqlServerBulkInsertOptions sqlServerOptions)
            sqlServerOptions = new SqlServerBulkInsertOptions(options);

         var properties = sqlServerOptions.PropertiesToInsert.DeterminePropertiesForInsert(entityType, null);
         var ctx = new BulkInsertContext(_ctx.GetService<IEntityDataReaderFactory>(),
                                         (SqlConnection)_ctx.Database.GetDbConnection(),
                                         (SqlTransaction?)_ctx.Database.CurrentTransaction?.GetDbTransaction(),
                                         sqlServerOptions,
                                         properties);

         EnsureNoSeparateOwnedTypesInsideCollectionOwnedType(properties);

         await BulkInsertAsync(entities, schema, tableName, ctx, cancellationToken);
      }

      private static void EnsureNoSeparateOwnedTypesInsideCollectionOwnedType(IReadOnlyList<PropertyWithNavigations> properties)
      {
         foreach (var property in properties)
         {
            INavigation? collection = null;

            foreach (var navigation in property.Navigations)
            {
               if (!navigation.IsInlined() && collection is not null)
                  throw new NotSupportedException($"Non-inlined (i.e. with its own table) nested owned type '{navigation.DeclaringEntityType.Name}.{navigation.Name}' inside another owned type collection '{collection.DeclaringEntityType.Name}.{collection.Name}' is not supported.");

               if (navigation.IsCollection)
                  collection = navigation;
            }
         }
      }

      private async Task BulkInsertAsync<T>(IEnumerable<T> entities, string? schema, string tableName, ISqlServerBulkOperationContext ctx, CancellationToken cancellationToken)
         where T : class
      {
         using var reader = ctx.ReaderFactory.Create(_ctx, entities, ctx.Properties, ctx.HasExternalProperties);
         using var bulkCopy = CreateSqlBulkCopy(ctx.Connection, ctx.Transaction, schema, tableName, ctx.Options);

         var columns = SetColumnMappings(bulkCopy, reader);

         await _ctx.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

         try
         {
            LogInserting(ctx.Options.SqlBulkCopyOptions, bulkCopy, columns);
            var stopwatch = Stopwatch.StartNew();

            await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);

            LogInserted(ctx.Options.SqlBulkCopyOptions, stopwatch.Elapsed, bulkCopy, columns);

            if (ctx.HasExternalProperties)
            {
               var readEntities = reader.GetReadEntities();

               if (readEntities.Count != 0)
                  await BulkInsertSeparatedOwnedEntitiesAsync(readEntities, ctx, cancellationToken);
            }
         }
         finally
         {
            await _ctx.Database.CloseConnectionAsync().ConfigureAwait(false);
         }
      }

      private async Task BulkInsertSeparatedOwnedEntitiesAsync(
         IReadOnlyList<object> parentEntities,
         ISqlServerBulkOperationContext parentBulkOperationContext,
         CancellationToken cancellationToken)
      {
         if (parentEntities.Count == 0)
            return;

         foreach (var childContext in parentBulkOperationContext.GetChildren(parentEntities))
         {
            await BulkInsertAsync(childContext.Entities,
                                  childContext.EntityType.GetSchema(),
                                  childContext.EntityType.GetTableName(),
                                  childContext,
                                  cancellationToken).ConfigureAwait(false);
         }
      }

      private static string SetColumnMappings(SqlBulkCopy bulkCopy, IEntityDataReader reader)
      {
         var columnsSb = new StringBuilder();

         for (var i = 0; i < reader.Properties.Count; i++)
         {
            var property = reader.Properties[i];
            var index = reader.GetPropertyIndex(property);
            var storeObject = StoreObjectIdentifier.Create(property.Property.DeclaringEntityType, StoreObjectType.Table)
                              ?? throw new Exception($"Could not create StoreObjectIdentifier for table '{property.Property.DeclaringEntityType.Name}'.");
            var columnName = property.Property.GetColumnName(storeObject);

            bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(index, columnName));

            if (columnsSb.Length > 0)
               columnsSb.Append(", ");

            columnsSb.Append(columnName).Append(' ').Append(property.Property.GetColumnType());
         }

         return columnsSb.ToString();
      }

      private SqlBulkCopy CreateSqlBulkCopy(SqlConnection sqlCon, SqlTransaction? sqlTx, string? schema, string tableName, ISqlServerBulkInsertOptions sqlServerOptions)
      {
         var bulkCopy = new SqlBulkCopy(sqlCon, sqlServerOptions.SqlBulkCopyOptions, sqlTx)
                        {
                           DestinationTableName = _sqlGenerationHelper.DelimitIdentifier(tableName, schema),
                           EnableStreaming = sqlServerOptions.EnableStreaming
                        };

         if (sqlServerOptions.BulkCopyTimeout.HasValue)
            bulkCopy.BulkCopyTimeout = (int)sqlServerOptions.BulkCopyTimeout.Value.TotalSeconds;

         if (sqlServerOptions.BatchSize.HasValue)
            bulkCopy.BatchSize = sqlServerOptions.BatchSize.Value;

         return bulkCopy;
      }

      private void LogInserting(SqlBulkCopyOptions options, SqlBulkCopy bulkCopy, string columns)
      {
         _logger.Logger.LogDebug(EventIds.Inserting, @"Executing DbCommand [SqlBulkCopyOptions={SqlBulkCopyOptions}, BulkCopyTimeout={BulkCopyTimeout}, BatchSize={BatchSize}, EnableStreaming={EnableStreaming}]
INSERT BULK {Table} ({Columns})", options, bulkCopy.BulkCopyTimeout, bulkCopy.BatchSize, bulkCopy.EnableStreaming,
                                 bulkCopy.DestinationTableName, columns);
      }

      private void LogInserted(SqlBulkCopyOptions options, TimeSpan duration, SqlBulkCopy bulkCopy, string columns)
      {
         _logger.Logger.LogInformation(EventIds.Inserted, @"Executed DbCommand ({Duration}ms) [SqlBulkCopyOptions={SqlBulkCopyOptions}, BulkCopyTimeout={BulkCopyTimeout}, BatchSize={BatchSize}, EnableStreaming={EnableStreaming}]
INSERT BULK {Table} ({Columns})", (long)duration.TotalMilliseconds,
                                       options, bulkCopy.BulkCopyTimeout, bulkCopy.BatchSize, bulkCopy.EnableStreaming,
                                       bulkCopy.DestinationTableName, columns);
      }

      /// <inheritdoc />
      public Task<ITempTableQuery<T>> BulkInsertIntoTempTableAsync<T>(
         IEnumerable<T> entities,
         ITempTableBulkInsertOptions options,
         CancellationToken cancellationToken = default)
         where T : class
      {
         if (options is not ISqlServerTempTableBulkInsertOptions sqlServerOptions)
            sqlServerOptions = new SqlServerTempTableBulkInsertOptions(options);

         return BulkInsertIntoTempTableAsync(entities, sqlServerOptions, cancellationToken);
      }

      /// <summary>
      /// Inserts the provided <paramref name="entities"/> into a temp table.
      /// </summary>
      /// <param name="entities">Entities to insert.</param>
      /// <param name="options">Options.</param>
      /// <param name="cancellationToken">Cancellation token.</param>
      /// <typeparam name="T">Type of the entities.</typeparam>
      /// <returns>A query returning the inserted <paramref name="entities"/>.</returns>
      public async Task<ITempTableQuery<T>> BulkInsertIntoTempTableAsync<T>(
         IEnumerable<T> entities,
         ISqlServerTempTableBulkInsertOptions options,
         CancellationToken cancellationToken = default)
         where T : class
      {
         if (entities == null)
            throw new ArgumentNullException(nameof(entities));
         if (options == null)
            throw new ArgumentNullException(nameof(options));

         var entityType = _ctx.Model.GetEntityType(typeof(T));
         var selectedProperties = options.PropertiesToInsert.DeterminePropertiesForTempTable(entityType, null);

         if (selectedProperties.Any(p => !p.IsInlined))
            throw new NotSupportedException($"Bulk insert of separate owned types into temp tables is not supported. Properties of separate owned types: {String.Join(", ", selectedProperties.Where(p => !p.IsInlined))}");

         var tempTableOptions = options.TempTableCreationOptions;

         if (options.MomentOfPrimaryKeyCreation == MomentOfSqlServerPrimaryKeyCreation.AfterBulkInsert)
            tempTableOptions = new SqlServerTempTableCreationOptions(tempTableOptions) { PrimaryKeyCreation = PrimaryKeyPropertiesProviders.None };

         var tempTableCreator = _ctx.GetService<ISqlServerTempTableCreator>();
         var tempTableReference = await tempTableCreator.CreateTempTableAsync(entityType, tempTableOptions, cancellationToken).ConfigureAwait(false);

         try
         {
            await BulkInsertAsync(entityType, entities, null, tempTableReference.Name, options.BulkInsertOptions, cancellationToken).ConfigureAwait(false);

            if (options.MomentOfPrimaryKeyCreation == MomentOfSqlServerPrimaryKeyCreation.AfterBulkInsert)
            {
               var tempTableProperties = options.TempTableCreationOptions.PropertiesToInclude.DeterminePropertiesForTempTable(entityType, true);
               var keyProperties = options.PrimaryKeyCreation.GetPrimaryKeyProperties(entityType, tempTableProperties);
               await tempTableCreator.CreatePrimaryKeyAsync(_ctx, keyProperties, tempTableReference.Name, options.TempTableCreationOptions.TruncateTableIfExists, cancellationToken).ConfigureAwait(false);
            }

            var query = _ctx.Set<T>().FromTempTable(tempTableReference.Name);

            var pk = entityType.FindPrimaryKey();

            if (pk is not null && pk.Properties.Count != 0)
               query = query.AsNoTracking();

            return new TempTableQuery<T>(query, tempTableReference);
         }
         catch (Exception)
         {
            await tempTableReference.DisposeAsync().ConfigureAwait(false);
            throw;
         }
      }

      /// <inheritdoc />
      public async Task TruncateTableAsync<T>(CancellationToken cancellationToken = default)
         where T : class
      {
         var entityType = _ctx.Model.GetEntityType(typeof(T));
         var tableIdentifier = _sqlGenerationHelper.DelimitIdentifier(entityType.GetTableName(), entityType.GetSchema());
         var truncateStatement = $"TRUNCATE TABLE {tableIdentifier};";

         await _ctx.Database.ExecuteSqlRawAsync(truncateStatement, cancellationToken);
      }

      /// <inheritdoc />
      public async Task<int> BulkUpdateAsync<T>(
         IEnumerable<T> entities,
         IBulkUpdateOptions options,
         CancellationToken cancellationToken = default)
         where T : class
      {
         if (entities == null)
            throw new ArgumentNullException(nameof(entities));
         if (options == null)
            throw new ArgumentNullException(nameof(options));

         if (options is not ISqlServerBulkUpdateOptions sqlServerOptions)
            sqlServerOptions = new SqlServerBulkUpdateOptions(options);

         var entityType = _ctx.Model.GetEntityType(typeof(T));
         var propertiesForUpdate = options.PropertiesToUpdate.DeterminePropertiesForUpdate(entityType, true);

         if (propertiesForUpdate.Count == 0)
            throw new ArgumentException("The number of properties to update cannot be 0.");

         var tempTableOptions = GetSqlServerTempTableBulkInsertOptions(sqlServerOptions, null, sqlServerOptions.PropertiesToUpdate, sqlServerOptions.KeyProperties);

         await using var tempTable = await BulkInsertIntoTempTableAsync(entities, tempTableOptions, cancellationToken);

         var mergeStatement = CreateMergeCommand(entityType, tempTable.Name, sqlServerOptions, null, propertiesForUpdate, options.KeyProperties.DetermineKeyProperties(entityType, true));

         return await _ctx.Database.ExecuteSqlRawAsync(mergeStatement, cancellationToken);
      }

      /// <inheritdoc />
      public async Task<int> BulkInsertOrUpdateAsync<T>(
         IEnumerable<T> entities,
         IBulkInsertOrUpdateOptions options,
         CancellationToken cancellationToken = default)
         where T : class
      {
         if (entities == null)
            throw new ArgumentNullException(nameof(entities));
         if (options == null)
            throw new ArgumentNullException(nameof(options));

         if (options is not ISqlServerBulkInsertOrUpdateOptions sqlServerOptions)
            sqlServerOptions = new SqlServerBulkInsertOrUpdateOptions(options);

         var entityType = _ctx.Model.GetEntityType(typeof(T));
         var propertiesForInsert = options.PropertiesToInsert.DeterminePropertiesForInsert(entityType, true);
         var propertiesForUpdate = options.PropertiesToUpdate.DeterminePropertiesForUpdate(entityType, true);

         if (propertiesForInsert.Count == 0)
            throw new ArgumentException("The number of properties to insert cannot be 0.");
         var tempTableOptions = GetSqlServerTempTableBulkInsertOptions(sqlServerOptions, options.PropertiesToInsert, sqlServerOptions.PropertiesToUpdate, sqlServerOptions.KeyProperties);

         await using var tempTable = await BulkInsertIntoTempTableAsync(entities, tempTableOptions, cancellationToken);

         var mergeStatement = CreateMergeCommand(entityType, tempTable.Name, sqlServerOptions, propertiesForInsert, propertiesForUpdate, options.KeyProperties.DetermineKeyProperties(entityType, true));

         return await _ctx.Database.ExecuteSqlRawAsync(mergeStatement, cancellationToken);
      }

      private static ISqlServerTempTableBulkInsertOptions GetSqlServerTempTableBulkInsertOptions(
         ISqlServerBulkOperationOptions sqlServerOptions,
         IEntityPropertiesProvider? propertiesToInsert,
         IEntityPropertiesProvider? propertiesToUpdate,
         IEntityPropertiesProvider? keyProperties)
      {
         return sqlServerOptions.TempTableOptions.PropertiesToInsert is not null
                   ? sqlServerOptions.TempTableOptions
                   : new SqlServerTempTableBulkInsertOptions(sqlServerOptions.TempTableOptions)
                     {
                        PropertiesToInsert = propertiesToInsert is null && propertiesToUpdate is null
                                                ? null
                                                : new CompositeTempTableEntityPropertiesProvider(propertiesToInsert, propertiesToUpdate, keyProperties)
                     };
      }

      private string CreateMergeCommand<T>(
         IEntityType entityType,
         string sourceTempTableName,
         T options,
         IReadOnlyList<PropertyWithNavigations>? propertiesToInsert,
         IReadOnlyList<PropertyWithNavigations> propertiesToUpdate,
         IReadOnlyList<PropertyWithNavigations> keyProperties)
         where T : ISqlServerBulkOperationOptions
      {
         var sb = new StringBuilder();

         sb.Append("MERGE INTO ")
           .Append(_sqlGenerationHelper.DelimitIdentifier(entityType.GetTableName(), entityType.GetSchema()));

         if (options.MergeTableHints.Count != 0)
         {
            sb.Append(" WITH (");

            for (var i = 0; i < options.MergeTableHints.Count; i++)
            {
               if (i != 0)
                  sb.Append(", ");

               sb.Append(options.MergeTableHints[i]);
            }

            sb.Append(")");
         }

         sb.AppendLine(" AS d")
           .Append("USING ")
           .Append(_sqlGenerationHelper.DelimitIdentifier(sourceTempTableName))
           .Append(" AS s ON ");

         var isFirstIteration = true;

         foreach (var property in keyProperties)
         {
            if (!isFirstIteration)
               sb.AppendLine(" AND ");

            var storeObject = StoreObjectIdentifier.Create(property.Property.DeclaringEntityType, StoreObjectType.Table)
                              ?? throw new Exception($"Could not create StoreObjectIdentifier for table '{property.Property.DeclaringEntityType.Name}'.");
            var escapedColumnName = _sqlGenerationHelper.DelimitIdentifier(property.Property.GetColumnName(storeObject));

            sb.Append("(d.").Append(escapedColumnName).Append(" = s.").Append(escapedColumnName);

            if (property.Property.IsNullable)
               sb.Append(" OR d.").Append(escapedColumnName).Append(" IS NULL AND s.").Append(escapedColumnName).Append(" IS NULL");

            sb.Append(")");
            isFirstIteration = false;
         }

         isFirstIteration = true;

         foreach (var property in propertiesToUpdate.Except(keyProperties))
         {
            if (!isFirstIteration)
            {
               sb.Append(", ");
            }
            else
            {
               sb.AppendLine()
                 .AppendLine("WHEN MATCHED THEN")
                 .Append("\tUPDATE SET ");
            }

            var storeObject = StoreObjectIdentifier.Create(property.Property.DeclaringEntityType, StoreObjectType.Table)
                              ?? throw new Exception($"Could not create StoreObjectIdentifier for table '{property.Property.DeclaringEntityType.Name}'.");
            var escapedColumnName = _sqlGenerationHelper.DelimitIdentifier(property.Property.GetColumnName(storeObject));

            sb.Append("d.").Append(escapedColumnName).Append(" = s.").Append(escapedColumnName);
            isFirstIteration = false;
         }

         if (propertiesToInsert is not null)
         {
            sb.AppendLine()
              .AppendLine("WHEN NOT MATCHED THEN")
              .Append("\tINSERT (");

            isFirstIteration = true;

            foreach (var property in propertiesToInsert)
            {
               if (!isFirstIteration)
                  sb.Append(", ");

               var storeObject = StoreObjectIdentifier.Create(property.Property.DeclaringEntityType, StoreObjectType.Table)
                                 ?? throw new Exception($"Could not create StoreObjectIdentifier for table '{property.Property.DeclaringEntityType.Name}'.");
               var escapedColumnName = _sqlGenerationHelper.DelimitIdentifier(property.Property.GetColumnName(storeObject));

               sb.Append(escapedColumnName);
               isFirstIteration = false;
            }

            sb.AppendLine(")")
              .Append("\tVALUES (");

            isFirstIteration = true;

            foreach (var property in propertiesToInsert)
            {
               if (!isFirstIteration)
                  sb.Append(", ");

               var storeObject = StoreObjectIdentifier.Create(property.Property.DeclaringEntityType, StoreObjectType.Table)
                                 ?? throw new Exception($"Could not create StoreObjectIdentifier for table '{property.Property.DeclaringEntityType.Name}'.");
               var escapedColumnName = _sqlGenerationHelper.DelimitIdentifier(property.Property.GetColumnName(storeObject));

               sb.Append("s.").Append(escapedColumnName);
               isFirstIteration = false;
            }

            sb.Append(")");
         }

         sb.Append(_sqlGenerationHelper.StatementTerminator);

         return sb.ToString();
      }
   }
}
