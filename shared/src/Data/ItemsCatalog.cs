using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Каталог "заглушечных" комплектов брони: light (кожанка), robe (мантия),
// heavy (тяжёлая, начиная с D). Только статы — без аффиксов и пассивов,
// перекатать аффиксы можно через ItemGenerator/ForgeDB.
//
// Прогрессия через TierProgression.Mult — см. shared/src/Domain/TierProgression.cs.
// Профили статов на каждый слот хранятся в BaseStats — на E low light они
// соответствуют значениям из inline-блока ItemsDB (1.0× эталон).
//
// Существующие E low (light_*) и E mid (robe_*_low + robe_chest_wisdom_low)
// инициализированы inline в ItemsDB и НЕ переписываются — каталог только
// добавляет недостающие комбинации.
internal static class ItemsCatalog
{
	// =====================================================================
	// Имена комплектов (Set.Name берёт отсюда, часть тоже базируется на этом)
	// =====================================================================
	private static readonly Dictionary<(string arch, string grade, string rank), string> SetNames = new()
	{
		// Light
		[("light", "E", "low")] = "Кожанка следопыта",   // legacy, для справки
		[("light", "E", "mid")] = "Кожанка лазутчика",
		[("light", "E", "top")] = "Кожанка егеря",
		[("light", "D", "low")] = "Куртка разведчика",
		[("light", "D", "mid")] = "Куртка охотника",
		[("light", "D", "top")] = "Куртка ассасина",
		[("light", "C", "low")] = "Кираса бродяги",
		[("light", "C", "mid")] = "Кираса странника",
		[("light", "C", "top")] = "Кираса ветерана",
		[("light", "B", "low")] = "Камзол мастера",
		[("light", "B", "mid")] = "Камзол лазутчика-мастера",
		[("light", "B", "top")] = "Камзол охотника на королей",

		// Robe
		[("robe", "E", "low")] = "Роба ученика",
		[("robe", "E", "mid")] = "Мантия мудрого",       // legacy
		[("robe", "E", "top")] = "Печать чаромага",      // legacy
		[("robe", "D", "low")] = "Мантия адепта",
		[("robe", "D", "mid")] = "Облачение чародея",
		[("robe", "D", "top")] = "Регалии волхва",
		[("robe", "C", "low")] = "Одеяние мага",
		[("robe", "C", "mid")] = "Облачение архимага",
		[("robe", "C", "top")] = "Регалии лорда магии",
		[("robe", "B", "low")] = "Мантия древнего",
		[("robe", "B", "mid")] = "Облачение теурга",
		[("robe", "B", "top")] = "Регалии магистра тайн",

		// Heavy (только D+)
		[("heavy", "D", "low")] = "Латы новобранца",
		[("heavy", "D", "mid")] = "Латы стражника",
		[("heavy", "D", "top")] = "Латы рыцаря",
		[("heavy", "C", "low")] = "Латы паладина",
		[("heavy", "C", "mid")] = "Латы храмовника",
		[("heavy", "C", "top")] = "Латы крестоносца",
		[("heavy", "B", "low")] = "Латы инквизитора",
		[("heavy", "B", "mid")] = "Латы командора",
		[("heavy", "B", "top")] = "Латы избранного",
	};

	public static string GetSetName(string arch, string grade, string rank)
		=> SetNames.TryGetValue((arch, grade, rank), out var n) ? n : $"{arch} {grade} {rank}";

	// =====================================================================
	// Регистрация в общий словарь
	// =====================================================================
	public static void RegisterAll(Dictionary<string, ArmorData> armors)
	{
		// E grade — добиваем то, что не определено inline в ItemsDB.
		// inline уже есть:
		//   light low (light_chest_strength_low/light_*_low, set=light_e_low),
		//   robe  mid (robe_chest_wisdom_low/robe_*_low,    set=robe_e_mid),
		//   robe  top (robe_chest_power_low — только chest, set=robe_e_top).
		AddSet(armors, "light", "E", "mid");
		AddSet(armors, "light", "E", "top");
		AddSet(armors, "robe",  "E", "low");
		// Для robe E top — chest уже есть, нам нужны helmet/gloves/boots.
		AddRobeETopExtras(armors);

		// D, C, B — полный набор архетипов × рангов.
		foreach (var grade in new[] { "D", "C", "B" })
			foreach (var rank in new[] { "low", "mid", "top" })
			{
				AddSet(armors, "light", grade, rank);
				AddSet(armors, "robe",  grade, rank);
				AddSet(armors, "heavy", grade, rank);
			}
	}

