using System.Data;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ColumnValueTypeEnum
{
    [EnumMember(Value = "String")]
    String,
    [EnumMember(Value = "Int16")]
    Int16,
    [EnumMember(Value = "Int32")]
    Int32,
    [EnumMember(Value = "Int64")]
    Int64,
    [EnumMember(Value = "UInt16")]
    UInt16,
    [EnumMember(Value = "UInt32")]
    UInt32,
    [EnumMember(Value = "UInt64")]
    UInt64,
    [EnumMember(Value = "Decimal")]
    Decimal,
    [EnumMember(Value = "Double")]
    Double,
    [EnumMember(Value = "Float")]
    Float,
    [EnumMember(Value = "Boolean")]
    Boolean,
    [EnumMember(Value = "DateTime")]
    DateTime,
    [EnumMember(Value = "DateTimeOffset")]
    DateTimeOffset,
    [EnumMember(Value = "TimeSpan")]
    TimeSpan,
    [EnumMember(Value = "Byte")]
    Byte,
    [EnumMember(Value = "SByte")]
    SByte,
    [EnumMember(Value = "ByteArray")]
    ByteArray,
    [EnumMember(Value = "Char")]
    Char,
    [EnumMember(Value = "Guid")]
    Guid,
    [EnumMember(Value = "Object")]
    Object
}
/// <summary>
/// Column for serializable data table.
/// </summary>
public class SerializableColumn
{
    /// <summary>
    /// Name of the data table.
    /// </summary>
    public string Name
    {
        get
        {
            return _Name;
        }
        set
        {
            if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Name));
            _Name = value;
        }
    }

    /// <summary>
    /// Column value type.
    /// </summary>
    public ColumnValueTypeEnum Type { get; set; } = ColumnValueTypeEnum.String;

    /// <summary>
    /// Original element type for array columns.
    /// Null for non-array columns or when type preservation is not required.
    /// Stored as the assembly-qualified type name for serialization compatibility.
    /// </summary>
    public string OriginalType { get; set; } = null;

    private string _Name = "MyTable";

    /// <summary>
    /// Instantiate.
    /// </summary>
    public SerializableColumn()
    {

    }
}

/// <summary>
/// Serializable data table.
/// </summary>
public class SerializableDataTable
{
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

    /// <summary>
    /// Name of the data table.
    /// </summary>
    public string Name { get; set; } = null;

    /// <summary>
    /// Columns.
    /// </summary>
    public List<SerializableColumn> Columns
    {
        get
        {
            return _Columns;
        }
        set
        {
            if (value == null) value = new List<SerializableColumn>();
            _Columns = value;
        }
    }

    /// <summary>
    /// Rows.
    /// </summary>
    public List<Dictionary<string, object>> Rows
    {
        get
        {
            return _Rows;
        }
        set
        {
            if (value == null) value = new List<Dictionary<string, object>>();
            _Rows = value;
        }
    }

    private List<SerializableColumn> _Columns = new List<SerializableColumn>();
    private List<Dictionary<string, object>> _Rows = new List<Dictionary<string, object>>();

    /// <summary>
    /// Instantiate.
    /// </summary>
    /// <param name="name">Name.</param>
    public SerializableDataTable(string name = null)
    {
        if (!String.IsNullOrEmpty(name)) Name = name;
    }

