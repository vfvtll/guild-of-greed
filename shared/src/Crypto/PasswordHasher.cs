using System;
using System.Security.Cryptography;
using System.Text;

namespace GuildOfGreed.Shared.Crypto;

// PBKDF2 (HMAC-SHA256) хеширование паролей. Используется на сервере при
// регистрации и при проверке логина — клиент шлёт пароль открытым текстом
// по защищённому каналу (TLS), сервер хеширует с уникальной солью аккаунта.
//
// Формат строки хеша (хранится в Account.PasswordHash):
//   $pbkdf2-sha256$<iter>$<saltBase64>$<hashBase64>
//
// Iter подобран так, чтобы хеш одного пароля считался ~50ms на современном CPU
// (защита от brute-force при утечке БД). При апгрейде железа можно поднять
// iter и автоматически перехешировать при следующем логине пользователя.
public static class PasswordHasher
{
	private const int SaltSize = 16;     // 128 бит соли — стандарт.
	private const int HashSize = 32;     // SHA-256 → 32 байта.
	private const int Iterations = 100_000;
	private const string Marker = "pbkdf2-sha256";

	// Хеширует пароль и возвращает строку для записи в БД.
	public static string Hash(string password)
	{
		if (string.IsNullOrEmpty(password)) throw new ArgumentException("password is empty");

		byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
		byte[] hash = Derive(password, salt, Iterations);

		var sb = new StringBuilder();
		sb.Append('$').Append(Marker)
		  .Append('$').Append(Iterations)
		  .Append('$').Append(Convert.ToBase64String(salt))
		  .Append('$').Append(Convert.ToBase64String(hash));
		return sb.ToString();
	}

	// Сверяет пароль с записанным хешом. Возвращает false для любого
	// неконсистентного формата вместо throw — чтобы вызывающий код
	// не утекал детали ошибки в API-ответ.
	public static bool Verify(string password, string stored)
	{
		if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;
		var parts = stored.Split('$');
		if (parts.Length != 5) return false;
		if (parts[1] != Marker) return false;
		if (!int.TryParse(parts[2], out int iter)) return false;

		byte[] salt;
		byte[] expected;
		try
		{
			salt = Convert.FromBase64String(parts[3]);
			expected = Convert.FromBase64String(parts[4]);
		}
		catch (FormatException)
		{
			return false;
		}

		byte[] actual = Derive(password, salt, iter);
		return CryptographicOperations.FixedTimeEquals(actual, expected);
	}

	private static byte[] Derive(string password, byte[] salt, int iter)
	{
		using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iter, HashAlgorithmName.SHA256);
		return pbkdf2.GetBytes(HashSize);
	}
}
