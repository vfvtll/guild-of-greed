using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using GuildOfGreed.Server.Db;
using GuildOfGreed.Shared.Auth;
using GuildOfGreed.Shared.Crypto;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Server;

// Серверное хранилище аккаунтов и персонажей в SQLite.
//
// Схема (см. EnsureSchema):
//   accounts  (id BLOB PK, login TEXT UNIQUE, email TEXT UNIQUE,
//              password_hash TEXT, created_at INTEGER)
//   sessions  (token TEXT PK, account_id BLOB FK, issued_at INTEGER, expires_at INTEGER)
//   characters(id BLOB PK, account_id BLOB FK, character_json TEXT)
//
// CharacterData сериализуется в JSON: схема персонажа меняется быстро,
// нормализация в столбцы потребует частых миграций. Зафиксируем колонки,
// если придётся часто запрашивать по полю (имя, level и т.п.).
public class AccountStore : IDisposable
{
	private const int MaxCharactersPerAccount = 7;
	private const int SessionLifetimeDays = 30;

	private readonly SqliteConnection _conn;
	private readonly object _lock = new();
	private readonly JsonSerializerOptions _jsonOptions = new() { IncludeFields = true };

	public AccountStore(string databasePath)
	{
		var connStr = new SqliteConnectionStringBuilder
		{
			DataSource = databasePath,
			Mode = SqliteOpenMode.ReadWriteCreate,
			ForeignKeys = true,   // включает ON DELETE CASCADE из схемы.
		}.ToString();
		_conn = new SqliteConnection(connStr);
		_conn.Open();
		MigrationRunner.Apply(_conn);
	}

	// === Accounts =============================================================

	public enum CreateResult { Ok, LoginTaken, EmailTaken, WeakPassword, InvalidLogin, InvalidEmail }

	public CreateResult CreateAccount(string login, string email, string password, out Account created)
	{
		created = null;
		if (!IsValidLogin(login))    return CreateResult.InvalidLogin;
		if (!IsValidEmail(email))    return CreateResult.InvalidEmail;
		if (!IsStrongEnough(password)) return CreateResult.WeakPassword;

		lock (_lock)
		{
			if (FindByLogin(login) != null) return CreateResult.LoginTaken;
			if (FindByEmail(email) != null) return CreateResult.EmailTaken;

			var account = new Account
			{
				Id = Guid.NewGuid(),
				Login = login,
				Email = email,
				PasswordHash = PasswordHasher.Hash(password),
				CreatedAt = DateTime.UtcNow,
			};

			using var cmd = _conn.CreateCommand();
			cmd.CommandText = """
				INSERT INTO accounts (id, login, email, password_hash, created_at)
				VALUES ($id, $login, $email, $hash, $created)
			""";
			cmd.Parameters.AddWithValue("$id", account.Id.ToByteArray());
			cmd.Parameters.AddWithValue("$login", account.Login);
			cmd.Parameters.AddWithValue("$email", account.Email);
			cmd.Parameters.AddWithValue("$hash", account.PasswordHash);
			cmd.Parameters.AddWithValue("$created", new DateTimeOffset(account.CreatedAt).ToUnixTimeSeconds());
			cmd.ExecuteNonQuery();

			created = account;
			return CreateResult.Ok;
		}
	}