    /// <summary>
    /// Convert from a DataTable object.
    /// </summary>
    /// <param name="dt">DataTable.</param>
    /// <returns>SerializableDataTable.</returns>
    public static SerializableDataTable FromDataTable(DataTable dt)
    {
        if (dt == null) throw new ArgumentNullException(nameof(dt));

        SerializableDataTable ret = new SerializableDataTable();
        ret.Name = dt.TableName;

        foreach (DataColumn col in dt.Columns)
        {
            SerializableColumn serCol = new SerializableColumn
            {
                Name = col.ColumnName,
                Type = DataTypeToColumnValueTypeEnum(col.DataType)
            };

            // If the column type is Object, check if actual values are arrays
            // and capture the full array type
            if (col.DataType == typeof(object))
            {
                Type arrayType = DetectArrayType(dt, col.ColumnName);
                if (arrayType != null)
                {
                    serCol.OriginalType = arrayType.AssemblyQualifiedName;
                }
            }
            // For explicit array types, also store the full type (except byte[] which is already handled)
            else if (col.DataType.IsArray)
            {
                Type elementType = col.DataType.GetElementType();
                if (elementType != null && elementType != typeof(byte))
                {
                    serCol.OriginalType = col.DataType.AssemblyQualifiedName;
                }
            }
            // For unknown types that map to Object (e.g., Pgvector.Vector), store the original type
            else if (serCol.Type == ColumnValueTypeEnum.Object && col.DataType != typeof(object))
            {
                serCol.OriginalType = col.DataType.AssemblyQualifiedName;
            }

            ret.Columns.Add(serCol);
        }

        foreach (DataRow row in dt.Rows)
        {
            Dictionary<string, object> val = new Dictionary<string, object>();

            foreach (DataColumn col in dt.Columns)
            {
                object cellValue = row[col.ColumnName];
                if (cellValue == DBNull.Value || cellValue == null)
                {
                    val.Add(col.ColumnName, null);
                }
                else
                {
                    // For unknown types, try to normalize to array via ToArray() method
                    object normalizedValue = TryNormalizeToArray(cellValue);
                    val.Add(col.ColumnName, normalizedValue);
                }
            }

            ret.Rows.Add(val);
        }

        return ret;
    }

    /// <summary>
    /// Convert to a DataTable object.
    /// </summary>
    /// <returns>DataTable.</returns>
    public DataTable ToDataTable()
    {
        DataTable ret = new DataTable(Name);

        foreach (SerializableColumn col in Columns)
        {
            ret.Columns.Add(new DataColumn
            {
                ColumnName = col.Name,
                DataType = ColumnValueTypeEnumToDataType(col.Type)
            });
        }

        for (int i = 0; i < Rows.Count; i++)
        {
            Dictionary<string, object> dict = Rows[i];

            DataRow row = ret.NewRow();

            foreach (KeyValuePair<string, object> val in dict)
            {
                SerializableColumn col = Columns.FirstOrDefault(c => c.Name.Equals(val.Key));
                if (col == null)
                    throw new ArgumentException("No column exists with name '" + val.Key + "' as found in row " + i + ".");

                object value = GetValue(val.Value, col.OriginalType);
                row[val.Key] = value ?? DBNull.Value;
            }

            ret.Rows.Add(row);
        }

        return ret;
    }

    /// <summary>
    /// Convert to a markdown table string.
    /// </summary>
    /// <returns>Markdown formatted string representation of the table, or null if no columns are defined.</returns>
    public string ToMarkdown()
    {
        return MarkdownConverter.Convert(this);
    }

    private static ColumnValueTypeEnum DataTypeToColumnValueTypeEnum(Type t)
    {
        switch (t)
        {
            case Type _ when t == typeof(string):
                return ColumnValueTypeEnum.String;
            case Type _ when t == typeof(Int16):
                return ColumnValueTypeEnum.Int16;
            case Type _ when t == typeof(Int32):
                return ColumnValueTypeEnum.Int32;
            case Type _ when t == typeof(Int64):
                return ColumnValueTypeEnum.Int64;
            case Type _ when t == typeof(UInt16):
                return ColumnValueTypeEnum.UInt16;
            case Type _ when t == typeof(UInt32):
                return ColumnValueTypeEnum.UInt32;
            case Type _ when t == typeof(UInt64):
                return ColumnValueTypeEnum.UInt64;
            case Type _ when t == typeof(decimal):
                return ColumnValueTypeEnum.Decimal;
            case Type _ when t == typeof(double):
                return ColumnValueTypeEnum.Double;
            case Type _ when t == typeof(float):
                return ColumnValueTypeEnum.Float;
            case Type _ when t == typeof(bool):
                return ColumnValueTypeEnum.Boolean;
            case Type _ when t == typeof(DateTime):
                return ColumnValueTypeEnum.DateTime;
            case Type _ when t == typeof(DateTimeOffset):
                return ColumnValueTypeEnum.DateTimeOffset;
            case Type _ when t == typeof(TimeSpan):
                return ColumnValueTypeEnum.TimeSpan;
            case Type _ when t == typeof(byte):
                return ColumnValueTypeEnum.Byte;
            case Type _ when t == typeof(sbyte):
                return ColumnValueTypeEnum.SByte;
            case Type _ when t == typeof(byte[]):
                return ColumnValueTypeEnum.ByteArray;
            case Type _ when t == typeof(char):
                return ColumnValueTypeEnum.Char;
            case Type _ when t == typeof(Guid):
                return ColumnValueTypeEnum.Guid;
            case Type _ when t == typeof(object):
                return ColumnValueTypeEnum.Object;
            default:
                // Fall back to Object for unknown types (e.g., custom database types like pgvector)
                return ColumnValueTypeEnum.Object;
        }
    }

