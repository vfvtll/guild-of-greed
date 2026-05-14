using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// База крафта: рецепты на E/D предметы + формулы XP / уровня / рарити-кэпа.
// См. .claude_design_crafting.md в корне репо для полной спеки.
//
// Рецепт = (resultItemId, skillId, list[ingredientId, count]).
// Базовый уровень: рецепты на все E-предметы из ItemsDB.Weapons/Armors и
// на D-предметы, которые догенерирует ItemsCatalog (light/robe/heavy ×
// 4 слота × low/mid/top). E и D — открыты с 1 уровня соответствующего
// скилла, без рецептов-предметов (как описано в design-doc §2).
public static class CraftingDB
{
	// =========================================================================
	// XP / Level
	// =========================================================================

	// XP до достижения каждого ключевого уровня (опорные точки из спеки).
	// Линейная интерполяция между breakpoints даёт монотонную кривую.
	// 0 → 0 ; 20 → 1000 ; 40 → 5000 ; 60 → 30 000 ; 80 → 230 000 ; 100 → 1 230 000
	private static readonly int[] _xpAtLevel = { 0, 1000, 5000, 30000, 230000, 1230000 };

	public static int XpForLevel(int level)
	{
		if (level <= 0) return 0;
		if (level >= 100) return _xpAtLevel[5];
		int seg = level / 20;             // 0..4
		int low = _xpAtLevel[seg];
		int high = _xpAtLevel[seg + 1];
		int lvlLow = seg * 20;
		int lvlHigh = lvlLow + 20;
		return low + (high - low) * (level - lvlLow) / (lvlHigh - lvlLow);
	}

	public static int LevelFromXp(int xp)
	{
		if (xp <= 0) return 0;
		// Простой линейный поиск — уровней всего 100.
		for (int lvl = 0; lvl <= 100; lvl++)
			if (XpForLevel(lvl + 1) > xp) return lvl;
		return 100;
	}

	// Максимальный уровень, до которого XP за указанный грейд начисляется.
	// E: 30, D: 30, C: 50, B: 70, A: 90, S: 100. Дальше — крафт грейда не
	// даёт XP (плато), нужно перейти на следующий грейд.
	public static int LevelCapForGrade(string grade) => grade switch
	{
		"E" => 30, "D" => 30, "C" => 50, "B" => 70, "A" => 90, "S" => 100,
		_   => 30,
	};

	// Минимальный уровень скилла для крафта предмета указанного грейда.
	// E/D — с 1-го уровня, C — 20, B — 40, A — 60, S — 80.
	public static int MinLevelForGrade(string grade) => grade switch
	{
		"E" => 0, "D" => 0, "C" => 20, "B" => 40, "A" => 60, "S" => 80,
		_   => 0,
	};

	// XP, который даёт один крафт предмета (грейд+tier). low:mid:top = 1:2:4.
	public static int CraftXp(string grade, string tier)
	{
		int top = grade switch
		{
			"E" => 50, "D" => 100, "C" => 200, "B" => 500, "A" => 2000, "S" => 5000,
			_   => 50,
		};
		return tier switch
		{
			"top" => top, "mid" => top / 2, "low" => top / 4, _ => top / 4,
		};
	}

	// =========================================================================
	// Skill ID resolution
	// =========================================================================

	// Резолв ID скилла по типу предмета. Из общих типов покрываем все
	// существующие в ItemsDB / ItemsCatalog: знакомые сейчас:
	//   sword_1h, sword_2h, knife, staff  (оружие)
	//   light, robe, heavy                (броня)
	// Бижутерия (amulet/ring) — пока не крафтится (нет рецептов и нет скилла).
	public static string SkillIdForType(string itemType) => itemType switch
	{
		"sword_1h" => "craft_sword_1h",
		"sword_2h" => "craft_sword_2h",
		"knife"    => "craft_knife",
		"staff"    => "craft_staff",
		"light"    => "craft_light",
		"robe"     => "craft_robe",
		"heavy"    => "craft_heavy",
		_ => null,
	};

	// Локализованное имя скилла — для UI крафта в городе.
	public static string SkillDisplayName(string skillId) => skillId switch
	{
		"craft_sword_1h" => "Кузнечное дело: одноручные мечи",
		"craft_sword_2h" => "Кузнечное дело: двуручные мечи",
		"craft_knife"    => "Кузнечное дело: кинжалы",
		"craft_staff"    => "Резьба по дереву: посохи",
		"craft_light"    => "Кожевенное дело: лёгкая броня",
		"craft_robe"     => "Ткачество: робы",
		"craft_heavy"    => "Кузнечное дело: тяжёлая броня",
		_ => skillId ?? "—",
	};

	public static IEnumerable<string> AllSkillIds()
	{
		yield return "craft_sword_1h";
		yield return "craft_sword_2h";
		yield return "craft_knife";
		yield return "craft_staff";
		yield return "craft_light";
		yield return "craft_robe";
		yield return "craft_heavy";
	}

	// =========================================================================
	// Rarity cap from skill level
	// =========================================================================

	// Кэп редкости в зависимости от уровня скилла. На скилле 0–9 крафт даёт
	// только Common, на 10–19 — до Uncommon, и т.д.; внутри окна катаем
	// случайно по весам ItemGenerator.RollRarity, но клампим сверху.
	public static ItemRarity MaxRarityForSkillLevel(int level)
	{
		if (level >= 50) return ItemRarity.Legendary;
		if (level >= 40) return ItemRarity.Epic;
		if (level >= 30) return ItemRarity.Heroic;
		if (level >= 20) return ItemRarity.Rare;
		if (level >= 10) return ItemRarity.Uncommon;
		return ItemRarity.Common;
	}

