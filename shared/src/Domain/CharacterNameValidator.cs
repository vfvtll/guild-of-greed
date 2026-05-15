using System;
using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

public enum CharNameStatus
{
	Ok,
	TooShort,
	TooLong,
	BadChars,
	Reserved,
	Profanity,
}

// Валидация имени персонажа. Pure-функция, используется и сервером (отказ
// в CreateCharacter), и клиентом (real-time подсказка под полем ввода).
// Серверная валидация — авторитетная; клиентская только для UX.
public static class CharacterNameValidator
{
	public const int MinLen = 3;
	public const int MaxLen = 20;

	private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"admin", "administrator", "gm", "moderator", "mod", "system", "server",
		"root", "support", "staff", "official", "anonymous", "null", "undefined",
		"авантюрист",
		"гм", "админ", "модератор", "система",
	};

	private static readonly string[] ReservedPrefixes =
	{
		"gm_", "gm-", "admin_", "admin-", "mod_", "mod-",
		"гм_", "гм-", "админ_", "админ-",
	};

	private static readonly string[] ProfanityWords =
	{
		"fuck", "shit", "bitch", "cunt", "asshole", "nigger", "faggot",
		"хуй", "пизд", "ебан", "ебать", "блядь", "сука", "пидор", "пидар",
	};

	public static CharNameStatus Validate(string rawName)
	{
		if (string.IsNullOrWhiteSpace(rawName)) return CharNameStatus.TooShort;
		string name = rawName.Trim();
		if (name.Length < MinLen) return CharNameStatus.TooShort;
		if (name.Length > MaxLen) return CharNameStatus.TooLong;

		bool prevSpace = false;
		foreach (char c in name)
		{
			if (char.IsLetter(c) || char.IsDigit(c) || c == '-' || c == '_')
			{
				prevSpace = false;
				continue;
			}
			if (c == ' ')
			{
				if (prevSpace) return CharNameStatus.BadChars;
				prevSpace = true;
				continue;
			}
			return CharNameStatus.BadChars;
		}

		string lower = name.ToLowerInvariant();
		if (ReservedNames.Contains(lower)) return CharNameStatus.Reserved;
		foreach (string p in ReservedPrefixes)
			if (lower.StartsWith(p, StringComparison.Ordinal)) return CharNameStatus.Reserved;

		foreach (string bad in ProfanityWords)
			if (lower.Contains(bad, StringComparison.Ordinal)) return CharNameStatus.Profanity;

		return CharNameStatus.Ok;
	}

	// Человекочитаемое сообщение для подсказки под полем ввода.
	// Локаль ru — текущий default. Если придётся локализовать — переедет в Lang.cs.
	public static string Hint(CharNameStatus status) => status switch
	{
		CharNameStatus.Ok        => "",
		CharNameStatus.TooShort  => $"Минимум {MinLen} символа",
		CharNameStatus.TooLong   => $"Максимум {MaxLen} символов",
		CharNameStatus.BadChars  => "Только буквы, цифры, '-', '_' и одиночные пробелы",
		CharNameStatus.Reserved  => "Это имя зарезервировано",
		CharNameStatus.Profanity => "Имя содержит запрещённые слова",
		_ => "",
	};
}
