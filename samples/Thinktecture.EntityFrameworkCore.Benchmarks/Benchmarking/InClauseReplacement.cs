using System.Data;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Thinktecture.Database;
using Thinktecture.EntityFrameworkCore.BulkOperations;

namespace Thinktecture.Benchmarking;

public class InClauseReplacement
{
   private BenchmarkContext? _benchmarkContext;
   private IServiceScope? _scope;
   private SqlServerBenchmarkDbContext? _sqlServerDbContext;
   private SqlServerTempTableBulkInsertOptions? _sqlServerTempTableBulkInsertOptions;
   private SqlConnection? _con;

   private static readonly Guid[] _values = Enumerable.Range(1, 1000).Select(i => Guid.NewGuid()).ToArray();

   private static readonly string _inClause = string.Join(",", _values.Select(g => $"'{g}'"));
   private static readonly string _xml = $"<R>{string.Join(String.Empty, _values.Select(i => $"<V>{i}</V>"))}</R>";
   private static readonly string _json = $"[{string.Join(",", _values.Select(g => $"\"{g}\""))}]";

   [GlobalSetup]
   public void Init()
   {
      _benchmarkContext = new BenchmarkContext();
      _scope = _benchmarkContext.RootServiceProvider.CreateScope();
      _sqlServerDbContext = _scope.ServiceProvider.GetRequiredService<SqlServerBenchmarkDbContext>();
      _sqlServerDbContext.Database.OpenConnection();
      _con = (SqlConnection?)_sqlServerDbContext.Database.GetDbConnection();
      _sqlServerTempTableBulkInsertOptions = new SqlServerTempTableBulkInsertOptions { DropTableOnDispose = false, TruncateTableIfExists = true };

      _sqlServerDbContext.Database.ExecuteSqlRaw(@"
IF( TYPE_ID('GuidTableType') IS NULL )
BEGIN
	CREATE TYPE GuidTableType AS TABLE
	(
		Column1 UNIQUEIDENTIFIER NOT NULL,
		PRIMARY KEY CLUSTERED (Column1)
	)
END");

      // fills EF caches
      InClause();
      TableValueParam();
      Xml();
      Xml_TOP();
      Xml_TOP_DISTINCT();
      Json();
      Json_TOP();
      Json_DISTINCT();
      Json_TOP_DISTINCT();
      TempTable();
   }

   [GlobalCleanup]
   public void Dispose()
   {
      _scope?.Dispose();
   }

   [Benchmark]
   public void InClause()
   {
      using var command = _con.CreateCommand();
      command.CommandText = $@"SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN ({_inClause})
";

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void TableValueParam()
   {
      using var command = _con.CreateCommand();
      command.CommandText = @"SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN
(
    SELECT [Column1] FROM @p0
)";

      var param = new SqlParameter("@p0", SqlDbType.Structured)
                  {
                     TypeName = "GuidTableType",
                     Value = new ValueReader(_values)
                  };
      command.Parameters.Add(param);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Xml()
   {
      using var command = _con.CreateCommand();
      command.CommandText = @"SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN
(
   SELECT I.value('. cast as xs:string?', 'uniqueidentifier') AS [V] FROM @p0.nodes('/R/V') N(I)
)";

      var param = new SqlParameter("@p0", SqlDbType.Xml)
                  {
                     Value = _xml
                  };
      command.Parameters.Add(param);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Xml_TOP()
   {
      using var command = _con.CreateCommand();
      command.CommandText = @"SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN
(
   SELECT TOP (@p1) I.value('. cast as xs:string?', 'uniqueidentifier') AS [V] FROM @p0.nodes('/R/V') N(I)
)";

      var param = new SqlParameter("@p0", SqlDbType.Xml)
                  {
                     Value = _xml
                  };
      command.Parameters.Add(param);
      command.Parameters.AddWithValue("@p1", _values.Length);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Xml_TOP_DISTINCT()
   {
      using var command = _con.CreateCommand();
      command.CommandText = @"SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN
(
   SELECT DISTINCT TOP (@p1) I.value('. cast as xs:string?', 'uniqueidentifier') AS [V] FROM @p0.nodes('/R/V') N(I)
)";

      var param = new SqlParameter("@p0", SqlDbType.Xml)
                  {
                     Value = _xml
                  };
      command.Parameters.Add(param);
      command.Parameters.AddWithValue("@p1", _values.Length);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Json()
   {
      using var command = _con.CreateCommand();
      command.CommandText = @"
SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN
(
    SELECT [value] FROM OPENJSON(@p0, '$') WITH ([value] UNIQUEIDENTIFIER '$')
)";

      command.Parameters.AddWithValue("@p0", _json);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Json_TOP()
   {
      using var command = _con.CreateCommand();
      command.CommandText = @"
SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN
(
    SELECT TOP (@p1) [value] FROM OPENJSON(@p0, '$') WITH ([value] UNIQUEIDENTIFIER '$')
)";

      command.Parameters.AddWithValue("@p0", _json);
      command.Parameters.AddWithValue("@p1", _values.Length);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Json_DISTINCT()
   {
      using var command = _con.CreateCommand();
      command.CommandText = @"
SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN
(
    SELECT DISTINCT [value] FROM OPENJSON(@p0, '$') WITH ([value] UNIQUEIDENTIFIER '$')
)";

      command.Parameters.AddWithValue("@p0", _json);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Json_TOP_DISTINCT()
   {
      using var command = _con.CreateCommand();
      command.CommandText = @"
SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN
(
    SELECT DISTINCT TOP (@p1) [value] FROM OPENJSON(@p0, '$') WITH ([value] UNIQUEIDENTIFIER '$')
)";

      command.Parameters.AddWithValue("@p0", _json);
      command.Parameters.AddWithValue("@p1", _values.Length);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void TempTable()
   {
      using var tempTable = _sqlServerDbContext.BulkInsertValuesIntoTempTableAsync(_values, _sqlServerTempTableBulkInsertOptions).Result;
      using var command = _con.CreateCommand();
      command.CommandText = $@"SELECT [m].*
FROM [demo].[Customers] AS [m]
WHERE [m].[Id] IN
(
    SELECT [column1] FROM [{tempTable.Name}]
)";

      command.ExecuteNonQuery();
   }
}
