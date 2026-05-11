using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
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
		}.ToString();
		_conn = new SqliteConnection(connStr);
		_conn.Open();
		EnsureSchema();
	}

	private void EnsureSchema()
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = """
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
		""";
		cmd.ExecuteNonQuery();
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

	public enum CreateCharResult { Ok, SlotsFull, InvalidStats }

	public CreateCharResult CreateCharacter(Guid accountId, CharacterData ch, out Guid characterId)
	{
		characterId = Guid.Empty;
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

	public List<CharacterData> ListCharacters(Guid accountId)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT character_json FROM characters WHERE account_id = $a";
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
		cmd.CommandText = "SELECT character_json FROM characters WHERE id = $id AND account_id = $a";
		cmd.Parameters.AddWithValue("$id", characterId.ToByteArray());
		cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
		using var r = cmd.ExecuteReader();
		if (!r.Read()) return null;
		return JsonSerializer.Deserialize<CharacterData>(r.GetString(0), _jsonOptions);
	}

	public bool DeleteCharacter(Guid accountId, Guid characterId)
	{
		lock (_lock)
		{
			using var cmd = _conn.CreateCommand();
			cmd.CommandText = "DELETE FROM characters WHERE id = $id AND account_id = $a";
			cmd.Parameters.AddWithValue("$id", characterId.ToByteArray());
			cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
			return cmd.ExecuteNonQuery() > 0;
		}
	}

	private int CountCharacters(Guid accountId)
	{
		using var cmd = _conn.CreateCommand();
		cmd.CommandText = "SELECT COUNT(*) FROM characters WHERE account_id = $a";
		cmd.Parameters.AddWithValue("$a", accountId.ToByteArray());
		return (int)(long)cmd.ExecuteScalar();
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

	private static bool IsValidCharStats(CharacterData ch)
	{
		if (string.IsNullOrWhiteSpace(ch.CharacterName)) return false;
		// 6 статов суммарно от 6*35=210 до 6*45+10бонус=280.
		int sum = ch.Str + ch.Int + ch.Con + ch.Wit + ch.Men + ch.Dex;
		return sum >= 210 && sum <= 280;
	}

	public void Dispose() => _conn?.Dispose();
}
