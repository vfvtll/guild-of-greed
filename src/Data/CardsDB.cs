using System;
using System.Collections.Generic;

// Card.Effect — что делает карта при разыгрывании:
//   damage_phys  — физ. урон по врагу
//   damage_magic — маг. урон по врагу
//   block        — щит игроку
//   heal         — восстановление ХП
//   debuff_phys  — на врага: получает на X% больше физ. урона N ходов
//   buff_magic   — на игрока: наносит на X% больше маг. урона N ходов
public class CardData
{
	public string Id;
	public string Name;
	public string Archetype;   // "warrior" / "mage"
	public string Type;        // "physical" / "magic"
	public int Cost;
	public int BaseDamage;
	public int Block;
	public int Heal;
	public int AmountPct;
	public int Duration;
	public string Effect;
	public string Icon;        // emoji в центре карты
}

public static class CardsDB
{
	public static readonly Dictionary<string, CardData> Cards = new()
	{
		// === Воин ===
		["strike"] = new()
		{
			Id = "strike", Name = "Удар", Archetype = "warrior", Type = "physical",
			Cost = 10, BaseDamage = 6, Effect = "damage_phys", Icon = "⚔",
		},
		["guard"] = new()
		{
			Id = "guard", Name = "Защитная стойка", Archetype = "warrior", Type = "physical",
			Cost = 8, Block = 8, Effect = "block", Icon = "🛡",
		},
		["armor_break"] = new()
		{
			Id = "armor_break", Name = "Пролом брони", Archetype = "warrior", Type = "physical",
			Cost = 18, AmountPct = 35, Duration = 2, Effect = "debuff_phys", Icon = "💢",
		},
		// === Маг ===
		["fire_bolt"] = new()
		{
			Id = "fire_bolt", Name = "Огненный снаряд", Archetype = "mage", Type = "magic",
			Cost = 18, BaseDamage = 8, Effect = "damage_magic", Icon = "🔥",
		},
		["water_strike"] = new()
		{
			Id = "water_strike", Name = "Удар водой", Archetype = "mage", Type = "magic",
			Cost = 22, BaseDamage = 11, Effect = "damage_magic", Icon = "💧",
		},
		["heal"] = new()
		{
			Id = "heal", Name = "Исцеление", Archetype = "mage", Type = "magic",
			Cost = 22, Heal = 12, Effect = "heal", Icon = "❤",
		},
		["magic_focus"] = new()
		{
			Id = "magic_focus", Name = "Магический фокус", Archetype = "mage", Type = "magic",
			Cost = 14, AmountPct = 20, Duration = 2, Effect = "buff_magic", Icon = "✨",
		},
	};

	public static CardData GetCard(string id)
		=> Cards.TryGetValue(id, out var c) ? c : null;

	// Стартовые колоды по 10 карт.
	public static readonly List<string> WarriorDeck = new()
	{
		"strike", "strike", "strike", "strike", "strike",
		"guard", "guard", "guard",
		"armor_break", "armor_break",
	};

	public static readonly List<string> MageDeck = new()
	{
		"fire_bolt", "fire_bolt", "fire_bolt",
		"water_strike", "water_strike",
		"magic_focus", "magic_focus",
		"heal", "heal",
		"strike",
	};

	// =====================================================================
	// Расчёт боевого эффекта — единый источник правды.
	// Combat.cs использует те же функции для применения урона,
	// CardView показывает результат на карте.
	// =====================================================================

	// Физ. урон до защиты и блока цели.
	// Формула: (BaseDamage + STR/3 + Оружие.ФизАтк + Броня.ФизАтк) ×
	//          Оружие.ФизМульт × (1 + ПроломБрони/100)
	public static int ComputePhysDamage(CardData card, CharacterData p, EnemyData enemy)
	{
		if (p == null) return Math.Max(1, card.BaseDamage);
		float baseAmt = card.BaseDamage + p.Str / 3f + p.WeaponPhysAtk() + p.PhysAtkBonus();
		float raw = baseAmt * p.PhysMult();
		float debuff = enemy != null ? enemy.GetEffectAmount("phys_taken_pct") / 100f : 0f;
		raw *= 1f + debuff;
		return Math.Max(1, (int)Math.Round(raw));
	}

