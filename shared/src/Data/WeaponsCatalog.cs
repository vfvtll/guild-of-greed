using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Каталог "сгенерированного" оружия: для каждой пары (grade, tier) выше E-low
// делает экземпляры всех 4 типов (sword_1h / sword_2h / staff / knife).
// Базовые статы — те же что у инлайн E-low версии в ItemsDB.Weapons, но
// масштабированные через TierProgression.Mult.
//
// E-low — НЕ переписывается (живёт inline в ItemsDB как стартовое оружие /
// shop stock / loot training-dummy). Каталог добавляет только:
//   E mid, E top
//   D low, mid, top
//
// IDs:
//   sword_1h_e_mid, sword_1h_e_top, sword_1h_d_low, ..., dagger_d_top.
internal static class WeaponsCatalog
{
	// Профиль одного типа оружия. Все характеристики повторяются между
	// tier'ами; меняются только PhysAtk/MagicAtk через TierProgression.Mult.
	// Базовые значения на E-low соответствуют тому, что лежит inline в ItemsDB.
	private struct Profile
	{
		public string Type;
		public bool IsTwoHanded;
		public int PhysAtkBase;
		public int MagicAtkBase;
		public int ExtraDraw;
		public int CritEveryNAttacks;
		public List<WeaponPassive> Passives;
	}

	private static readonly Dictionary<string, Profile> Profiles = new()
	{
		["sword_1h"] = new Profile
		{
			Type = "sword_1h", IsTwoHanded = false,
			PhysAtkBase = 6, MagicAtkBase = 4,
			ExtraDraw = 1, CritEveryNAttacks = 10,
			Passives = new() { new WeaponPassive(WeaponPassive.PowerPerNonAttack, 10) },
		},
		["sword_2h"] = new Profile
		{
			Type = "sword_2h", IsTwoHanded = true,
			PhysAtkBase = 10, MagicAtkBase = 7,
			ExtraDraw = 0, CritEveryNAttacks = 12,
			Passives = new() { new WeaponPassive(WeaponPassive.BleedOnHit, 50) },
		},
		["staff"] = new Profile
		{
			Type = "staff", IsTwoHanded = true,
			PhysAtkBase = 7, MagicAtkBase = 15,
			ExtraDraw = 0, CritEveryNAttacks = 20,
			Passives = new() { new WeaponPassive(WeaponPassive.MagicChain, 20, 30) },
		},
		["knife"] = new Profile
		{
			Type = "knife", IsTwoHanded = false,
			PhysAtkBase = 3, MagicAtkBase = 3,
			ExtraDraw = 0, CritEveryNAttacks = 6,
			Passives = new(),
		},
	};

	// Имена по (type, grade, tier). Если не нашли — fallback на тип+тэг.
	private static readonly Dictionary<(string type, string grade, string tier), string> Names = new()
	{
		// E mid / top
		[("sword_1h", "E", "mid")] = "Старший меч",
		[("sword_1h", "E", "top")] = "Меч странствия",
		[("sword_2h", "E", "mid")] = "Военный двуручник",
		[("sword_2h", "E", "top")] = "Двуручник наёмника",
		[("staff",    "E", "mid")] = "Посох послушника",
		[("staff",    "E", "top")] = "Посох мудрости",
		[("knife",    "E", "mid")] = "Кинжал лазутчика",
		[("knife",    "E", "top")] = "Кинжал убийцы",

		// D
		[("sword_1h", "D", "low")] = "Меч стражника",
		[("sword_1h", "D", "mid")] = "Меч капитана",
		[("sword_1h", "D", "top")] = "Меч рыцаря",
		[("sword_2h", "D", "low")] = "Двуручник дружинника",
		[("sword_2h", "D", "mid")] = "Двуручник варвара",
		[("sword_2h", "D", "top")] = "Двуручник палача",
		[("staff",    "D", "low")] = "Посох адепта",
		[("staff",    "D", "mid")] = "Посох чародея",
		[("staff",    "D", "top")] = "Посох волхва",
		[("knife",    "D", "low")] = "Кинжал бандита",
		[("knife",    "D", "mid")] = "Кинжал тени",
		[("knife",    "D", "top")] = "Кинжал ассасина",
	};

	public static void RegisterAll(Dictionary<string, WeaponData> weapons)
	{
		// E: добавляем mid и top. low остаётся inline в ItemsDB.
		foreach (var type in Profiles.Keys)
		{
			Add(weapons, type, "E", "mid");
			Add(weapons, type, "E", "top");
		}
		// D: полный набор low/mid/top.
		foreach (var type in Profiles.Keys)
		{
			Add(weapons, type, "D", "low");
			Add(weapons, type, "D", "mid");
			Add(weapons, type, "D", "top");
		}
	}

	private static void Add(Dictionary<string, WeaponData> weapons,
	                         string type, string grade, string tier)
	{
		var p = Profiles[type];
		float m = TierProgression.Mult(grade, tier);
		string id = $"{type}_{grade.ToLowerInvariant()}_{tier}";
		string name = Names.TryGetValue((type, grade, tier), out var n)
			? n
			: $"{ItemsDB.WeaponTypeName(type)} ({grade}-{tier})";

		weapons[id] = new WeaponData
		{
			Id = id,
			Name = name,
			Type = type,
			Grade = grade,
			Tier = tier,
			IsTwoHanded = p.IsTwoHanded,
			PhysAtk = p.PhysAtkBase > 0 ? Math.Max(1, (int)Math.Round(p.PhysAtkBase * m)) : 0,
			MagicAtk = p.MagicAtkBase > 0 ? Math.Max(1, (int)Math.Round(p.MagicAtkBase * m)) : 0,
			ExtraDraw = p.ExtraDraw,
			CritEveryNAttacks = p.CritEveryNAttacks,
			Passives = ClonePassives(p.Passives),
		};
	}

	private static List<WeaponPassive> ClonePassives(List<WeaponPassive> src)
	{
		var list = new List<WeaponPassive>(src.Count);
		foreach (var p in src) list.Add(new WeaponPassive(p.Kind, p.Magnitude, p.Magnitude2));
		return list;
	}
}
