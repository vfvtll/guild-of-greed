using System.Collections.Generic;

// === Оружие ===
// Все оружие имеет phys_atk и magic_atk (флэт прибавка к базе урона)
// и множители phys_mult / magic_mult.
//   Sword 1H: средний урон, +1 карта в начале хода.
//   Sword 2H: больший физический урон.
//   Staff: высокая магическая атака.
public class WeaponData
{
	public string Id;
	public string Name;
	public string Type;
	public string Grade;
	public string Tier;
	public int PhysAtk;
	public int MagicAtk;
	public float PhysMult = 1.0f;
	public float MagicMult = 1.0f;
	public int ExtraDraw;

	public WeaponData Clone() => (WeaponData)MemberwiseClone();
}

// === Броня ===
// Робы: маленькая физ. защита, +МакМП, бонусы магии.
// Лёгкая: средняя физ. защита, без +МакМП, +физ. атака.
public class ArmorData
{
	public string Id;
	public string Name;
	public string Type;
	public string Grade;
	public string Tier;
	public int PhysDef;
	public int PhysAtkBonus;
	public int MagicAtkBonus;
	public int MagicAtkPct;
	public int MpMaxBonus;     // прибавка к максимальной мане
	public int MpRegenBonus;
	public int HpBonus;
	public int ExtraDrawBonus;
	public string SuffixId;
	public string SuffixName;

	public ArmorData Clone() => (ArmorData)MemberwiseClone();
}

public static class ItemsDB
{
	public static readonly Dictionary<string, WeaponData> Weapons = new()
	{
		["sword_1h_low"] = new()
		{
			Id = "sword_1h_low", Name = "Старый меч", Type = "sword_1h",
			Grade = "E", Tier = "low",
			PhysAtk = 4, MagicAtk = 0,
			PhysMult = 1.0f, MagicMult = 0.5f,
			ExtraDraw = 1,
		},
		["sword_2h_low"] = new()
		{
			Id = "sword_2h_low", Name = "Тяжёлый двуручник", Type = "sword_2h",
			Grade = "E", Tier = "low",
			PhysAtk = 8, MagicAtk = 0,
			PhysMult = 1.3f, MagicMult = 0.4f,
		},
		["staff_low"] = new()
		{
			Id = "staff_low", Name = "Посох ученика", Type = "staff",
			Grade = "E", Tier = "low",
			PhysAtk = 1, MagicAtk = 10,
			PhysMult = 0.4f, MagicMult = 1.5f,
		},
	};

	public static readonly Dictionary<string, ArmorData> Armors = new()
	{
		// Робы — даёт МакМП (это "ткань мага"), маленькая физ. защита.
		["robe_power_low"] = new()
		{
			Id = "robe_power_low", Name = "Роба силы", Type = "robe",
			Grade = "E", Tier = "low",
			PhysDef = 2,
			MpMaxBonus = 30, MpRegenBonus = 2,
			MagicAtkBonus = 5, MagicAtkPct = 5,
		},
		["robe_wisdom_low"] = new()
		{
			Id = "robe_wisdom_low", Name = "Роба мудрости", Type = "robe",
			Grade = "E", Tier = "low",
			PhysDef = 2,
			MpMaxBonus = 30, MpRegenBonus = 6,
		},
		// Лёгкая — без МакМП, средняя физ. защита, +ФизАтк / ХП.
		["light_strength_low"] = new()
		{
			Id = "light_strength_low", Name = "Кожаная броня силы", Type = "light",
			Grade = "E", Tier = "low",
			PhysDef = 7,
			MpRegenBonus = 1,
			PhysAtkBonus = 4,
		},
		["light_vigor_low"] = new()
		{
			Id = "light_vigor_low", Name = "Кожаная броня стойкости", Type = "light",
			Grade = "E", Tier = "low",
			PhysDef = 8,
			MpRegenBonus = 1,
			PhysAtkBonus = 1, HpBonus = 30,
		},
	};

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
		return $"{a.Name}: {string.Join(", ", parts)}";
	}
}
