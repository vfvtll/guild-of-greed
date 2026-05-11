using System;
using Microsoft.Data.Sqlite;

namespace GuildOfGreed.Server.Db;

// Применяет все недостающие миграции из Migrations.All. Идемпотентен:
// повторный запуск ничего не делает если все миграции уже на месте.
//
// Поток:
//   1. Создаём schema_version (если нет).
//   2. Читаем максимальную применённую версию (0 если таблица пустая).
//   3. Для каждой миграции с Version > current — в транзакции:
//      - Запускаем Sql.
//      - INSERT в schema_version.
//      - Commit.
//
// Если миграция упала — транзакция откатывается, сервер крашится.
// Это намеренно: лучше остановиться, чем работать с полу-применённой схемой.
public static class MigrationRunner
{
	public static void Apply(SqliteConnection conn)
	{
		EnsureSchemaVersionTable(conn);
		int current = GetCurrentVersion(conn);

		foreach (var m in Migrations.All)
		{
			if (m.Version <= current) continue;
			ApplyOne(conn, m);
			Logger.Info($"db migration v{m.Version} applied: {m.Description}");
		}
	}

	private static void ApplyOne(SqliteConnection conn, Migration m)
	{
		using var tx = conn.BeginTransaction();
		try
		{
			using (var cmd = conn.CreateCommand())
			{
				cmd.Transaction = tx;
				cmd.CommandText = m.Sql;
				cmd.ExecuteNonQuery();
			}
			using (var insert = conn.CreateCommand())
			{
				insert.Transaction = tx;
				insert.CommandText = """
					INSERT INTO schema_version (version, applied_at, description)
					VALUES ($v, $t, $d)
				""";
				insert.Parameters.AddWithValue("$v", m.Version);
				insert.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
				insert.Parameters.AddWithValue("$d", m.Description ?? "");
				insert.ExecuteNonQuery();
			}
			tx.Commit();
		}
		catch
		{
			tx.Rollback();
			throw;
		}
	}

	private static void EnsureSchemaVersionTable(SqliteConnection conn)
	{
		using var cmd = conn.CreateCommand();
		cmd.CommandText = """
			CREATE TABLE IF NOT EXISTS schema_version (
				version INTEGER PRIMARY KEY,
				applied_at INTEGER NOT NULL,
				description TEXT NOT NULL
			)
		""";
		cmd.ExecuteNonQuery();
	}

	private static int GetCurrentVersion(SqliteConnection conn)
	{
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version";
		var result = cmd.ExecuteScalar();
		return result is long l ? (int)l : 0;
	}
}
