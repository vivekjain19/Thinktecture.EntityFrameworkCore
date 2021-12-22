using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Diagnostics.Internal;
using Thinktecture.EntityFrameworkCore;
using Thinktecture.EntityFrameworkCore.Infrastructure;
using Thinktecture.EntityFrameworkCore.Query;
using Thinktecture.EntityFrameworkCore.Testing;
using Thinktecture.Json;
using Thinktecture.TestDatabaseContext;

namespace Thinktecture;

[Collection("SqlServerTests")]
public class IntegrationTestsBase : SqlServerDbContextIntegrationTests<TestDbContext>
{
   private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { Converters = { new ConvertibleClassConverter() } };

   protected Action<ModelBuilder>? ConfigureModel { get; set; }
   protected IReadOnlyCollection<string> SqlStatements { get; }

   protected bool IsTenantDatabaseSupportEnabled { get; set; }
   protected Mock<ITenantDatabaseProvider> TenantDatabaseProviderMock { get; }

   protected IntegrationTestsBase(ITestOutputHelper testOutputHelper, bool useSharedTables)
      : base(TestContext.Instance.ConnectionString, useSharedTables)
   {
      DisableModelCache = true;

      var loggerFactory = TestContext.Instance.GetLoggerFactory(testOutputHelper);
      SqlStatements = loggerFactory.CollectExecutedCommands();

      UseLoggerFactory(loggerFactory);

      TenantDatabaseProviderMock = new Mock<ITenantDatabaseProvider>(MockBehavior.Strict);
   }

   [SuppressMessage("Usage", "EF1001", MessageId = "Internal EF Core API usage.")]
   protected IDiagnosticsLogger<TCategory> CreateDiagnosticsLogger<TCategory>(ILoggingOptions? options = null, DiagnosticSource? diagnosticSource = null)
      where TCategory : LoggerCategory<TCategory>, new()
   {
      var loggerFactory = LoggerFactory ?? throw new InvalidOperationException($"The {nameof(LoggerFactory)} must be set first.");

      return new DiagnosticsLogger<TCategory>(loggerFactory, options ?? new LoggingOptions(),
                                              diagnosticSource ?? new DiagnosticListener(typeof(TCategory).ShortDisplayName()),
                                              new SqlServerLoggingDefinitions(),
                                              new NullDbContextLogger());
   }

   /// <inheritdoc />
   protected override TestDbContext CreateContext(DbContextOptions<TestDbContext> options, IDbDefaultSchema schema)
   {
      var ctx = base.CreateContext(options, schema);
      ctx.ConfigureModel = ConfigureModel;

      return ctx;
   }

   /// <inheritdoc />
   protected override DbContextOptionsBuilder<TestDbContext> CreateOptionsBuilder(DbConnection? connection)
   {
      var builder = base.CreateOptionsBuilder(connection)
                        .AddNestedTransactionSupport()
                        .ConfigureWarnings(warningsBuilder => warningsBuilder.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

      builder.AddOrUpdateExtension<RelationalDbContextOptionsExtension>(extension => extension.Register(typeof(Mock<ITenantDatabaseProvider>), TenantDatabaseProviderMock));

      return builder;
   }

   /// <inheritdoc />
   protected override void ConfigureSqlServer(SqlServerDbContextOptionsBuilder builder)
   {
      base.ConfigureSqlServer(builder);

      builder.AddBulkOperationSupport()
             .AddRowNumberSupport()
             .AddCollectionParameterSupport(_jsonSerializerOptions);

      if (IsTenantDatabaseSupportEnabled)
         builder.AddTenantDatabaseSupport<TestTenantDatabaseProviderFactory>();
   }

   /// <inheritdoc />
   protected override string DetermineSchema(bool useSharedTables)
   {
      return useSharedTables ? $"{TestContext.Instance.Configuration["SourceBranchName"]}_tests" : base.DetermineSchema(false);
   }
}