	// =========================================================================
	// Recipes
	// =========================================================================

	public class Ingredient
	{
		public string ResourceId;
		public int Count;
		public Ingredient(string id, int c) { ResourceId = id; Count = c; }
	}

	public class Recipe
	{
		public string ResultItemId;
		public string SkillId;
		public string Grade;
		public string Tier;
		public string Type;
		public List<Ingredient> Ingredients;
	}

	// Резолв рецепта по itemId. Возвращает null если предмет не крафтится:
	//   - нет в ItemsDB / ItemsCatalog;
	//   - тип без скилла (бижутерия — amulet/ring);
	//   - low-E (см. design-doc §10: low E падает с дропа, крафт начинается с mid).
	public static Recipe Resolve(string itemId)
	{
		if (string.IsNullOrEmpty(itemId)) return null;

		string type, grade, tier;
		var weapon = ItemsDB.GetWeapon(itemId);
		if (weapon != null)
		{
			type = weapon.Type; grade = weapon.Grade; tier = weapon.Tier;
		}
		else
		{
			var armor = ItemsDB.GetArmor(itemId);
			if (armor == null) return null;
			// Бижутерию пока не крафтим.
			if (armor.Slot == ArmorSlot.Amulet || armor.Slot == ArmorSlot.Ring1 || armor.Slot == ArmorSlot.Ring2)
				return null;
			type = armor.Type; grade = armor.Grade;
			// Legacy предметы (robe_chest_power_low, robe_helmet_low, ...) хранят
			// Tier="low" вне зависимости от настоящего тира сета. Source of truth —
			// суффикс SetId. Это влияет и на отображение, и на ингредиенты.
			tier = EffectiveTier(armor);
		}

		// low-E не крафтится — выпадает естественным дропом с E-мобов.
		if (grade == "E" && tier == "low") return null;

		string skillId = SkillIdForType(type);
		if (skillId == null) return null;

		var ings = IngredientsFor(type, grade, tier, isWeapon: weapon != null);
		if (ings == null || ings.Count == 0) return null;

		return new Recipe
		{
			ResultItemId = itemId,
			SkillId = skillId,
			Grade = grade,
			Tier = tier,
			Type = type,
			Ingredients = ings,
		};
	}

	// Реальный tier предмета: предпочитаем суффикс SetId, потому что
	// у legacy-предметов поле Tier застряло как "low".
	private static string EffectiveTier(ArmorData armor)
	{
		if (!string.IsNullOrEmpty(armor.SetId))
		{
			if (armor.SetId.EndsWith("_top")) return "top";
			if (armor.SetId.EndsWith("_mid")) return "mid";
			if (armor.SetId.EndsWith("_low")) return "low";
		}
		return string.IsNullOrEmpty(armor.Tier) ? "low" : armor.Tier;
	}

	// Все рецепты текущего грейда (для UI вкладок). На базовом уровне —
	// только E и D. Перечисление дёргает Resolve для каждого предмета
	// соответствующего грейда.
	public static IEnumerable<Recipe> RecipesByGrade(string grade)
	{
		// Weapons in ItemsDB.
		foreach (var w in ItemsDB.Weapons.Values)
		{
			if (w.Grade != grade) continue;
			var r = Resolve(w.Id);
			if (r != null) yield return r;
		}
		// Armors (incl. auto-generated через ItemsCatalog).
		foreach (var a in ItemsDB.Armors.Values)
		{
			if (a.Grade != grade) continue;
			if (a.Slot == ArmorSlot.Amulet || a.Slot == ArmorSlot.Ring1 || a.Slot == ArmorSlot.Ring2)
				continue;
			var r = Resolve(a.Id);
			if (r != null) yield return r;
		}
	}

	// =========================================================================
	// Ingredient profiles
	// =========================================================================

	// Базовые количества ингредиентов на tier=low. Для mid/top масштабируем
	// (см. TierMul). Грейд определяет грейд ресурса (E-вещь = E-ресурсы,
	// D-вещь = D-ресурсы).
	private static List<Ingredient> IngredientsFor(string type, string grade, string tier, bool isWeapon)
	{
		string g = grade;
		int mul = TierMul(tier);

		// Слот-агностично для брони: тяжесть зависит от части — chest самый
		// дорогой. Но базовый уровень: разница только по архетипу, не по
		// слоту. Подкрутим позже если нужно (TBD design-doc §12).
		List<Ingredient> ings = new();
		switch (type)
		{
			// === Оружие ===
			case "sword_1h":
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Ore, g),     2 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Leather, g), 1 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Wood, g),    1 * mul));
				break;
			case "sword_2h":
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Ore, g),     4 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Leather, g), 1 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Wood, g),    2 * mul));
				break;
			case "knife":
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Ore, g),     1 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Leather, g), 1 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Wood, g),    1 * mul));
				break;
			case "staff":
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Wood, g),    3 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Essence, g), 2 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Cloth, g),   1 * mul));
				break;

			// === Броня ===
			case "light":
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Leather, g), 3 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Cloth, g),   1 * mul));
				break;
			case "robe":
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Cloth, g),   3 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Essence, g), 1 * mul));
				break;
			case "heavy":
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Ore, g),     4 * mul));
				ings.Add(new(ResourcesDB.Id(ResourcesDB.Kind.Leather, g), 1 * mul));
				break;

			default:
				return null;
		}
		return ings;
	}

	private static int TierMul(string tier) => tier switch
	{
		"top" => 3, "mid" => 2, "low" => 1, _ => 1,
	};
}
