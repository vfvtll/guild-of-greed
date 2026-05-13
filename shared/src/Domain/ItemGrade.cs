using System;
using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Грейд предмета — L2-стиль (E низший, S высший). Определяет:
//   - базовые статы предмета (грейд D даёт больше, чем E, и т.д.);
//   - какие редкости вообще могут на нём катиться (см. AllowedRarities);
//   - какие сеты ему доступны (см. SetsDB в Инк. D).
//
// Сейчас на ArmorData/WeaponData поле Grade — строка ("E", "D", ...). Это
// сохраняется для совместимости со старыми сейвами. Парсим в enum только
// внутри расчётов, обратно сериализуем как строку.
public enum ItemGrade
{
	E = 0,
	D = 1,
	C = 2,
	B = 3,
	A = 4,
	S = 5,
}

public static class ItemGrades
{
	// Парсинг строкового кода грейда из сейва/конфига. Неизвестное значение —
	// E (consider it минимум, не упадём при чтении старых данных).
	public static ItemGrade Parse(string code)
	{
		if (string.IsNullOrEmpty(code)) return ItemGrade.E;
		return code.ToUpperInvariant() switch
		{
			"E" => ItemGrade.E,
			"D" => ItemGrade.D,
			"C" => ItemGrade.C,
			"B" => ItemGrade.B,
			"A" => ItemGrade.A,
			"S" => ItemGrade.S,
			_   => ItemGrade.E,
		};
	}

	public static string Code(ItemGrade g) => g.ToString();   // "E".."S"

	// Какие редкости могут выпасть на предмете данного грейда.
	// Согласовано 2026-05-12:
	//   E: Common, Uncommon
	//   D: + Rare
	//   C: + Heroic
	//   B: + Epic
	//   A: + Legendary
	//   S: все шесть (генератор отдаст предпочтение верху распределения)
	public static IReadOnlyList<ItemRarity> AllowedRarities(ItemGrade grade)
	{
		return _allowedByGrade[(int)grade];
	}

	public static bool IsRarityAllowed(ItemGrade grade, ItemRarity rarity)
	{
		var list = _allowedByGrade[(int)grade];
		foreach (var r in list) if (r == rarity) return true;
		return false;
	}

	private static readonly ItemRarity[][] _allowedByGrade =
	{
		// E
		new[] { ItemRarity.Common, ItemRarity.Uncommon },
		// D
		new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare },
		// C
		new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Heroic },
		// B
		new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Heroic, ItemRarity.Epic },
		// A
		new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Heroic, ItemRarity.Epic, ItemRarity.Legendary },
		// S
		new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Heroic, ItemRarity.Epic, ItemRarity.Legendary },
	};
}

// Сводка по схеме аффиксов одного предмета. Используется генератором
// (Инк. C) и UI (Инк. F). Хранится не как поле — каждый раз derive от Rarity.
//
// Согласовано 2026-05-13:
//   Common:    0 префиксов, 0 суффиксов  (голая база, апгрейд в Uncommon — реальное событие)
//   Uncommon:  1 префикс,   1 суффикс
//   Rare:      1 префикс,   2 суффикса
//   Heroic:    2 префикса,  2 суффикса
//   Epic:      2 префикса,  3 суффикса
//   Legendary: 3 префикса,  3 суффикса
public readonly struct AffixBudget
{
	public readonly int Prefixes;
	public readonly int Suffixes;
	public AffixBudget(int p, int s) { Prefixes = p; Suffixes = s; }

	public static AffixBudget For(ItemRarity rarity) => rarity switch
	{
		ItemRarity.Common    => new AffixBudget(0, 0),
		ItemRarity.Uncommon  => new AffixBudget(1, 1),
		ItemRarity.Rare      => new AffixBudget(1, 2),
		ItemRarity.Heroic    => new AffixBudget(2, 2),
		ItemRarity.Epic      => new AffixBudget(2, 3),
		ItemRarity.Legendary => new AffixBudget(3, 3),
		_                    => new AffixBudget(0, 0),
	};
}
