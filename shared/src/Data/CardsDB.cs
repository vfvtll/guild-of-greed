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

		// === Карты-апгрейды по типу оружия (открываются на ур.1 оружия) =====
		// При weapon-level≥1 стартовая колода получает замену 2x strike → slash
		// (или эквивалентно по типу). См. DeckUpgrades ниже.

		// Одноручный меч ур.1: более мощный удар за бо́льшую ману.
		["slash"] = new()
		{
			Id = "slash", Name = "Разрез", Archetype = "warrior", Type = "physical",
			Cost = 18, BaseDamage = 11, Effect = "damage_phys", Icon = "🗡",
		},
		// Двуручный меч ур.1: тяжёлый раскол.
		["cleave"] = new()
		{
			Id = "cleave", Name = "Раскол", Archetype = "warrior", Type = "physical",
			Cost = 22, BaseDamage = 14, Effect = "damage_phys", Icon = "🪓",
		},
		// Кинжал ур.1: дешёвый укол. Архетип кинжала — спам.
		["quick_stab"] = new()
		{
			Id = "quick_stab", Name = "Быстрый укол", Archetype = "warrior", Type = "physical",
			Cost = 6, BaseDamage = 4, Effect = "damage_phys", Icon = "🔪",
		},
		// Посох ур.1: усиленный снаряд.
		["empowered_bolt"] = new()
		{
			Id = "empowered_bolt", Name = "Усиленный снаряд", Archetype = "mage", Type = "magic",
			Cost = 24, BaseDamage = 14, Effect = "damage_magic", Icon = "🔥",
		},
	};

	public static CardData GetCard(string id)
		=> Cards.TryGetValue(id, out var c) ? c : null;

	// === Стартовые колоды по типу оружия ====================================
	//
	// 10-карточные стартеры. Заточены под конкретное оружие чтобы каждое
	// чувствовалось по-своему ещё ДО прокачки. Голый персонаж (Weapon=null)
	// получает Warrior-стартер как универсальный fallback.

	private static readonly List<string> WarriorStarter = new()
	{
		"strike", "strike", "strike", "strike", "strike",
		"guard", "guard", "guard",
		"armor_break", "armor_break",
	};

	// Двуручник — больше strike, меньше guard (стиль 2H — давление, не блок).
	private static readonly List<string> TwoHanderStarter = new()
	{
		"strike", "strike", "strike", "strike", "strike", "strike",
		"guard", "guard",
		"armor_break", "armor_break",
	};

	// Кинжал — шквал ударов, меньше защиты. Своя ниша откроется на ур.1 апгрейдом.
	private static readonly List<string> KnifeStarter = new()
	{
		"strike", "strike", "strike", "strike", "strike", "strike",
		"guard", "guard",
		"armor_break", "armor_break",
	};

	// Маг (посох).
	private static readonly List<string> MageStarter = new()
	{
		"fire_bolt", "fire_bolt", "fire_bolt",
		"water_strike", "water_strike",
		"magic_focus", "magic_focus",
		"heal", "heal",
		"strike",
	};

	// Lookup стартовой колоды по типу оружия. Неизвестный тип → Warrior.
	private static readonly Dictionary<string, List<string>> StarterByWeaponType = new()
	{
		["sword_1h"] = WarriorStarter,
		["sword_2h"] = TwoHanderStarter,
		["knife"]    = KnifeStarter,
		["staff"]    = MageStarter,
	};

	// Старые имена оставлены под Obsolete на случай ссылок из save-блобов / тестов.
	[System.Obsolete("Use DeckFor(character)")] public static readonly List<string> WarriorDeck = WarriorStarter;
	[System.Obsolete("Use DeckFor(character)")] public static readonly List<string> MageDeck = MageStarter;

	// === Апгрейды колоды по уровню оружия ===================================
	//
	// Когда GetWeaponLevel(type) ≥ Level — стартовые карты заменяются на
	// улучшенные. На ур.1 пока один апгрейд на тип (показать паттерн),
	// потом докинем больше уровней с дополнительными апгрейдами.
	public class DeckUpgrade
	{
		public int Level;        // Минимальный уровень оружия для применения.
		public string OldCardId; // Что заменяем (берём первые N штук в колоде).
		public string NewCardId; // На что меняем.
		public int Count;        // Сколько копий заменить.
	}

	private static readonly Dictionary<string, List<DeckUpgrade>> UpgradesByWeaponType = new()
	{
		["sword_1h"] = new()
		{
			new() { Level = 1, OldCardId = "strike", NewCardId = "slash", Count = 2 },
		},
		["sword_2h"] = new()
		{
			new() { Level = 1, OldCardId = "strike", NewCardId = "cleave", Count = 2 },
		},
		["knife"] = new()
		{
			new() { Level = 1, OldCardId = "strike", NewCardId = "quick_stab", Count = 3 },
		},
		["staff"] = new()
		{
			new() { Level = 1, OldCardId = "fire_bolt", NewCardId = "empowered_bolt", Count = 2 },
		},
	};

	// Стартовая колода для боя. Используется и клиентом (StartNewCombatAsync),
	// и сервером (HandleStartBattle) — обе стороны обязаны получить одно и то же.
	// Чистая функция от character.Weapon.Type + character.WeaponXp[type].
	public static List<string> DeckFor(CharacterData character)
	{
		string type = character?.Weapon?.Type;
		var starter = !string.IsNullOrEmpty(type)
			&& StarterByWeaponType.TryGetValue(type, out var s)
			? s : WarriorStarter;

		var deck = new List<string>(starter);
		if (character != null && !string.IsNullOrEmpty(type)
			&& UpgradesByWeaponType.TryGetValue(type, out var upgrades))
		{
			int level = character.GetWeaponLevel(type);
			foreach (var u in upgrades)
			{
				if (level < u.Level) continue;
				ReplaceN(deck, u.OldCardId, u.NewCardId, u.Count);
			}
		}
		return deck;
	}

	// Заменяет первые N вхождений oldId на newId. Стабильность порядка важна
	// для CSP — обе стороны должны получить идентичный список.
	private static void ReplaceN(List<string> deck, string oldId, string newId, int count)
	{
		for (int i = 0; i < deck.Count && count > 0; i++)
		{
			if (deck[i] != oldId) continue;
			deck[i] = newId;
			count--;
		}
	}

	// =====================================================================
	// Расчёт боевого эффекта — единый источник правды.
	// Combat.cs использует те же функции для применения урона,
	// CardView показывает результат на карте.
	// =====================================================================

	// Физ. урон до защиты и блока цели.
	// Формула: (BaseDamage + STR/3 + Оружие.ФизАтк + Броня.ФизАтк + ПрефФизАтк)
	//          × (1 + СуфФизАтк%/100) × (1 + ПроломБрони/100)
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
		float raw = card.BaseDamage + p.Str / 3f + p.WeaponPhysAtk() + p.PhysAtkBonus();
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
	//          × (1 + СуфМагАтк%/100) × (1 + Бафф/100)
	//          × (1 + chainCount × MagicChain.Magnitude/100)        ← И6.3
	// chainCount — сколько атакующих маг.заклинаний уже было в ЭТОМ ходу
	// (т.е. для первого = 0, для второго = 1). Передаётся из CombatEngine.
	public static int ComputeMagicDamage(CardData card, CharacterData p, EnemyData enemy,
		int chainCount = 0)
	{
		if (p == null) return Math.Max(1, card.BaseDamage);
		float raw = card.BaseDamage + p.Int / 3f + p.WeaponMagAtk() + p.MagicAtkBonus();
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
