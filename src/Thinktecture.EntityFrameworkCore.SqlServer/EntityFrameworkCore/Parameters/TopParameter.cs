namespace Thinktecture.EntityFrameworkCore.Parameters;

/// <summary>
/// Represents thee TOP parameter.
/// </summary>
internal class TopParameter<T> : IConvertible
{
   private readonly IReadOnlyCollection<T> _items;

   public TopParameter(IReadOnlyCollection<T> items)
   {
      _items = items;
   }

   public int ToInt32(IFormatProvider? provider)
   {
      return _items.Count;
   }

   public string ToString(IFormatProvider? provider)
   {
      return _items.Count.ToString(provider);
   }

   /// <inheritdoc />
#pragma warning disable CS1591
   // ReSharper disable ArrangeMethodOrOperatorBody
   public TypeCode GetTypeCode() => throw new NotSupportedException();

   public bool ToBoolean(IFormatProvider? provider) => throw new NotSupportedException();
   public byte ToByte(IFormatProvider? provider) => throw new NotSupportedException();
   public char ToChar(IFormatProvider? provider) => throw new NotSupportedException();
   public DateTime ToDateTime(IFormatProvider? provider) => throw new NotSupportedException();
   public decimal ToDecimal(IFormatProvider? provider) => throw new NotSupportedException();
   public double ToDouble(IFormatProvider? provider) => throw new NotSupportedException();
   public short ToInt16(IFormatProvider? provider) => throw new NotSupportedException();
   public long ToInt64(IFormatProvider? provider) => throw new NotSupportedException();
   public sbyte ToSByte(IFormatProvider? provider) => throw new NotSupportedException();
   public float ToSingle(IFormatProvider? provider) => throw new NotSupportedException();
   public object ToType(Type conversionType, IFormatProvider? provider) => throw new NotSupportedException();
   public ushort ToUInt16(IFormatProvider? provider) => throw new NotSupportedException();
   public uint ToUInt32(IFormatProvider? provider) => throw new NotSupportedException();
   public ulong ToUInt64(IFormatProvider? provider) => throw new NotSupportedException();

   // ReSharper restore ArrangeMethodOrOperatorBody
#pragma warning restore CS1591
}