	// Маг. урон до защиты и блока цели.
	// Формула: (BaseDamage + INT/3 + Оружие.МагАтк + Броня.МагАтк) ×
	//          Оружие.МагМульт × (1 + Броня.МагАтк%/100) × (1 + Бафф/100)
	public static int ComputeMagicDamage(CardData card, CharacterData p, EnemyData enemy)
	{
		if (p == null) return Math.Max(1, card.BaseDamage);
		float baseAmt = card.BaseDamage + p.Int / 3f + p.WeaponMagAtk() + p.MagicAtkBonus();
		float raw = baseAmt * p.MagicMult();
		raw *= 1f + p.MagicAtkPct() / 100f;
		raw *= 1f + p.GetEffectAmount("magic_dmg_pct") / 100f;
		return Math.Max(1, (int)Math.Round(raw));
	}

	public static int ComputeBlock(CardData card, CharacterData p)
		=> card.Block + (p?.Str ?? 0) / 4;

	public static int ComputeHeal(CardData card, CharacterData p)
		=> card.Heal + (p?.Int ?? 0) / 3;

	// Конкретный эффект для отображения на карте. Зависит от текущих
	// статов, экипировки и активных эффектов цели.
	public static string DescribeCurrent(CardData card, CharacterData p, EnemyData enemy)
	{
		return card.Effect switch
		{
			"damage_phys"  => $"Урон: {ComputePhysDamage(card, p, enemy)} физ.",
			"damage_magic" => $"Урон: {ComputeMagicDamage(card, p, enemy)} маг.",
			"block"        => $"Блок: +{ComputeBlock(card, p)}",
			"heal"         => $"Исцеление: +{ComputeHeal(card, p)} ХП",
			"debuff_phys"  => $"Цель: +{card.AmountPct}% физ. урона\nна {card.Duration} хода",
			"buff_magic"   => $"+{card.AmountPct}% маг. урона\nна {card.Duration} хода",
			_              => "—",
		};
	}

	// Полная формула карты — для тултипа иконки ⓘ.
	public static string DescribeFormula(CardData card)
	{
		return card.Effect switch
		{
			"damage_phys" =>
				$"Физ. урон\n" +
				$"= ({card.BaseDamage} + STR÷3 + Оружие.ФизАтк + Броня.ФизАтк)\n" +
				$"  × Оружие.ФизМульт\n" +
				$"  × (1 + ПроломБрони ÷ 100)\n" +
				$"\nЗатем − ФизЗащ цели и поглощается её блоком.",
			"damage_magic" =>
				$"Маг. урон\n" +
				$"= ({card.BaseDamage} + INT÷3 + Оружие.МагАтк + Броня.МагАтк)\n" +
				$"  × Оружие.МагМульт\n" +
				$"  × (1 + Броня.МагАтк% ÷ 100)\n" +
				$"  × (1 + МагФокус ÷ 100)",
			"block" =>
				$"Блок = {card.Block} + STR÷4\n" +
				$"Сбрасывается в начале следующего хода.",
			"heal" =>
				$"Восстановление = {card.Heal} + INT÷3\n" +
				$"Не превышает максимум ХП.",
			"debuff_phys" =>
				$"Накладывает на цель эффект:\n" +
				$"+{card.AmountPct}% получаемого физ. урона\n" +
				$"в течение {card.Duration} ходов.",
			"buff_magic" =>
				$"Накладывает на игрока эффект:\n" +
				$"+{card.AmountPct}% наносимого маг. урона\n" +
				$"в течение {card.Duration} ходов.",
			_ => "—",
		};
	}
}