    private static Type ColumnValueTypeEnumToDataType(ColumnValueTypeEnum cvt)
    {
        switch (cvt)
        {
            case ColumnValueTypeEnum.String:
                return typeof(string);
            case ColumnValueTypeEnum.Int16:
                return typeof(Int16);
            case ColumnValueTypeEnum.Int32:
                return typeof(Int32);
            case ColumnValueTypeEnum.Int64:
                return typeof(Int64);
            case ColumnValueTypeEnum.UInt16:
                return typeof(UInt16);
            case ColumnValueTypeEnum.UInt32:
                return typeof(UInt32);
            case ColumnValueTypeEnum.UInt64:
                return typeof(UInt64);
            case ColumnValueTypeEnum.Decimal:
                return typeof(decimal);
            case ColumnValueTypeEnum.Double:
                return typeof(double);
            case ColumnValueTypeEnum.Float:
                return typeof(float);
            case ColumnValueTypeEnum.Boolean:
                return typeof(bool);
            case ColumnValueTypeEnum.DateTime:
                return typeof(DateTime);
            case ColumnValueTypeEnum.DateTimeOffset:
                return typeof(DateTimeOffset);
            case ColumnValueTypeEnum.TimeSpan:
                return typeof(TimeSpan);
            case ColumnValueTypeEnum.Byte:
                return typeof(byte);
            case ColumnValueTypeEnum.SByte:
                return typeof(sbyte);
            case ColumnValueTypeEnum.ByteArray:
                return typeof(byte[]);
            case ColumnValueTypeEnum.Char:
                return typeof(char);
            case ColumnValueTypeEnum.Guid:
                return typeof(Guid);
            case ColumnValueTypeEnum.Object:
                return typeof(object);
            default:
                throw new ArgumentException("Unknown column value type '" + cvt.ToString() + "'.");
        }
    }

    private static Type DetectArrayType(DataTable dt, string columnName)
    {
        foreach (DataRow row in dt.Rows)
        {
            object cellValue = row[columnName];
            if (cellValue != null && cellValue != DBNull.Value && cellValue.GetType().IsArray)
            {
                return cellValue.GetType();
            }
        }
        return null;
    }

    private static object TryNormalizeToArray(object value)
    {
        if (value == null) return null;

        Type valueType = value.GetType();

        // If it's already a known/primitive type or an array, return as-is
        if (valueType.IsPrimitive || valueType == typeof(string) || valueType == typeof(decimal) ||
            valueType == typeof(DateTime) || valueType == typeof(DateTimeOffset) ||
            valueType == typeof(TimeSpan) || valueType == typeof(Guid) || valueType.IsArray)
        {
            return value;
        }

        // For unknown types, try to extract array data via ToArray() method
        System.Reflection.MethodInfo toArrayMethod = valueType.GetMethod("ToArray",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            null, Type.EmptyTypes, null);

        if (toArrayMethod != null && toArrayMethod.ReturnType.IsArray)
        {
            try
            {
                return toArrayMethod.Invoke(value, null);
            }
            catch
            {
                // If ToArray() fails, return original value
                return value;
            }
        }

        // No ToArray() method found, return original value
        return value;
    }

    private static object GetValue(object obj, string originalType)
    {
        if (obj == null) return null;

