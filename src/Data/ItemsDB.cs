using System.Collections.Generic;

// Реестр оружия и брони + расчётные хелперы.
// Сами POCO-определения (WeaponData, ArmorData, ArmorSlot) — в Domain/.
public static class ItemsDB
{
	// =====================================================================
	// Оружие
	// =====================================================================
	public static readonly Dictionary<string, WeaponData> Weapons = new()
	{
		["sword_1h_low"] = new()
		{
			Id = "sword_1h_low", Name = "Старый меч", Type = "sword_1h",
			Grade = "E", Tier = "low",
			PhysAtk = 4, MagicAtk = 0,
			PhysMult = 1.0f, MagicMult = 0.5f,
			ExtraDraw = 1,
			CritEveryNAttacks = 10,
		},
		["sword_2h_low"] = new()
		{
			Id = "sword_2h_low", Name = "Тяжёлый двуручник", Type = "sword_2h",
			Grade = "E", Tier = "low",
			PhysAtk = 8, MagicAtk = 0,
			PhysMult = 1.3f, MagicMult = 0.4f,
			CritEveryNAttacks = 12,
		},
		["staff_low"] = new()
		{
			Id = "staff_low", Name = "Посох ученика", Type = "staff",
			Grade = "E", Tier = "low",
			PhysAtk = 1, MagicAtk = 10,
			PhysMult = 0.4f, MagicMult = 1.5f,
			CritEveryNAttacks = 20,
		},
	};

	// =====================================================================
	// Броня (4 слота: chest, helmet, gloves, boots)
	// =====================================================================
	public static readonly Dictionary<string, ArmorData> Armors = new()
	{
		// === ГРУДЬ (chest) — основной кусок, наибольшая защита и бонусы ===
		["robe_chest_power_low"] = new()
		{
			Id = "robe_chest_power_low", Name = "Роба силы", Type = "robe",
			Slot = ArmorSlot.Chest, Grade = "E", Tier = "low",
			PhysDef = 2,
			MpMaxBonus = 30, MpRegenBonus = 2,
			MagicAtkBonus = 5, MagicAtkPct = 5,
		},
		["robe_chest_wisdom_low"] = new()
		{
			Id = "robe_chest_wisdom_low", Name = "Роба мудрости", Type = "robe",
			Slot = ArmorSlot.Chest, Grade = "E", Tier = "low",
			PhysDef = 2,
			MpMaxBonus = 30, MpRegenBonus = 6,
		},
		["light_chest_strength_low"] = new()
		{
			Id = "light_chest_strength_low", Name = "Кожаная броня силы", Type = "light",
			Slot = ArmorSlot.Chest, Grade = "E", Tier = "low",
			PhysDef = 7,
			MpRegenBonus = 1,
			PhysAtkBonus = 4,
		},
		["light_chest_vigor_low"] = new()
		{
			Id = "light_chest_vigor_low", Name = "Кожаная броня стойкости", Type = "light",
			Slot = ArmorSlot.Chest, Grade = "E", Tier = "low",
			PhysDef = 8,
			MpRegenBonus = 1,
			PhysAtkBonus = 1, HpBonus = 30,
		},

		// === ШЛЕМ (helmet) ===
		["robe_helmet_low"] = new()
		{
			Id = "robe_helmet_low", Name = "Капюшон мага", Type = "robe",
			Slot = ArmorSlot.Helmet, Grade = "E", Tier = "low",
			PhysDef = 1, MpMaxBonus = 8, MagicAtkBonus = 2,
		},
		["light_helmet_low"] = new()
		{
			Id = "light_helmet_low", Name = "Кожаный шлем", Type = "light",
			Slot = ArmorSlot.Helmet, Grade = "E", Tier = "low",
			PhysDef = 2, HpBonus = 8,
		},

		// === ПЕРЧАТКИ (gloves) ===
		["robe_gloves_low"] = new()
		{
			Id = "robe_gloves_low", Name = "Перчатки мага", Type = "robe",
			Slot = ArmorSlot.Gloves, Grade = "E", Tier = "low",
			PhysDef = 1, MagicAtkBonus = 3,
		},
		["light_gloves_low"] = new()
		{
			Id = "light_gloves_low", Name = "Кожаные перчатки", Type = "light",
			Slot = ArmorSlot.Gloves, Grade = "E", Tier = "low",
			PhysDef = 2, PhysAtkBonus = 2,
		},

		// === САПОГИ (boots) ===
		["robe_boots_low"] = new()
		{
			Id = "robe_boots_low", Name = "Туфли мага", Type = "robe",
			Slot = ArmorSlot.Boots, Grade = "E", Tier = "low",
			PhysDef = 1, MpRegenBonus = 2,
		},
		["light_boots_low"] = new()
		{
			Id = "light_boots_low", Name = "Кожаные сапоги", Type = "light",
			Slot = ArmorSlot.Boots, Grade = "E", Tier = "low",
			PhysDef = 2, MpRegenBonus = 1, PhysAtkBonus = 1,
		},
	};

