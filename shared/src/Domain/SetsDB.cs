using System;
using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Реестр всех комплектов (сетов) в игре. Согласовано: минимум 3 на Grade.
//
// Историческая часть (E grade) определена inline ниже. Всё остальное —
// light/robe/heavy × low/mid/top на D, C, B + недостающие куски на E —
// генерируется через BuildAllSets() с прогрессией по TierProgression.Mult.
//
// Бонусы кладутся прогрессивно: чем больше частей сета — тем сильнее. Все
// текущие сеты — 4 piece (chest/helmet/gloves/boots), бонусы — на 2 и 4
// надетые части. Бижутерия (амулеты, кольца) в сеты пока не входит.
//
// Привязка предмета к сету = ArmorData.SetId. Регистрация дублируется здесь
// (PartIds) для валидации и UI-отображения "сколько ты надел".
public static class SetsDB
{
	public static readonly Dictionary<string, SetData> Sets = BuildAllSets();

	private static Dictionary<string, SetData> BuildAllSets()
	{
		var sets = new Dictionary<string, SetData>
		{
			// === E-grade / low — кожаная защита (light, strength chest) ===
			["light_e_low"] = new()
			{
				Id = "light_e_low", Name = "Кожанка следопыта",
				Grade = "E", Variant = "low",
				PartIds = new() { "light_chest_strength_low", "light_helmet_low", "light_gloves_low", "light_boots_low" },
				Bonuses = new()
				{
					new SetBonus(2, AffixStatKind.PhysAtk, 2,  isPercent: false),
					new SetBonus(4, AffixStatKind.PhysDef, 3,  isPercent: false),
					new SetBonus(4, AffixStatKind.Hp,      15, isPercent: false),
				},
			},

			// === E-grade / mid — мантия мудрости (robe, mp focus) ===
			["robe_e_mid"] = new()
			{
				Id = "robe_e_mid", Name = "Мантия мудрого",
				Grade = "E", Variant = "mid",
				PartIds = new() { "robe_chest_wisdom_low", "robe_helmet_low", "robe_gloves_low", "robe_boots_low" },
				Bonuses = new()
				{
					new SetBonus(2, AffixStatKind.MpRegen, 2,  isPercent: false),
					new SetBonus(4, AffixStatKind.Mp,      25, isPercent: false),
					new SetBonus(4, AffixStatKind.MagAtk,  10, isPercent: true),
				},
			},

			// === E-grade / top — печать чаромага (robe, power chest) ===
			// 4-piece: chest = legacy robe_chest_power_low; остальные —
			// robe_helmet_e_top / robe_gloves_e_top / robe_boots_e_top
			// (генерируются ItemsCatalog'ом в Data).
			["robe_e_top"] = new()
			{
				Id = "robe_e_top", Name = "Печать чаромага",
				Grade = "E", Variant = "top",
				PartIds = new() { "robe_chest_power_low", "robe_helmet_e_top", "robe_gloves_e_top", "robe_boots_e_top" },
				Bonuses = new()
				{
					new SetBonus(2, AffixStatKind.MagAtk, 5,  isPercent: false),
					new SetBonus(4, AffixStatKind.Mp,     20, isPercent: false),
					new SetBonus(4, AffixStatKind.MagAtk, 15, isPercent: true),
				},
			},
		};

		// Добиваем E grade: light mid/top, robe low.
		Add(sets, BuildLightSet("E", "mid"));
		Add(sets, BuildLightSet("E", "top"));
		Add(sets, BuildRobeSet ("E", "low"));

		// D / C / B grade: все три архетипа × три ранга.
		foreach (var grade in new[] { "D", "C", "B" })
		{
			foreach (var rank in new[] { "low", "mid", "top" })
			{
				Add(sets, BuildLightSet(grade, rank));
				Add(sets, BuildRobeSet (grade, rank));
				Add(sets, BuildHeavySet(grade, rank));
			}
		}

		return sets;
	}

	private static void Add(Dictionary<string, SetData> sets, SetData s) => sets[s.Id] = s;

	// =====================================================================
	// Фабрики комплектов
	//
	// ID-конвенция: SetId = "{arch}_{grade.lower}_{rank}", PartId =
	// "{arch}_{slot}_{grade.lower}_{rank}". Имена согласованы с ItemsCatalog
	// (Data) — он формирует те же ID для конкретных предметов.
	// =====================================================================
	private static SetData BuildLightSet(string grade, string rank)
	{
		string id = $"light_{grade.ToLowerInvariant()}_{rank}";
		float m = TierProgression.Mult(grade, rank);
		return new SetData
		{
			Id = id, Name = LightName(grade, rank), Grade = grade, Variant = rank,
			PartIds = Parts("light", grade, rank),
			Bonuses = new()
			{
				new SetBonus(2, AffixStatKind.PhysAtk, Scale(2, m),  isPercent: false),
				new SetBonus(4, AffixStatKind.PhysDef, Scale(3, m),  isPercent: false),
				new SetBonus(4, AffixStatKind.Hp,      Scale(15, m), isPercent: false),
			},
		};
	}

