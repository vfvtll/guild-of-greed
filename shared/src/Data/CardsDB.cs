using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Реестр карт + расчётные хелперы.
// CardData (POCO) — в Domain/CardData.cs.
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

	// Стартовая колода по экипированному оружию. Используется и клиентом
	// при StartNewCombat, и сервером в BattleSession — оба должны получить
	// одну и ту же колоду для одного игрока.
	public static List<string> DeckFor(CharacterData character)
	{
		// Магическая колода — для всего что попадает в тип "staff" (посох).
		// Подгрузка через character.Weapon (instance), а не Id — на случай
		// нестандартных weapon-Id (например, легендарные посохи в будущем).
		if (character?.Weapon?.Type == "staff")
			return new List<string>(MageDeck);
		return new List<string>(WarriorDeck);
	}

	// =====================================================================
	// Расчёт боевого эффекта — единый источник правды.
	// Combat.cs использует те же функции для применения урона,
	// CardView показывает результат на карте.
	// =====================================================================

	// Физ. урон до защиты и блока цели.
	// Формула: (BaseDamage + STR/3 + Оружие.ФизАтк + Броня.ФизАтк + ПрефФизАтк)
	//          × Оружие.ФизМульт × (1 + СуфФизАтк%/100) × (1 + ПроломБрони/100)
	//          × (1 + N_nonAttack × PowerPerNonAttack/100)         ← И6.3
	// PhysAtkBonus()/MagicAtkBonus() уже включают плоские префиксы аффиксов
	// (см. CharacterData). PhysAtkPct() — суффиксы.
	//
	// Optional `hand` — текущая рука игрока. Если передана, считается
	// бонус от пассива одноручника (PowerPerNonAttack). null/пусто = бонус 0.
	public static int ComputePhysDamage(CardData card, CharacterData p, EnemyData enemy,
		List<string> hand = null)
	{
		if (p == null) return Math.Max(1, card.BaseDamage);
		float baseAmt = card.BaseDamage + p.Str / 3f + p.WeaponPhysAtk() + p.PhysAtkBonus();
		float raw = baseAmt * p.PhysMult();
		// Суффиксы аффиксов на ФизАтк (мультипликативный).
		raw *= 1f + p.PhysAtkPct() / 100f;
		// Пассив одноручника: каждая non-attack карта в руке усиливает удар.
		int perNonAttack = p.Weapon?.PassiveMagnitude(WeaponPassive.PowerPerNonAttack) ?? 0;
		if (perNonAttack > 0 && hand != null && hand.Count > 0)
		{
			int nonAttack = CountNonAttack(hand);
			raw *= 1f + nonAttack * perNonAttack / 100f;
		}
		// Бафф эликсира ярости (potion_strength).
		raw *= 1f + p.GetEffectAmount("phys_dmg_pct") / 100f;
		// Дебаф пролома брони на враге.
		float debuff = enemy != null ? enemy.GetEffectAmount("phys_taken_pct") / 100f : 0f;
		raw *= 1f + debuff;
		return Math.Max(1, (int)Math.Round(raw));
	}

	// Маг. урон до защиты и блока цели.
	// Формула: (BaseDamage + INT/3 + Оружие.МагАтк + Броня.МагАтк + ПрефМагАтк)
	//          × Оружие.МагМульт × (1 + СуфМагАтк%/100) × (1 + Бафф/100)
	//          × (1 + chainCount × MagicChain.Magnitude/100)        ← И6.3
	// chainCount — сколько атакующих маг.заклинаний уже было в ЭТОМ ходу
	// (т.е. для первого = 0, для второго = 1). Передаётся из CombatEngine.
	public static int ComputeMagicDamage(CardData card, CharacterData p, EnemyData enemy,
		int chainCount = 0)
	{
		if (p == null) return Math.Max(1, card.BaseDamage);
		float baseAmt = card.BaseDamage + p.Int / 3f + p.WeaponMagAtk() + p.MagicAtkBonus();
		float raw = baseAmt * p.MagicMult();
		raw *= 1f + p.MagicAtkPct() / 100f;
		raw *= 1f + p.GetEffectAmount("magic_dmg_pct") / 100f;
		// Пассив посоха: каждое следующее заклинание +Magnitude% урона.
		int chain = p.Weapon?.PassiveMagnitude(WeaponPassive.MagicChain) ?? 0;
		if (chain > 0 && chainCount > 0)
			raw *= 1f + chainCount * chain / 100f;
		return Math.Max(1, (int)Math.Round(raw));
	}

	// Стоимость маны с учётом пассивов оружия. По умолчанию = card.Cost.
	// Для посоха: каждое следующее атакующее маг.заклинание стоит +Magnitude2%.
	public static int ComputeManaCost(CardData card, CharacterData p, int chainCount = 0)
	{
		if (card == null) return 0;
		if (p == null) return card.Cost;
		float cost = card.Cost;
		if (card.IsMagicAttack)
		{
			var chain = p.Weapon?.GetPassive(WeaponPassive.MagicChain);
			if (chain != null && chainCount > 0)
				cost *= 1f + chainCount * chain.Magnitude2 / 100f;
		}
		return Math.Max(0, (int)Math.Round(cost));
	}

	private static int CountNonAttack(List<string> hand)
	{
		int n = 0;
		foreach (var id in hand)
		{
			var c = GetCard(id);
			if (c != null && !c.IsAttack) n++;
		}
		return n;
	}

	public static int ComputeBlock(CardData card, CharacterData p)
		=> card.Block + (p?.Dex ?? 0) / 4;

	public static int ComputeHeal(CardData card, CharacterData p)
		=> card.Heal + (p?.Int ?? 0) / 3;

	// Конкретный эффект для отображения на карте. Зависит от текущих
	// статов, экипировки и активных эффектов цели + контекста боя
	// (рука для меча, chain для посоха).
	public static string DescribeCurrent(CardData card, CharacterData p, EnemyData enemy,
		List<string> hand = null, int chainCount = 0)
	{
		return card.Effect switch
		{
			"damage_phys"  => $"Урон: {ComputePhysDamage(card, p, enemy, hand)} физ.",
			"damage_magic" => $"Урон: {ComputeMagicDamage(card, p, enemy, chainCount)} маг.",
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
				$"\nЗатем − ФизЗащ цели и поглощается её блоком.\n" +
				$"\n🎯 Крит каждые max(2, Оружие.CritN − DEX÷10) атак\n" +
				$"  × (1.5 + DEX÷100) урон",
			"damage_magic" =>
				$"Маг. урон\n" +
				$"= ({card.BaseDamage} + INT÷3 + Оружие.МагАтк + Броня.МагАтк)\n" +
				$"  × Оружие.МагМульт\n" +
				$"  × (1 + Броня.МагАтк% ÷ 100)\n" +
				$"  × (1 + МагФокус ÷ 100)\n" +
				$"\n🎯 Крит каждые max(2, Оружие.CritN − DEX÷10) атак\n" +
				$"  × (1.5 + DEX÷100) урон",
			"block" =>
				$"Блок = {card.Block} + DEX÷4\n" +
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