	private static void AddSet(Dictionary<string, ArmorData> armors,
	                            string arch, string grade, string rank)
	{
		string setId  = $"{arch}_{grade.ToLowerInvariant()}_{rank}";
		string setName = GetSetName(arch, grade, rank);
		string g = grade;
		string t = rank;
		float m = TierProgression.Mult(grade, rank);

		armors[$"{arch}_chest_{g.ToLowerInvariant()}_{rank}"] = MakeChest(arch, g, t, setId, setName, m);
		armors[$"{arch}_helmet_{g.ToLowerInvariant()}_{rank}"] = MakeHelmet(arch, g, t, setId, setName, m);
		armors[$"{arch}_gloves_{g.ToLowerInvariant()}_{rank}"] = MakeGloves(arch, g, t, setId, setName, m);
		armors[$"{arch}_boots_{g.ToLowerInvariant()}_{rank}"] = MakeBoots(arch, g, t, setId, setName, m);
	}

	// Допилка robe_e_top: chest уже зарегистрирован как `robe_chest_power_low`
	// в ItemsDB inline, тут только три недостающие части. SetId совпадает —
	// они вместе с chest составят полный 4-piece.
	private static void AddRobeETopExtras(Dictionary<string, ArmorData> armors)
	{
		const string setId   = "robe_e_top";
		const string setName = "Печать чаромага";
		float m = TierProgression.Mult("E", "top");

		armors["robe_helmet_e_top"] = MakeHelmet("robe", "E", "top", setId, setName, m);
		armors["robe_gloves_e_top"] = MakeGloves("robe", "E", "top", setId, setName, m);
		armors["robe_boots_e_top"]  = MakeBoots ("robe", "E", "top", setId, setName, m);
	}

	// =====================================================================
	// Базовые сетки статов (E low light = 1.0× эталон)
	//
	// Для каждой пары (archetype, slot) — базовые значения. Применяем TierMult
	// и округляем. Минимум 1 для PhysDef.
	// =====================================================================
	private struct Stats
	{
		public float PhysDef, PhysAtk, MagicAtk, Hp, MpMax, MpRegen;
	}

	private static readonly Dictionary<(string arch, ArmorSlot slot), Stats> BaseStats = new()
	{
		// Light: physical-bias, sane PhysDef + PhysAtk + чуть MpRegen.
		[("light", ArmorSlot.Chest)]  = new Stats { PhysDef = 7,  PhysAtk = 4, Hp = 5,  MpRegen = 1 },
		[("light", ArmorSlot.Helmet)] = new Stats { PhysDef = 2,  Hp = 8 },
		[("light", ArmorSlot.Gloves)] = new Stats { PhysDef = 2,  PhysAtk = 2 },
		[("light", ArmorSlot.Boots)]  = new Stats { PhysDef = 2,  PhysAtk = 1, MpRegen = 1 },

		// Robe: magic-bias, низкая физ.защ., много MP/MagAtk.
		[("robe",  ArmorSlot.Chest)]  = new Stats { PhysDef = 2,  MagicAtk = 5, MpMax = 30, MpRegen = 4 },
		[("robe",  ArmorSlot.Helmet)] = new Stats { PhysDef = 1,  MagicAtk = 2, MpMax = 10 },
		[("robe",  ArmorSlot.Gloves)] = new Stats { PhysDef = 1,  MagicAtk = 3 },
		[("robe",  ArmorSlot.Boots)]  = new Stats { PhysDef = 1,  MpRegen = 2, MagicAtk = 1 },

		// Heavy: tank-bias, высокая защ., много HP, чуть PhysAtk.
		[("heavy", ArmorSlot.Chest)]  = new Stats { PhysDef = 13, Hp = 35, PhysAtk = 2 },
		[("heavy", ArmorSlot.Helmet)] = new Stats { PhysDef = 5,  Hp = 14 },
		[("heavy", ArmorSlot.Gloves)] = new Stats { PhysDef = 4,  Hp = 10, PhysAtk = 1 },
		[("heavy", ArmorSlot.Boots)]  = new Stats { PhysDef = 4,  Hp = 10 },
	};