	private static SetData BuildRobeSet(string grade, string rank)
	{
		string id = $"robe_{grade.ToLowerInvariant()}_{rank}";
		float m = TierProgression.Mult(grade, rank);
		return new SetData
		{
			Id = id, Name = RobeName(grade, rank), Grade = grade, Variant = rank,
			PartIds = Parts("robe", grade, rank),
			Bonuses = new()
			{
				new SetBonus(2, AffixStatKind.MpRegen, Scale(2, m),  isPercent: false),
				new SetBonus(4, AffixStatKind.Mp,      Scale(25, m), isPercent: false),
				// MagAtk%-бонус растёт мягко (overall %-стат, не надо ×6 к 60).
				new SetBonus(4, AffixStatKind.MagAtk,  10 + (int)Math.Round((m - 1) * 4), isPercent: true),
			},
		};
	}

	private static SetData BuildHeavySet(string grade, string rank)
	{
		string id = $"heavy_{grade.ToLowerInvariant()}_{rank}";
		float m = TierProgression.Mult(grade, rank);
		return new SetData
		{
			Id = id, Name = HeavyName(grade, rank), Grade = grade, Variant = rank,
			PartIds = Parts("heavy", grade, rank),
			Bonuses = new()
			{
				new SetBonus(2, AffixStatKind.PhysDef, Scale(3, m),  isPercent: false),
				new SetBonus(4, AffixStatKind.Hp,      Scale(30, m), isPercent: false),
				new SetBonus(4, AffixStatKind.PhysAtk, Scale(2, m),  isPercent: false),
			},
		};
	}

	private static List<string> Parts(string arch, string grade, string rank)
	{
		string g = grade.ToLowerInvariant();
		return new()
		{
			$"{arch}_chest_{g}_{rank}",
			$"{arch}_helmet_{g}_{rank}",
			$"{arch}_gloves_{g}_{rank}",
			$"{arch}_boots_{g}_{rank}",
		};
	}

	private static int Scale(int baseVal, float m) => Math.Max(1, (int)Math.Round(baseVal * m));

	// =====================================================================
	// Локализованные имена комплектов (русский, без Lang.T — shared слой)
	// =====================================================================
	private static string LightName(string g, string r) => (g, r) switch
	{
		("E", "mid") => "Кожанка лазутчика",
		("E", "top") => "Кожанка егеря",
		("D", "low") => "Куртка разведчика",
		("D", "mid") => "Куртка охотника",
		("D", "top") => "Куртка ассасина",
		("C", "low") => "Кираса бродяги",
		("C", "mid") => "Кираса странника",
		("C", "top") => "Кираса ветерана",
		("B", "low") => "Камзол мастера",
		("B", "mid") => "Камзол лазутчика-мастера",
		("B", "top") => "Камзол охотника на королей",
		_            => $"Light {g} {r}",
	};

	private static string RobeName(string g, string r) => (g, r) switch
	{
		("E", "low") => "Роба ученика",
		("D", "low") => "Мантия адепта",
		("D", "mid") => "Облачение чародея",
		("D", "top") => "Регалии волхва",
		("C", "low") => "Одеяние мага",
		("C", "mid") => "Облачение архимага",
		("C", "top") => "Регалии лорда магии",
		("B", "low") => "Мантия древнего",
		("B", "mid") => "Облачение теурга",
		("B", "top") => "Регалии магистра тайн",
		_            => $"Robe {g} {r}",
	};

	private static string HeavyName(string g, string r) => (g, r) switch
	{
		("D", "low") => "Латы новобранца",
		("D", "mid") => "Латы стражника",
		("D", "top") => "Латы рыцаря",
		("C", "low") => "Латы паладина",
		("C", "mid") => "Латы храмовника",
		("C", "top") => "Латы крестоносца",
		("B", "low") => "Латы инквизитора",
		("B", "mid") => "Латы командора",
		("B", "top") => "Латы избранного",
		_            => $"Heavy {g} {r}",
	};

	public static SetData Get(string setId)
		=> setId != null && Sets.TryGetValue(setId, out var s) ? s : null;

	// Все бонусы сета, активные при заданном числе надетых частей.
	public static IEnumerable<SetBonus> ActiveBonusesFor(SetData set, int partsEquipped)
	{
		if (set == null) yield break;
		foreach (var b in set.Bonuses)
			if (partsEquipped >= b.RequiredParts) yield return b;
	}
}