        if (obj is JsonElement)
        {
            JsonElement element = (JsonElement)obj;
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    return ParseJsonArray(element, originalType);
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Number:
                    string numStr = obj.ToString();
                    // Handle scientific notation (e.g., 3.4028235E+38) as double
                    if (numStr.Contains("E") || numStr.Contains("e"))
                        return Double.Parse(numStr);
                    if (numStr.Contains("."))
                        return Decimal.Parse(numStr);
                    else
                    {
                        // Try to parse as long to handle large integers
                        if (Int64.TryParse(numStr, out long longVal))
                            return longVal;
                        return Decimal.Parse(numStr);
                    }
                case JsonValueKind.Object:
                    // Try to deserialize to original type if available
                    if (!String.IsNullOrEmpty(originalType))
                    {
                        Type targetType = ResolveType(originalType);
                        if (targetType != null)
                        {
                            try
                            {
                                string json = element.GetRawText();
                                object deserialized = JsonSerializer.Deserialize(json, targetType);
                                if (deserialized != null)
                                    return deserialized;
                            }
                            catch
                            {
                                // Deserialization failed, fall through to JSON string
                            }
                        }
                    }
                    // Fall back to compact JSON string
                    return JsonSerializer.Serialize(element);
                case JsonValueKind.String:
                    return obj.ToString();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.Undefined:
                    return null;
                default:
                    // Fall back to compact JSON string for unknown types
                    return JsonSerializer.Serialize(element);
            }
        }