	// =====================================================================
	// Суффиксы — катаются на броню E grade при выпадении лута
	// =====================================================================
	public class Suffix
	{
		public string Name;
		public int PhysAtkBonus;
		public int MagicAtkBonus;
		public int MpRegenBonus;
		public int MpMaxBonus;
		public int HpBonus;
		public int ExtraDrawBonus;
	}

	public static readonly Dictionary<string, Suffix> Suffixes = new()
	{
		["of_power"]    = new() { Name = "Силы",      PhysAtkBonus = 3 },
		["of_magic"]    = new() { Name = "Магии",     MagicAtkBonus = 3 },
		["of_wisdom"]   = new() { Name = "Мудрости",  MpRegenBonus = 3 },
		["of_mana"]     = new() { Name = "Маны",      MpMaxBonus = 20 },
		["of_vitality"] = new() { Name = "Жизни",     HpBonus = 25 },
		["of_drawing"]  = new() { Name = "Розыгрыша", ExtraDrawBonus = 1 },
	};

	// =====================================================================
	// Доступ
	// =====================================================================
	public static WeaponData GetWeapon(string id)
		=> Weapons.TryGetValue(id, out var w) ? w : null;

	public static ArmorData GetArmor(string id)
		=> Armors.TryGetValue(id, out var a) ? a : null;

	public static ArmorData RollArmorWithSuffix(string id)
	{
		var armor = GetArmor(id)?.Clone();
		if (armor == null || armor.Grade != "E") return armor;
		var keys = new List<string>(Suffixes.Keys);
		var pick = Rng.Pick(keys);
		var suf = Suffixes[pick];
		armor.SuffixId = pick;
		armor.SuffixName = suf.Name;
		armor.PhysAtkBonus   += suf.PhysAtkBonus;
		armor.MagicAtkBonus  += suf.MagicAtkBonus;
		armor.MpRegenBonus   += suf.MpRegenBonus;
		armor.MpMaxBonus     += suf.MpMaxBonus;
		armor.HpBonus        += suf.HpBonus;
		armor.ExtraDrawBonus += suf.ExtraDrawBonus;
		armor.Name = $"{armor.Name} {suf.Name}";
		return armor;
	}

	// =====================================================================
	// Описания для UI
	// =====================================================================
	public static string DescribeWeapon(WeaponData w)
	{
		if (w == null) return "—";
		var parts = new List<string>
		{
			$"Физ ×{w.PhysMult:F1}",
			$"Маг ×{w.MagicMult:F1}",
			$"+{w.PhysAtk} ФизАтк",
			$"+{w.MagicAtk} МагАтк",
		};
		if (w.ExtraDraw > 0) parts.Add($"+{w.ExtraDraw} карта");
		if (w.CritEveryNAttacks > 0 && w.CritEveryNAttacks < 999)
			parts.Add($"крит каждые {w.CritEveryNAttacks}");
		return $"{w.Name}: {string.Join(", ", parts)}";
	}

	public static string DescribeArmor(ArmorData a)
	{
		if (a == null) return "—";
		var parts = new List<string>();
		if (a.PhysDef > 0)         parts.Add($"{a.PhysDef} ФизЗащ");
		if (a.MpMaxBonus > 0)      parts.Add($"+{a.MpMaxBonus} МакМП");
		if (a.MpRegenBonus > 0)    parts.Add($"+{a.MpRegenBonus} РегМП");
		if (a.PhysAtkBonus > 0)    parts.Add($"+{a.PhysAtkBonus} ФизАтк");
		if (a.MagicAtkBonus > 0)   parts.Add($"+{a.MagicAtkBonus} МагАтк");
		if (a.MagicAtkPct > 0)     parts.Add($"+{a.MagicAtkPct}% МагАтк");
		if (a.HpBonus > 0)         parts.Add($"+{a.HpBonus} ХП");
		if (a.ExtraDrawBonus > 0)  parts.Add($"+{a.ExtraDrawBonus} карта");
		return parts.Count == 0 ? a.Name : $"{a.Name}: {string.Join(", ", parts)}";
	}

	// Возвращает все brony данного слота — для будущего инвентарного UI выбора.
	public static IEnumerable<ArmorData> ArmorsBySlot(ArmorSlot slot)
	{
		foreach (var a in Armors.Values)
			if (a.Slot == slot) yield return a;
	}
}
