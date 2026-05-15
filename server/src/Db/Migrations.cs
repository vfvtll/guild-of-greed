namespace GuildOfGreed.Server.Db;

// Все миграции схемы в хронологическом порядке. Запускаются автоматически
// MigrationRunner.Apply при старте сервера; каждая в своей транзакции,
// записывает себя в schema_version после успешного применения.
//
// Правила добавления:
//   - Новая миграция = новый объект в конце массива с Version = последний+1.
//     ВЕРСИИ НЕ ПЕРЕНУМЕРОВЫВАТЬ — раз применённая v3 на проде не передвинется.
//   - SQL внутри миграции запускается одним батчем (SQLite поддерживает
//     множество statements через ';' в одной CommandText).
//   - Любое изменение схемы (новый столбец, индекс, таблица, перенос данных) =
//     отдельная миграция. Не редактируй применённые миграции — добавляй новые.
public static class Migrations
{
	public static readonly Migration[] All =
	{
		new()
		{
			Version = 1,
			Description = "initial schema (accounts, sessions, characters)",
			Sql = """
				CREATE TABLE IF NOT EXISTS accounts (
					id BLOB PRIMARY KEY,
					login TEXT UNIQUE NOT NULL COLLATE NOCASE,
					email TEXT UNIQUE NOT NULL COLLATE NOCASE,
					password_hash TEXT NOT NULL,
					created_at INTEGER NOT NULL
				);
				CREATE TABLE IF NOT EXISTS sessions (
					token TEXT PRIMARY KEY,
					account_id BLOB NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
					issued_at INTEGER NOT NULL,
					expires_at INTEGER NOT NULL
				);
				CREATE TABLE IF NOT EXISTS characters (
					id BLOB PRIMARY KEY,
					account_id BLOB NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
					character_json TEXT NOT NULL
				);
				CREATE INDEX IF NOT EXISTS idx_characters_account ON characters(account_id);
				CREATE INDEX IF NOT EXISTS idx_sessions_account   ON sessions(account_id);
			""",
		},
		new()
		{
			Version = 2,
			Description = "soft-delete для characters (deleted_at)",
			// deleted_at IS NULL = жив. INTEGER (Unix ms) = soft-deleted.
			// Существующие записи остаются с NULL — никто не теряется.
			Sql = """
				ALTER TABLE characters ADD COLUMN deleted_at INTEGER;
				CREATE INDEX IF NOT EXISTS idx_characters_alive
					ON characters(account_id) WHERE deleted_at IS NULL;
			""",
		},
		new()
		{
			Version = 3,
			Description = "active_battles: persistence активного боя",
			// 1:1 на персонажа. Запись создаётся в HandleStartBattle и
			// обновляется после каждого BattleAction. Удаляется при ended.
			Sql = """
				CREATE TABLE IF NOT EXISTS active_battles (
					character_id BLOB PRIMARY KEY REFERENCES characters(id) ON DELETE CASCADE,
					snapshot_json TEXT NOT NULL,
					updated_at INTEGER NOT NULL
				);
			""",
		},
	};
}