	public Account FindByLogin(string login)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT id, login, email, password_hash, created_at FROM accounts WHERE login = $login";
		cmd.Parameters.AddWithValue("$login", login);
		return ReadAccount(cmd);
	}

	public Account FindByEmail(string email)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT id, login, email, password_hash, created_at FROM accounts WHERE email = $email";
		cmd.Parameters.AddWithValue("$email", email);
		return ReadAccount(cmd);
	}

	public Account FindById(Guid id)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT id, login, email, password_hash, created_at FROM accounts WHERE id = $id";
		cmd.Parameters.AddWithValue("$id", id.ToByteArray());
		return ReadAccount(cmd);
	}

	private static Account ReadAccount(SqliteCommand cmd)
	{
		using var r = cmd.ExecuteReader();
		if (!r.Read()) return null;
		return new Account
		{
			Id = new Guid((byte[])r["id"]),
			Login = r.GetString(1),
			Email = r.GetString(2),
			PasswordHash = r.GetString(3),
			CreatedAt = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(4)).UtcDateTime,
		};
	}

	// === Sessions =============================================================

	public AccountSession IssueSession(Guid accountId)
	{
		var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
			.Replace("+", "-").Replace("/", "_").TrimEnd('=');
		var now = DateTime.UtcNow;
		var session = new AccountSession
		{
			Token = token,
			AccountId = accountId,
			IssuedAt = now,
			ExpiresAt = now.AddDays(SessionLifetimeDays),
		};
		lock (_lock)
		{
			using var cmd = _conn.CreateCommand();
			cmd.CommandText = """
				INSERT INTO sessions (token, account_id, issued_at, expires_at)
				VALUES ($t, $a, $i, $e)
			""";
			cmd.Parameters.AddWithValue("$t", token);
			cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
			cmd.Parameters.AddWithValue("$i", new DateTimeOffset(session.IssuedAt).ToUnixTimeSeconds());
			cmd.Parameters.AddWithValue("$e", new DateTimeOffset(session.ExpiresAt).ToUnixTimeSeconds());
			cmd.ExecuteNonQuery();
		}
		return session;
	}

	public AccountSession FindSession(string token)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT account_id, issued_at, expires_at FROM sessions WHERE token = $t";
		cmd.Parameters.AddWithValue("$t", token);
		using var r = cmd.ExecuteReader();
		if (!r.Read()) return null;
		var s = new AccountSession
		{
			Token = token,
			AccountId = new Guid((byte[])r["account_id"]),
			IssuedAt = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(1)).UtcDateTime,
			ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(2)).UtcDateTime,
		};
		if (s.ExpiresAt <= DateTime.UtcNow)
		{
			DeleteSession(token);
			return null;
		}
		return s;
	}

	public void DeleteSession(string token)
	{
		lock (_lock)
		{
			using var cmd = _conn.CreateCommand();
			cmd.CommandText = "DELETE FROM sessions WHERE token = $t";
			cmd.Parameters.AddWithValue("$t", token);
			cmd.ExecuteNonQuery();
		}
	}

	// === Characters ===========================================================

	public enum CreateCharResult
	{
		Ok,
		SlotsFull,
		InvalidStats,
		NameTooShort,
		NameTooLong,
		NameBadChars,
		NameReserved,
		NameProfanity,
	}

	public CreateCharResult CreateCharacter(Guid accountId, CharacterData ch, out Guid characterId)
	{
		characterId = Guid.Empty;
		var nameResult = ValidateCharacterName(ch.CharacterName);
		if (nameResult != CreateCharResult.Ok) return nameResult;
		if (!IsValidCharStats(ch)) return CreateCharResult.InvalidStats;

		lock (_lock)
		{
			if (CountCharacters(accountId) >= MaxCharactersPerAccount) return CreateCharResult.SlotsFull;

			if (ch.Id == Guid.Empty) ch.Id = Guid.NewGuid();
			characterId = ch.Id;

			using var cmd = _conn.CreateCommand();
			cmd.CommandText = """
				INSERT INTO characters (id, account_id, character_json)
				VALUES ($id, $a, $j)
			""";
			cmd.Parameters.AddWithValue("$id", characterId.ToByteArray());
			cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
			cmd.Parameters.AddWithValue("$j", JsonSerializer.Serialize(ch, _jsonOptions));
			cmd.ExecuteNonQuery();
			return CreateCharResult.Ok;
		}
	}

	// Все методы чтения персонажей фильтруют deleted_at IS NULL — soft-deleted
	// записи в API не видны, но физически в БД остаются для возможного restore.
	public List<CharacterData> ListCharacters(Guid accountId)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT character_json FROM characters WHERE account_id = $a AND deleted_at IS NULL";
		cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
		var list = new List<CharacterData>();
		using var r = cmd.ExecuteReader();
		while (r.Read())
		{
			var ch = JsonSerializer.Deserialize<CharacterData>(r.GetString(0), _jsonOptions);
			if (ch != null) list.Add(ch);
		}
		return list;
	}

	public CharacterData LoadCharacter(Guid accountId, Guid characterId)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT character_json FROM characters WHERE id = $id AND account_id = $a AND deleted_at IS NULL";
		cmd.Parameters.AddWithValue("$id", characterId.ToByteArray());
		cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
		using var r = cmd.ExecuteReader();
		if (!r.Read()) return null;
		var ch = JsonSerializer.Deserialize<CharacterData>(r.GetString(0), _jsonOptions);
		// Старые сейвы могут не иметь BaseXxx — заполняем «нулевую» базу
		// текущими статами, чтобы респек у такого персонажа ничего не вернул.
		ch?.EnsureBaseStats();
		return ch;
	}

	public enum DeleteCharResult { Ok, NotFound, RateLimited }

	// Cooldown: не больше N удалений в час с одного аккаунта. Защита от
	// случайного «удалю всех персонажей» и от автоматических спам-скриптов.
	private const int MaxDeletionsPerHour = 3;
	private static readonly System.TimeSpan DeletionWindow = System.TimeSpan.FromHours(1);

	// Soft-delete: ставит deleted_at. Запись физически остаётся в БД, чтобы
	// при необходимости вручную восстановить через UPDATE deleted_at = NULL
	// (в течение 7 дней; на cleanup-таску — отдельная задача).
	public DeleteCharResult DeleteCharacter(Guid accountId, Guid characterId)
	{
		lock (_lock)
		{
			if (CountRecentDeletions(accountId) >= MaxDeletionsPerHour)
				return DeleteCharResult.RateLimited;

			using var cmd = _conn.CreateCommand();
			cmd.CommandText = """
				UPDATE characters SET deleted_at = $t
				WHERE id = $id AND account_id = $a AND deleted_at IS NULL
			""";
			cmd.Parameters.AddWithValue("$t", System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
			cmd.Parameters.AddWithValue("$id", characterId.ToByteArray());
			cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
			int affected = cmd.ExecuteNonQuery();
			return affected > 0 ? DeleteCharResult.Ok : DeleteCharResult.NotFound;
		}
	}

	private int CountRecentDeletions(Guid accountId)
	{
		long since = System.DateTimeOffset.UtcNow.Subtract(DeletionWindow).ToUnixTimeMilliseconds();
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = """
			SELECT COUNT(*) FROM characters
			WHERE account_id = $a AND deleted_at IS NOT NULL AND deleted_at >= $since
		""";
		cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
		cmd.Parameters.AddWithValue("$since", since);
		return (int)(long)cmd.ExecuteScalar();
	}

	// Сохраняет обновлённый CharacterData (HP/Inventory/Equipment/Effects).
	// Вызывается сервером после BattleEnded — это даёт persistence изменений
	// после боя между сессиями игрока.
	public bool UpdateCharacter(Guid accountId, CharacterData ch)
	{
		if (ch == null) return false;
		lock (_lock)
		{
			using var cmd = _conn.CreateCommand();
			cmd.CommandText = """
				UPDATE characters SET character_json = $j
				WHERE id = $id AND account_id = $a
			""";
			cmd.Parameters.AddWithValue("$j", JsonSerializer.Serialize(ch, _jsonOptions));
			cmd.Parameters.AddWithValue("$id", ch.Id.ToByteArray());
			cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
			return cmd.ExecuteNonQuery() > 0;
		}
	}

	// Лимит слотов считаем только по живым — soft-deleted не блокируют
	// создание новых.
	private int CountCharacters(Guid accountId)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT COUNT(*) FROM characters WHERE account_id = $a AND deleted_at IS NULL";
		cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
		return (int)(long)cmd.ExecuteScalar();
	}

	// === Active battle persistence ============================================
	// Snapshot активного боя — UPSERT по character_id. Запись пишется после
	// каждого BattleAction и удаляется при ended.

	public void SaveActiveBattle(Guid characterId, string snapshotJson)
	{
		lock (_lock)
		{
			using var cmd = _conn.CreateCommand();
			cmd.CommandText = """
				INSERT INTO active_battles (character_id, snapshot_json, updated_at)
				VALUES ($id, $j, $t)
				ON CONFLICT(character_id) DO UPDATE SET
					snapshot_json = excluded.snapshot_json,
					updated_at = excluded.updated_at
			""";
			cmd.Parameters.AddWithValue("$id", characterId.ToByteArray());
			cmd.Parameters.AddWithValue("$j", snapshotJson);
			cmd.Parameters.AddWithValue("$t", System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
			cmd.ExecuteNonQuery();
		}
	}

	public string LoadActiveBattle(Guid characterId)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT snapshot_json FROM active_battles WHERE character_id = $id";
		cmd.Parameters.AddWithValue("$id", characterId.ToByteArray());
		using var r = cmd.ExecuteReader();
		return r.Read() ? r.GetString(0) : null;
	}

	public void ClearActiveBattle(Guid characterId)
	{
		lock (_lock)
		{
			using var cmd = _conn.CreateCommand();
			cmd.CommandText = "DELETE FROM active_battles WHERE character_id = $id";
			cmd.Parameters.AddWithValue("$id", characterId.ToByteArray());
			cmd.ExecuteNonQuery();
		}
	}

	// === Validation ===========================================================

	private static bool IsValidLogin(string login)
		=> !string.IsNullOrWhiteSpace(login) && login.Length is >= 3 and <= 24;

	private static bool IsValidEmail(string email)
	{
		if (string.IsNullOrWhiteSpace(email)) return false;
		int at = email.IndexOf('@');
		return at > 0 && at < email.Length - 1 && email.IndexOf('.', at) > 0;
	}

	private static bool IsStrongEnough(string password)
		=> !string.IsNullOrEmpty(password) && password.Length >= 6;

	// Стартовая валидация: BaseXxx — рандом в [35..45]; Stat ≥ Base
	// (нельзя вычитать); ровно 10 распределённых очков, по стату ≤ +10.
	private const int MinBase = 35;
	private const int MaxBase = 45;
	private const int PointsToDistribute = 10;

	private static bool IsValidCharStats(CharacterData ch)
	{
		(int s, int b)[] pairs =
		{
			(ch.Str, ch.BaseStr),
			(ch.Int, ch.BaseInt),
			(ch.Con, ch.BaseCon),
			(ch.Wit, ch.BaseWit),
			(ch.Men, ch.BaseMen),
			(ch.Dex, ch.BaseDex),
		};
		int spent = 0;
		foreach (var (s, b) in pairs)
		{
			if (b < MinBase || b > MaxBase) return false;
			int delta = s - b;
			if (delta < 0 || delta > PointsToDistribute) return false;
			spent += delta;
		}
		return spent == PointsToDistribute;
	}

	// Валидация имени — pure-функция в shared/Domain/CharacterNameValidator.
	// Здесь только маппим статус в CreateCharResult.
	public static CreateCharResult ValidateCharacterName(string rawName)
		=> CharacterNameValidator.Validate(rawName) switch
		{
			CharNameStatus.Ok        => CreateCharResult.Ok,
			CharNameStatus.TooShort  => CreateCharResult.NameTooShort,
			CharNameStatus.TooLong   => CreateCharResult.NameTooLong,
			CharNameStatus.BadChars  => CreateCharResult.NameBadChars,
			CharNameStatus.Reserved  => CreateCharResult.NameReserved,
			CharNameStatus.Profanity => CreateCharResult.NameProfanity,
			_ => CreateCharResult.NameBadChars,
		};

	public void Dispose() => _conn?.Dispose();
}