        return obj;
    }

    private static object ParseJsonArray(JsonElement arrayElement, string originalType)
    {
        int length = arrayElement.GetArrayLength();

        // If we have type metadata, try to use it
        if (!String.IsNullOrEmpty(originalType))
        {
            Type targetType = ResolveType(originalType);

            if (targetType != null)
            {
                // If the original type is an array, create that array type
                if (targetType.IsArray)
                {
                    Type elementType = targetType.GetElementType();
                    if (elementType != null)
                    {
                        Array typedArray = Array.CreateInstance(elementType, length);
                        int index = 0;
                        foreach (JsonElement item in arrayElement.EnumerateArray())
                        {
                            object value = GetValue(item, null);
                            typedArray.SetValue(ConvertToType(value, elementType), index);
                            index++;
                        }
                        return typedArray;
                    }
                }
                else
                {
                    // Original type is not an array (e.g., Pgvector.Vector)
                    // Try to reconstruct it using a constructor that takes an array
                    object reconstructed = TryReconstructFromArray(arrayElement, targetType);
                    if (reconstructed != null)
                        return reconstructed;

                    // Fall through to return a typed array based on the JSON content
                }
            }
        }

        // Determine the best array type from the JSON content
        return ParseJsonArrayWithInferredType(arrayElement, length);
    }

    private static Type ResolveType(string assemblyQualifiedName)
    {
        Type targetType = Type.GetType(assemblyQualifiedName);
        if (targetType != null) return targetType;

        // Type not found - try to load from all loaded assemblies
        string typeName = assemblyQualifiedName.Split(',')[0].Trim();
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            targetType = assembly.GetType(typeName);
            if (targetType != null) return targetType;
        }

        return null;
    }

    private static object TryReconstructFromArray(JsonElement arrayElement, Type targetType)
    {
        try
        {
            int length = arrayElement.GetArrayLength();

            // Try array constructor patterns (double[] first for precision, then float[] for compatibility)
            Type[] arrayTypes = new[]
            {
                    // Floating point (double first for precision)
                    typeof(double[]), typeof(float[]), typeof(decimal[]),
                    // Signed integers (long first for range)
                    typeof(long[]), typeof(int[]), typeof(short[]), typeof(sbyte[]),
                    // Unsigned integers
                    typeof(ulong[]), typeof(uint[]), typeof(ushort[]), typeof(byte[]),
                    // Other primitives
                    typeof(bool[]), typeof(char[]),
                    // Common reference/value types
                    typeof(string[]), typeof(Guid[]), typeof(DateTime[]), typeof(DateTimeOffset[]), typeof(TimeSpan[])
                };

            foreach (Type arrayType in arrayTypes)
            {
                System.Reflection.ConstructorInfo ctor = targetType.GetConstructor(new[] { arrayType });
                if (ctor != null)
                {
                    Type elementType = arrayType.GetElementType();
                    Array array = Array.CreateInstance(elementType, length);
                    int index = 0;
                    foreach (JsonElement item in arrayElement.EnumerateArray())
                    {
                        object value = GetValue(item, null);
                        array.SetValue(ConvertToType(value, elementType), index);
                        index++;
                    }
                    return ctor.Invoke(new object[] { array });
                }
            }

            // Try ReadOnlyMemory<T> constructor patterns (double first for precision)
            Type[] memoryTypes = new[]
            {
                    typeof(ReadOnlyMemory<double>), typeof(ReadOnlyMemory<float>),
                    typeof(ReadOnlyMemory<long>), typeof(ReadOnlyMemory<int>), typeof(ReadOnlyMemory<short>), typeof(ReadOnlyMemory<sbyte>),
                    typeof(ReadOnlyMemory<ulong>), typeof(ReadOnlyMemory<uint>), typeof(ReadOnlyMemory<ushort>), typeof(ReadOnlyMemory<byte>),
                    typeof(ReadOnlyMemory<bool>), typeof(ReadOnlyMemory<char>)
                };

            foreach (Type memoryType in memoryTypes)
            {
                System.Reflection.ConstructorInfo ctor = targetType.GetConstructor(new[] { memoryType });
                if (ctor != null)
                {
                    Type elementType = memoryType.GetGenericArguments()[0];
                    Array array = Array.CreateInstance(elementType, length);
                    int index = 0;
                    foreach (JsonElement item in arrayElement.EnumerateArray())
                    {
                        object value = GetValue(item, null);
                        array.SetValue(ConvertToType(value, elementType), index);
                        index++;
                    }

                    // Create ReadOnlyMemory<T> from the array
                    object memoryInstance = Activator.CreateInstance(memoryType, array);
                    return ctor.Invoke(new object[] { memoryInstance });
                }
            }
        }
        catch
        {
            // Reconstruction failed
        }

        return null;
    }

    private static object ParseJsonArrayWithInferredType(JsonElement arrayElement, int length)
    {
        if (length == 0)
        {
            return new object[0];
        }

        // Peek at the first element to determine type
        JsonElement firstElement = arrayElement[0];

        switch (firstElement.ValueKind)
        {
            case JsonValueKind.Number:
                // Default to double[] for numeric arrays (preserves precision better than float[])
                double[] doubleResult = new double[length];
                int doubleIdx = 0;
                foreach (JsonElement item in arrayElement.EnumerateArray())
                {
                    doubleResult[doubleIdx++] = item.GetDouble();
                }
                return doubleResult;

            case JsonValueKind.String:
                string[] stringResult = new string[length];
                int strIdx = 0;
                foreach (JsonElement item in arrayElement.EnumerateArray())
                {
                    stringResult[strIdx++] = item.GetString();
                }
                return stringResult;

            case JsonValueKind.True:
            case JsonValueKind.False:
                bool[] boolResult = new bool[length];
                int boolIdx = 0;
                foreach (JsonElement item in arrayElement.EnumerateArray())
                {
                    boolResult[boolIdx++] = item.GetBoolean();
                }
                return boolResult;

            default:
                // Fallback to object[]
                object[] result = new object[length];
                int idx = 0;
                foreach (JsonElement item in arrayElement.EnumerateArray())
                {
                    result[idx++] = GetValue(item, null);
                }
                return result;
        }
    }

    private static object ConvertToType(object value, Type targetType)
    {
        if (value == null) return null;

        if (targetType == typeof(float))
            return Convert.ToSingle(value);
        if (targetType == typeof(double))
            return Convert.ToDouble(value);
        if (targetType == typeof(decimal))
            return Convert.ToDecimal(value);
        if (targetType == typeof(int) || targetType == typeof(Int32))
            return Convert.ToInt32(value);
        if (targetType == typeof(long) || targetType == typeof(Int64))
            return Convert.ToInt64(value);
        if (targetType == typeof(short) || targetType == typeof(Int16))
            return Convert.ToInt16(value);
        if (targetType == typeof(uint) || targetType == typeof(UInt32))
            return Convert.ToUInt32(value);
        if (targetType == typeof(ulong) || targetType == typeof(UInt64))
            return Convert.ToUInt64(value);
        if (targetType == typeof(ushort) || targetType == typeof(UInt16))
            return Convert.ToUInt16(value);
        if (targetType == typeof(byte))
            return Convert.ToByte(value);
        if (targetType == typeof(sbyte))
            return Convert.ToSByte(value);
        if (targetType == typeof(bool))
            return Convert.ToBoolean(value);
        if (targetType == typeof(string))
            return Convert.ToString(value);
        if (targetType == typeof(Guid))
        {
            if (value is Guid guidVal)
                return guidVal;
            return Guid.Parse(value.ToString());
        }
        if (targetType == typeof(DateTime))
        {
            if (value is DateTime dateTimeVal)
                return dateTimeVal;
            return DateTime.Parse(value.ToString());
        }
        if (targetType == typeof(DateTimeOffset))
        {
            if (value is DateTimeOffset dateTimeOffsetVal)
                return dateTimeOffsetVal;
            return DateTimeOffset.Parse(value.ToString());
        }
        if (targetType == typeof(TimeSpan))
        {
            if (value is TimeSpan timeSpanVal)
                return timeSpanVal;
            return TimeSpan.Parse(value.ToString());
        }
        if (targetType == typeof(char))
        {
            if (value is char charVal)
                return charVal;
            string strVal = value.ToString();
            return strVal.Length > 0 ? strVal[0] : '\0';
        }

        return Convert.ChangeType(value, targetType);
    }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
}
public static class MarkdownConverter
{
    /// <summary>
    /// Newline character to use in all markdown conversion operations.
    /// Defaults to Environment.NewLine.
    /// </summary>
    public static string NewlineCharacter { get; set; } = Environment.NewLine;

