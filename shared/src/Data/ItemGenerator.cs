using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Ролл предметов с аффиксами. Используется и при дропе лута (CombatEngine
// читает LootEntry.Affixed и вызывает Roll*), и в EnsureDefaults для гарантированных
// стартовых вещей.
//
// Контракт CSP:
//   - Принимает RandomSource из BattleState — клиент и сервер генерят
//     идентичные предметы при одном seed.
//   - Если rng=null, использует глобальный Rng (для UI/eDevServerEvents где
//     детерминизм не нужен — например, при создании стартовых вещей в
//     EnsureDefaults на клиенте до коннекта).
//
// Алгоритм Roll*:
//   1. Клонируем базу из ItemsDB.
//   2. Если rarity не передан — катаем случайный из AllowedRarities(grade).
//   3. По бюджету (AffixBudget.For(rarity)) выбираем уникальные префиксы и
//      суффиксы из AffixesDB, фильтруя ArmorOnly/WeaponOnly.
//   4. Заполняем Affixes и Rarity на клон.
//
// Имя предмета остаётся таким же — отображение префиксов/суффиксов в UI
// делается отдельно (см. ItemsDB.Describe* и Инк. F для inventory cards).
public static class ItemGenerator
{
	// Вероятности выпадения каждой rarity при random-rolle для данного grade.
	// Сумма всех весов в строке = 100. Нижние редкости намного чаще верхних.
	//
	// Грейд E:    Common 80 / Uncommon 20
	// Грейд D:    Common 65 / Uncommon 28 / Rare 7
	// Грейд C:    Common 50 / Uncommon 32 / Rare 14 / Heroic 4
	// Грейд B:    Common 38 / Uncommon 32 / Rare 18 / Heroic 9  / Epic 3
	// Грейд A:    Common 28 / Uncommon 30 / Rare 22 / Heroic 13 / Epic 6  / Legendary 1
	// Грейд S:    Common 18 / Uncommon 26 / Rare 26 / Heroic 18 / Epic 9  / Legendary 3
	private static readonly int[][] _rarityWeightsByGrade =
	{
		new[] { 80, 20,  0,  0,  0,  0 }, // E
		new[] { 65, 28,  7,  0,  0,  0 }, // D
		new[] { 50, 32, 14,  4,  0,  0 }, // C
		new[] { 38, 32, 18,  9,  3,  0 }, // B
		new[] { 28, 30, 22, 13,  6,  1 }, // A
		new[] { 18, 26, 26, 18,  9,  3 }, // S
	};

	public static ItemRarity RollRarity(ItemGrade grade, RandomSource rng)
	{
		var weights = _rarityWeightsByGrade[(int)grade];
		int total = 0;
		for (int i = 0; i < weights.Length; i++) total += weights[i];
		if (total <= 0) return ItemRarity.Common;
		int pick = rng != null ? rng.Next(total) : Rng.Next(total);
		int acc = 0;
		for (int i = 0; i < weights.Length; i++)
		{
			acc += weights[i];
			if (pick < acc) return (ItemRarity)i;
		}
		return ItemRarity.Common;
	}

	// =========================================================================
	// Броня
	// =========================================================================

	public static ArmorData RollArmor(string baseId, RandomSource rng, ItemRarity? forceRarity = null)
	{
		var src = ItemsDB.GetArmor(baseId);
		if (src == null) return null;
		var item = src.Clone();

		var grade = ItemGrades.Parse(item.Grade);
		var rarity = forceRarity ?? RollRarity(grade, rng);
		// Защита от ситуации forceRarity > AllowedRarity(grade) — clamp вниз.
		if (!ItemGrades.IsRarityAllowed(grade, rarity))
		{
			var allowed = ItemGrades.AllowedRarities(grade);
			rarity = allowed[allowed.Count - 1];
		}
		item.Rarity = rarity;
		item.Affixes = RollAffixes(rarity, grade, isWeapon: false, rng);
		return item;
	}

	// =========================================================================
	// Щиты
	// =========================================================================
	//
	// Аффиксы щита берутся из общего пула (isWeapon: false, как у брони) —
	// PhysDef/MagDef/Hp/Mp/MpRegen/HpRegen из AffixesDB.Prefixes/Suffixes.
	// Специфический «защитный пул» (отдельные аффиксы только для щитов)
	// зарезервирован, но пока не выделен.
	public static ShieldData RollShield(string baseId, RandomSource rng, ItemRarity? forceRarity = null)
	{
		var src = ShieldsDB.Get(baseId);
		if (src == null) return null;
		var item = src.Clone();

		var grade = ItemGrades.Parse(item.Grade);
		var rarity = forceRarity ?? RollRarity(grade, rng);
		if (!ItemGrades.IsRarityAllowed(grade, rarity))
		{
			var allowed = ItemGrades.AllowedRarities(grade);
			rarity = allowed[allowed.Count - 1];
		}
		item.Rarity = rarity;
		item.Affixes = RollAffixes(rarity, grade, isWeapon: false, rng);
		return item;
	}

	// =========================================================================
	// Оружие
	// =========================================================================

	public static WeaponData RollWeapon(string baseId, RandomSource rng, ItemRarity? forceRarity = null)
	{
		var src = ItemsDB.GetWeapon(baseId);
		if (src == null) return null;
		var item = src.Clone();

		var grade = ItemGrades.Parse(item.Grade);
		var rarity = forceRarity ?? RollRarity(grade, rng);
		if (!ItemGrades.IsRarityAllowed(grade, rarity))
		{
			var allowed = ItemGrades.AllowedRarities(grade);
			rarity = allowed[allowed.Count - 1];
		}
		item.Rarity = rarity;
		item.Affixes = RollAffixes(rarity, grade, isWeapon: true, rng);
		return item;
	}

	// =========================================================================
	// Внутреннее: выбор аффиксов
	// =========================================================================

	private static List<AppliedAffix> RollAffixes(ItemRarity rarity, ItemGrade grade,
		bool isWeapon, RandomSource rng)
	{
		var budget = AffixBudget.For(rarity);
		var list = new List<AppliedAffix>(budget.Prefixes + budget.Suffixes);

		// Префиксы: только уникальные kind'ы (нельзя два +PhysAtk на один предмет).
		var prefixPool = PoolFor(AffixesDB.Prefixes, isWeapon);
		PickUnique(prefixPool, budget.Prefixes, grade, list, rng);

		// Суффиксы: тоже уникальные kind'ы между собой, но могут пересечься с
		// префиксами по kind (+5 ФизАтк префиксом и +5% ФизАтк суффиксом — ок).
		var suffixPool = PoolFor(AffixesDB.Suffixes, isWeapon);
		PickUnique(suffixPool, budget.Suffixes, grade, list, rng);

		return list;
	}

	private static List<AffixDef> PoolFor(Dictionary<string, AffixDef> source, bool isWeapon)
	{
		var pool = new List<AffixDef>(source.Count);
		foreach (var def in source.Values)
		{
			if (isWeapon && def.ArmorOnly) continue;
			if (!isWeapon && def.WeaponOnly) continue;
			pool.Add(def);
		}
		return pool;
	}

	private static void PickUnique(List<AffixDef> pool, int count, ItemGrade grade,
		List<AppliedAffix> outList, RandomSource rng)
	{
		if (count <= 0 || pool.Count == 0) return;
		// Копия чтобы не мутировать исходный pool.
		var remaining = new List<AffixDef>(pool);
		// Внутри одного списка (Prefix или Suffix) не разрешаем дублирование
		// одного и того же AffixStatKind — это даёт скучный одностаковый
		// предмет вместо разнообразного.
		var usedKinds = new HashSet<AffixStatKind>();
		// Учитываем kinds, уже добавленные ранее (например префиксы при выборе суффикса).
		// Стоп, не учитываем: design = префиксы и суффиксы могут совпадать по kind.
		// Просто учитываем kind'ы внутри текущей ветки (prefixes XOR suffixes).
		AffixSlot? sameSlotFilter = pool.Count > 0 ? (AffixSlot?)pool[0].Slot : null;
		foreach (var a in outList)
		{
			if (sameSlotFilter.HasValue && a.Slot == sameSlotFilter.Value)
				usedKinds.Add(a.Kind);
		}

		while (count > 0 && remaining.Count > 0)
		{
			int idx = rng != null ? rng.Next(remaining.Count) : Rng.Next(remaining.Count);
			var def = remaining[idx];
			// Swap-remove — O(1).
			remaining[idx] = remaining[remaining.Count - 1];
			remaining.RemoveAt(remaining.Count - 1);

			if (usedKinds.Contains(def.Kind)) continue;
			usedKinds.Add(def.Kind);

			int magnitude = AffixesDB.MagnitudeForGrade(def, grade);
			outList.Add(new AppliedAffix(def.Id, def.Slot, def.Kind, magnitude));
			count--;
		}
	}
}
