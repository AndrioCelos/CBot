using System.Data.Common;

namespace Pokedex;
internal static class Extensions {
	public static string? GetString(this DbDataReader reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return reader.IsDBNull(columnIndex) ? null : reader.GetString(columnIndex);
	}
	public static T GetEnum<T>(this DbDataReader reader, string column) where T : Enum => (T) Enum.Parse(typeof(T), reader.GetString(column) ?? throw new InvalidCastException(column + " is null."), true);
	public static int GetInt32(this DbDataReader reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return (int) reader.GetInt64(columnIndex);
	}
	public static int? GetNullableInt32(this DbDataReader reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return reader.IsDBNull(columnIndex) ? null : (int) reader.GetInt64(columnIndex);
	}
	public static bool GetBool(this DbDataReader reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return reader.GetBoolean(columnIndex);
	}
	public static bool IsNull(this DbDataReader reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return reader.IsDBNull(columnIndex);
	}

	public static string? GetString(this DbDataRecord reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return reader.IsDBNull(columnIndex) ? null : reader.GetString(columnIndex);
	}
	public static int GetInt32(this DbDataRecord reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return (int) reader.GetInt64(columnIndex);
	}
	public static int? GetNullableInt32(this DbDataRecord reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return reader.IsDBNull(columnIndex) ? null : (int) reader.GetInt64(columnIndex);
	}
	public static bool GetBool(this DbDataRecord reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return reader.GetInt64(columnIndex) != 0;
	}
	public static bool IsNull(this DbDataRecord reader, string column) {
		var columnIndex = reader.GetOrdinal(column);
		return reader.IsDBNull(columnIndex);
	}
}
