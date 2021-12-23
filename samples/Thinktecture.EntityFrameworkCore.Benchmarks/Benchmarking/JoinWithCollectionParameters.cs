using System.Collections;
using System.Data;
using System.Data.Common;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Thinktecture.Database;
using Thinktecture.EntityFrameworkCore.BulkOperations;
using Thinktecture.EntityFrameworkCore.TempTables;

namespace Thinktecture.Benchmarking;

public class JoinWithCollectionParameters
{
   private BenchmarkContext? _benchmarkContext;
   private IServiceScope? _scope;
   private SqlServerBenchmarkDbContext? _sqlServerDbContext;
   private SqlServerTempTableBulkInsertOptions _sqlServerTempTableBulkInsertOptions;
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
INNER JOIN
    @p0 AS param
    ON [m].[Id] = param.[Column1]
";

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
      command.CommandText = $@"SELECT [m].*
FROM [demo].[Customers] AS [m]
INNER JOIN
    (
        SELECT I.value('. cast as xs:string?', 'uniqueidentifier') AS [Column1] FROM @p0.nodes('/R/V') N(I)
    ) param
    ON [m].[Id] =  param.Column1
";

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
      command.CommandText = $@"SELECT [m].*
FROM [demo].[Customers] AS [m]
INNER JOIN
    (
        SELECT TOP (@p1) I.value('. cast as xs:string?', 'uniqueidentifier') AS [Column1] FROM @p0.nodes('/R/V') N(I)
    ) param
    ON [m].[Id] =  param.Column1
";

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
      command.CommandText = $@"SELECT [m].*
FROM [demo].[Customers] AS [m]
INNER JOIN
    (
        SELECT DISTINCT TOP (@p1) I.value('. cast as xs:string?', 'uniqueidentifier') AS [Column1] FROM @p0.nodes('/R/V') N(I)
    ) param
    ON [m].[Id] =  param.Column1
";

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
      command.CommandText = $@"SELECT [m].*
FROM [demo].[Customers] AS [m]
INNER JOIN
    (
        SELECT Column1 FROM OPENJSON(@p0, '$') WITH ([Column1] UNIQUEIDENTIFIER '$') AS param
    ) AS param
    ON [m].[Id] = param.[Column1]
";

      command.Parameters.AddWithValue("@p0", _json);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Json_TOP()
   {
      using var command = _con.CreateCommand();
      command.CommandText = $@"SELECT [m].*
FROM [demo].[Customers] AS [m]
INNER JOIN
    (
        SELECT TOP (@p1) Column1 FROM OPENJSON(@p0, '$') WITH ([Column1] UNIQUEIDENTIFIER '$') AS param
    ) AS param
    ON [m].[Id] = param.[Column1]
";

      command.Parameters.AddWithValue("@p0", _json);
      command.Parameters.AddWithValue("@p1", _values.Length);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Json_DISTINCT()
   {
      using var command = _con.CreateCommand();
      command.CommandText = $@"SELECT [m].*
FROM [demo].[Customers] AS [m]
INNER JOIN
    (
        SELECT DISTINCT Column1 FROM OPENJSON(@p0, '$') WITH ([Column1] UNIQUEIDENTIFIER '$') AS param
    ) AS param
    ON [m].[Id] = param.[Column1]
";

      command.Parameters.AddWithValue("@p0", _json);

      command.ExecuteNonQuery();
   }

   [Benchmark]
   public void Json_TOP_DISTINCT()
   {
      using var command = _con.CreateCommand();
      command.CommandText = $@"SELECT [m].*
FROM [demo].[Customers] AS [m]
INNER JOIN
    (
        SELECT DISTINCT TOP (@p1) Column1 FROM OPENJSON(@p0, '$') WITH ([Column1] UNIQUEIDENTIFIER '$') AS param
    ) AS param
    ON [m].[Id] = param.[Column1]
";

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
INNER JOIN
     [{tempTable.Name}] AS param
    ON [m].[Id] = param.[Column1]
";

      command.ExecuteNonQuery();
   }

   public class ValueReader : DbDataReader
   {
      private readonly Guid[] _values;

      private int _index;

      public override int FieldCount => 1;

      public ValueReader(Guid[] values)
      {
         _values = values;
         _index = -1;
      }

      public override DataTable GetSchemaTable()
      {
         var schemaTable = new DataTable();
         schemaTable.Columns.Add(SchemaTableColumn.ColumnName, typeof(string));
         schemaTable.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
         schemaTable.Columns.Add(SchemaTableColumn.IsKey, typeof(bool));
         schemaTable.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int));

         var row = schemaTable.NewRow();
         row[SchemaTableColumn.ColumnName] = "Column1";
         row[SchemaTableColumn.DataType] = typeof(Guid);
         row[SchemaTableColumn.IsKey] = true;
         row[SchemaTableColumn.ColumnOrdinal] = 0;

         schemaTable.Rows.Add(row);

         return schemaTable;
      }

      public override bool Read()
      {
         return ++_index < _values.Length;
      }

      public override Guid GetGuid(int ordinal)
      {
         return _values[_index];
      }

      public override int GetInt32(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override bool IsDBNull(int ordinal)
      {
         return false;
      }

      public override bool GetBoolean(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override byte GetByte(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
      {
         throw new NotImplementedException();
      }

      public override char GetChar(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
      {
         throw new NotImplementedException();
      }

      public override string GetDataTypeName(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override DateTime GetDateTime(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override decimal GetDecimal(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override double GetDouble(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override Type GetFieldType(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override float GetFloat(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override short GetInt16(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override long GetInt64(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override string GetName(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override int GetOrdinal(string name)
      {
         throw new NotImplementedException();
      }

      public override string GetString(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override object GetValue(int ordinal)
      {
         throw new NotImplementedException();
      }

      public override int GetValues(object[] values)
      {
         throw new NotImplementedException();
      }

      public override object this[int ordinal] => throw new NotImplementedException();

      public override object this[string name] => throw new NotImplementedException();

      public override int RecordsAffected { get; }

      public override bool HasRows { get; }

      public override bool IsClosed { get; }

      public override bool NextResult()
      {
         throw new NotImplementedException();
      }

      public override int Depth { get; }

      public override IEnumerator GetEnumerator()
      {
         throw new NotImplementedException();
      }
   }
}