	// =====================================================================
	// Фабрики для каждой слота
	// =====================================================================
	private static ArmorData MakeChest(string arch, string g, string r, string setId, string setName, float m)
	{
		var s = Scale(BaseStats[(arch, ArmorSlot.Chest)], m);
		return new ArmorData
		{
			Id = $"{arch}_chest_{g.ToLowerInvariant()}_{r}",
			Name = setName,
			Type = arch, Slot = ArmorSlot.Chest, Grade = g, Tier = r,
			SetId = setId,
			PhysDef = s.PhysDef, PhysAtkBonus = s.PhysAtk, MagicAtkBonus = s.MagicAtk,
			HpBonus = s.Hp, MpMaxBonus = s.MpMax, MpRegenBonus = s.MpRegen,
		};
	}

	private static ArmorData MakeHelmet(string arch, string g, string r, string setId, string setName, float m)
	{
		var s = Scale(BaseStats[(arch, ArmorSlot.Helmet)], m);
		return new ArmorData
		{
			Id = $"{arch}_helmet_{g.ToLowerInvariant()}_{r}",
			Name = $"{setName} — шлем",
			Type = arch, Slot = ArmorSlot.Helmet, Grade = g, Tier = r,
			SetId = setId,
			PhysDef = s.PhysDef, PhysAtkBonus = s.PhysAtk, MagicAtkBonus = s.MagicAtk,
			HpBonus = s.Hp, MpMaxBonus = s.MpMax, MpRegenBonus = s.MpRegen,
		};
	}

	private static ArmorData MakeGloves(string arch, string g, string r, string setId, string setName, float m)
	{
		var s = Scale(BaseStats[(arch, ArmorSlot.Gloves)], m);
		return new ArmorData
		{
			Id = $"{arch}_gloves_{g.ToLowerInvariant()}_{r}",
			Name = $"{setName} — перчатки",
			Type = arch, Slot = ArmorSlot.Gloves, Grade = g, Tier = r,
			SetId = setId,
			PhysDef = s.PhysDef, PhysAtkBonus = s.PhysAtk, MagicAtkBonus = s.MagicAtk,
			HpBonus = s.Hp, MpMaxBonus = s.MpMax, MpRegenBonus = s.MpRegen,
		};
	}

	private static ArmorData MakeBoots(string arch, string g, string r, string setId, string setName, float m)
	{
		var s = Scale(BaseStats[(arch, ArmorSlot.Boots)], m);
		return new ArmorData
		{
			Id = $"{arch}_boots_{g.ToLowerInvariant()}_{r}",
			Name = $"{setName} — сапоги",
			Type = arch, Slot = ArmorSlot.Boots, Grade = g, Tier = r,
			SetId = setId,
			PhysDef = s.PhysDef, PhysAtkBonus = s.PhysAtk, MagicAtkBonus = s.MagicAtk,
			HpBonus = s.Hp, MpMaxBonus = s.MpMax, MpRegenBonus = s.MpRegen,
		};
	}

	// Перевод float-базы в int с округлением. PhysDef всегда ≥ 1, иначе 0.
	private struct ScaledStats
	{
		public int PhysDef, PhysAtk, MagicAtk, Hp, MpMax, MpRegen;
	}

	private static ScaledStats Scale(Stats b, float m)
	{
		return new ScaledStats
		{
			PhysDef  = b.PhysDef  > 0 ? Math.Max(1, (int)Math.Round(b.PhysDef * m)) : 0,
			PhysAtk  = b.PhysAtk  > 0 ? Math.Max(1, (int)Math.Round(b.PhysAtk * m)) : 0,
			MagicAtk = b.MagicAtk > 0 ? Math.Max(1, (int)Math.Round(b.MagicAtk * m)) : 0,
			Hp       = b.Hp       > 0 ? Math.Max(1, (int)Math.Round(b.Hp * m)) : 0,
			MpMax    = b.MpMax    > 0 ? Math.Max(1, (int)Math.Round(b.MpMax * m)) : 0,
			MpRegen  = b.MpRegen  > 0 ? Math.Max(1, (int)Math.Round(b.MpRegen * m)) : 0,
		};
	}
}