    /// <summary>
    /// Convert an entire DataTable to markdown format.
    /// </summary>
    /// <param name="dt">The DataTable to convert.</param>
    /// <returns>Markdown formatted string representation of the table, or null if no columns are defined.</returns>
    public static string Convert(DataTable dt)
    {
        if (dt == null || dt.Columns.Count == 0)
        {
            return null;
        }

        StringBuilder sb = new StringBuilder();

        // Add table name as header if available
        if (!string.IsNullOrEmpty(dt.TableName))
        {
            sb.Append($"# {dt.TableName}");
            sb.Append(NewlineCharacter);
            sb.Append(NewlineCharacter);
        }

        // Add headers
        sb.Append(ConvertHeaders(dt));

        // Add rows
        foreach (string rowMarkdown in IterateRows(dt))
        {
            sb.Append(rowMarkdown);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Convert an entire SerializableDataTable to markdown format.
    /// </summary>
    /// <param name="dt">The SerializableDataTable to convert.</param>
    /// <returns>Markdown formatted string representation of the table, or null if no columns are defined.</returns>
    public static string Convert(SerializableDataTable dt)
    {
        if (dt == null || dt.Columns == null || dt.Columns.Count == 0)
        {
            return null;
        }

        StringBuilder sb = new StringBuilder();

        // Add table name as header if available
        if (!string.IsNullOrEmpty(dt.Name))
        {
            sb.Append($"# {dt.Name}");
            sb.Append(NewlineCharacter);
            sb.Append(NewlineCharacter);
        }

        // Add headers
        sb.Append(ConvertHeaders(dt));

        // Add rows
        foreach (string rowMarkdown in IterateRows(dt))
        {
            sb.Append(rowMarkdown);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Convert just the headers of a DataTable to markdown format.
    /// </summary>
    /// <param name="dt">The DataTable whose headers to convert.</param>
    /// <returns>Markdown formatted string representation of the table headers.</returns>
    public static string ConvertHeaders(DataTable dt)
    {
        if (dt == null || dt.Columns.Count == 0)
        {
            return null;
        }

        StringBuilder sb = new StringBuilder();

        // Build header row
        foreach (DataColumn col in dt.Columns)
        {
            sb.Append("| ");
            sb.Append(col.ColumnName);
            sb.Append(" ");
        }
        sb.Append("|");
        sb.Append(NewlineCharacter);

        // Build separator row
        foreach (DataColumn col in dt.Columns)
        {
            sb.Append("|---");
        }
        sb.Append("|");
        sb.Append(NewlineCharacter);

        return sb.ToString();
    }

    /// <summary>
    /// Convert just the headers of a SerializableDataTable to markdown format.
    /// </summary>
    /// <param name="dt">The SerializableDataTable whose headers to convert.</param>
    /// <returns>Markdown formatted string representation of the table headers.</returns>
    public static string ConvertHeaders(SerializableDataTable dt)
    {
        if (dt == null || dt.Columns == null || dt.Columns.Count == 0)
        {
            return null;
        }

        StringBuilder sb = new StringBuilder();

        // Build header row
        foreach (SerializableColumn col in dt.Columns)
        {
            sb.Append("| ");
            sb.Append(col.Name);
            sb.Append(" ");
        }
        sb.Append("|");
        sb.Append(NewlineCharacter);

        // Build separator row
        foreach (SerializableColumn col in dt.Columns)
        {
            sb.Append("|---");
        }
        sb.Append("|");
        sb.Append(NewlineCharacter);

        return sb.ToString();
    }

    /// <summary>
    /// Convert a specific row of a DataTable to markdown format.
    /// </summary>
    /// <param name="dt">The DataTable containing the row.</param>
    /// <param name="rowNumber">Zero-based index of the row to convert.</param>
    /// <returns>Markdown formatted string representation of the row.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dt is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when rowNumber is outside the bounds of the rows collection.</exception>
    public static string ConvertRow(DataTable dt, int rowNumber)
    {
        if (dt == null)
        {
            throw new ArgumentNullException(nameof(dt));
        }

        if (rowNumber < 0 || rowNumber >= dt.Rows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rowNumber), "Row number is outside the bounds of the rows collection.");
        }

        StringBuilder sb = new StringBuilder();
        DataRow row = dt.Rows[rowNumber];

        foreach (DataColumn col in dt.Columns)
        {
            sb.Append("| ");

            object value = row[col];

            if (value == null || value == DBNull.Value)
            {
                sb.Append("");
            }
            else if (col.DataType == typeof(byte[]))
            {
                // For byte arrays, convert to Base64
                if (value is byte[] byteArray)
                {
                    sb.Append(System.Convert.ToBase64String(byteArray));
                }
                else
                {
                    sb.Append(value.ToString());
                }
            }
            else if (col.DataType == typeof(object) || value.GetType().IsArray)
            {
                // For Object types and arrays, output JSON
                sb.Append(FormatComplexValue(value));
            }
            else
            {
                // Format DateTime and DateTimeOffset with microsecond precision
                if (value is DateTime dateTimeVal)
                {
                    sb.Append(dateTimeVal.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
                }
                else if (value is DateTimeOffset dateTimeOffsetVal)
                {
                    sb.Append(dateTimeOffsetVal.ToString("yyyy-MM-dd HH:mm:ss.ffffff zzz"));
                }
                else
                {
                    // Escape pipe characters in data to avoid breaking markdown table format
                    string displayValue = value.ToString().Replace("|", "\\|");
                    sb.Append(displayValue);
                }
            }

            sb.Append(" ");
        }

        sb.Append("|");
        sb.Append(NewlineCharacter);

        return sb.ToString();
    }

    /// <summary>
    /// Convert a specific row of a SerializableDataTable to markdown format.
    /// </summary>
    /// <param name="dt">The SerializableDataTable containing the row.</param>
    /// <param name="rowNumber">Zero-based index of the row to convert.</param>
    /// <returns>Markdown formatted string representation of the row.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dt is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when rowNumber is outside the bounds of the rows collection.</exception>
    public static string ConvertRow(SerializableDataTable dt, int rowNumber)
    {
        if (dt == null)
        {
            throw new ArgumentNullException(nameof(dt));
        }

        if (dt.Rows == null)
        {
            throw new ArgumentNullException(nameof(dt.Rows));
        }

        if (rowNumber < 0 || rowNumber >= dt.Rows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rowNumber), "Row number is outside the bounds of the rows collection.");
        }

        StringBuilder sb = new StringBuilder();
        Dictionary<string, object> row = dt.Rows[rowNumber];

        foreach (SerializableColumn col in dt.Columns)
        {
            sb.Append("| ");

            if (row.TryGetValue(col.Name, out object value))
            {
                if (value == null)
                {
                    sb.Append("");
                }
                else if (col.Type == ColumnValueTypeEnum.ByteArray)
                {
                    // For byte arrays, convert to Base64
                    if (value is byte[] byteArray)
                    {
                        sb.Append(System.Convert.ToBase64String(byteArray));
                    }
                    else
                    {
                        sb.Append(value.ToString());
                    }
                }
                else if (col.Type == ColumnValueTypeEnum.Object || value.GetType().IsArray)
                {
                    // For Object types and arrays, output JSON
                    sb.Append(FormatComplexValue(value));
                }
                else
                {
                    // Format DateTime and DateTimeOffset with microsecond precision
                    if (value is DateTime dateTimeVal)
                    {
                        sb.Append(dateTimeVal.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
                    }
                    else if (value is DateTimeOffset dateTimeOffsetVal)
                    {
                        sb.Append(dateTimeOffsetVal.ToString("yyyy-MM-dd HH:mm:ss.ffffff zzz"));
                    }
                    else
                    {
                        // Escape pipe characters in data to avoid breaking markdown table format
                        string displayValue = value.ToString().Replace("|", "\\|");
                        sb.Append(displayValue);
                    }
                }
            }
            else
            {
                sb.Append("");
            }

            sb.Append(" ");
        }

        sb.Append("|");
        sb.Append(NewlineCharacter);

        return sb.ToString();
    }

    /// <summary>
    /// Iterate through rows of a DataTable, yielding the markdown representation of each row.
    /// </summary>
    /// <param name="dt">The DataTable to iterate.</param>
    /// <returns>IEnumerable of strings, each representing a row in markdown format.</returns>
    public static IEnumerable<string> IterateRows(DataTable dt)
    {
        if (dt == null || dt.Rows.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < dt.Rows.Count; i++)
        {
            yield return ConvertRow(dt, i);
        }
    }

    /// <summary>
    /// Iterate through rows of a SerializableDataTable, yielding the markdown representation of each row.
    /// </summary>
    /// <param name="dt">The SerializableDataTable to iterate.</param>
    /// <returns>IEnumerable of strings, each representing a row in markdown format.</returns>
    public static IEnumerable<string> IterateRows(SerializableDataTable dt)
    {
        if (dt == null || dt.Rows == null || dt.Rows.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < dt.Rows.Count; i++)
        {
            yield return ConvertRow(dt, i);
        }
    }

    private static string FormatComplexValue(object value)
    {
        if (value == null)
        {
            return "";
        }

        try
        {
            // For arrays, format as compact JSON array
            if (value.GetType().IsArray)
            {
                Array arr = (Array)value;
                StringBuilder arraySb = new StringBuilder();
                arraySb.Append("[");

                for (int i = 0; i < arr.Length; i++)
                {
                    if (i > 0)
                    {
                        arraySb.Append(",");
                    }

                    object element = arr.GetValue(i);
                    if (element == null)
                    {
                        arraySb.Append("null");
                    }
                    else if (element is string strElement)
                    {
                        arraySb.Append("\"");
                        arraySb.Append(strElement.Replace("\"", "\\\""));
                        arraySb.Append("\"");
                    }
                    else if (element is bool boolElement)
                    {
                        arraySb.Append(boolElement ? "true" : "false");
                    }
                    else
                    {
                        arraySb.Append(element.ToString());
                    }
                }

                arraySb.Append("]");
                return arraySb.ToString();
            }

            // For other complex types, serialize as JSON
            string json = JsonSerializer.Serialize(value);
            return json;
        }
        catch
        {
            return value.ToString();
        }
    }
}
