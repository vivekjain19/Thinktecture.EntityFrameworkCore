using System.Collections;
using System.Data;
using System.Data.Common;

namespace Thinktecture.Benchmarking;

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

   public override int GetInt32(int ordinal) => throw new NotImplementedException();
   public override bool IsDBNull(int ordinal) => false;
   public override bool GetBoolean(int ordinal) => throw new NotImplementedException();
   public override byte GetByte(int ordinal) => throw new NotImplementedException();
   public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotImplementedException();
   public override char GetChar(int ordinal) => throw new NotImplementedException();
   public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotImplementedException();
   public override string GetDataTypeName(int ordinal) => throw new NotImplementedException();
   public override DateTime GetDateTime(int ordinal) => throw new NotImplementedException();
   public override decimal GetDecimal(int ordinal) => throw new NotImplementedException();
   public override double GetDouble(int ordinal) => throw new NotImplementedException();
   public override Type GetFieldType(int ordinal) => throw new NotImplementedException();
   public override float GetFloat(int ordinal) => throw new NotImplementedException();
   public override short GetInt16(int ordinal) => throw new NotImplementedException();
   public override long GetInt64(int ordinal) => throw new NotImplementedException();
   public override string GetName(int ordinal) => throw new NotImplementedException();
   public override int GetOrdinal(string name) => throw new NotImplementedException();
   public override string GetString(int ordinal) => throw new NotImplementedException();
   public override object GetValue(int ordinal) => throw new NotImplementedException();
   public override int GetValues(object[] values) => throw new NotImplementedException();
   public override object this[int ordinal] => throw new NotImplementedException();
   public override object this[string name] => throw new NotImplementedException();

   public override int RecordsAffected { get; }

   public override bool HasRows { get; }

   public override bool IsClosed { get; }

   public override bool NextResult() => throw new NotImplementedException();

   public override int Depth { get; }

   public override IEnumerator GetEnumerator() => throw new NotImplementedException();
}
