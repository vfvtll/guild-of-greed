using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Кузница: расчёт цен распыла, улучшения и реролла. Чистые функции.
// Используется и клиентом (UI), и сервером (валидация PushCharacter в будущем).
//
// === Экономика ============================================================
//
// База цены = функция от (grade, tier). 8 тиров с перекрытием:
//   Low E = 0, Mid E = Low D = 1, Top E = Mid D = 2, Top D = Mid C = 3, ...
//   Top S = 7. Формула tier = gradeIdx + rankIdx (E=0..S=5; low=0,mid=1,top=2).
//
// Линейка базы (1 → 10000 за 8 шагов, ~×3.7 за шаг):
//   1, 5, 15, 50, 200, 750, 2500, 10000
//
// Бонус за rarity при распыле (на полную базу):
//   Common 0%, Uncommon +10%, Rare +30%, Heroic +70%, Epic +150%, Legendary +310%.
//   Соответствует "половине вложенного в улучшение" — игрок может вернуть
//   половину инвестиций распылом.
//
// Стоимость улучшения по rarity (в % от базы):
//   Common→Uncommon 20%, →Rare 40%, →Heroic 80%, →Epic 160%, →Legendary 320%.
//   Удвоение каждый шаг.
//
// Стоимость реролла аффиксов: 25% от базы. Недорого и доступно.
//
// === Ограничения по grade ================================================
//   E: до Uncommon
//   D: до Rare
//   C: до Heroic
//   B: до Epic
//   A/S: до Legendary
public static class ForgeDB
{
	// База цены за тир (0 = Low E .. 7 = Top S). См. формулу TierIndex.
	private static readonly long[] BaseByTier = { 1, 5, 15, 50, 200, 750, 2500, 10000 };

	// Бонус к распылу за rarity, в % от базы (индекс = (int)ItemRarity).
	private static readonly int[] DismantleBonusPct = { 0, 10, 30, 70, 150, 310 };

	// Цена улучшения rarity → rarity+1, в % от базы (индекс = текущая rarity).
	private static readonly int[] UpgradeCostPct = { 20, 40, 80, 160, 320 };

	// Максимальная rarity по grade. E ограничен Uncommon, S — до Legendary.
	//
	// A и S оба упираются в Legendary преднамеренно: Legendary — высший
	// rarity-каркас (см. ItemRarity), кузничный кап у A и S совпадает.
	// Разница между ними — в прогрессии персонажа (S-предметы требуют S-grade
	// = уровни 101..120, A-предметы достижимы на 81..100). Кузнечный потолок
	// прокачки персонажа — 100 уровень; A-шмотки позволяют дойти до 90,
	// S-шмотки нужны чтобы пробить выше.
	private static readonly Dictionary<string, ItemRarity> MaxRarityByGrade = new()
	{
		["E"] = ItemRarity.Uncommon,
		["D"] = ItemRarity.Rare,
		["C"] = ItemRarity.Heroic,
		["B"] = ItemRarity.Epic,
		["A"] = ItemRarity.Legendary,
		["S"] = ItemRarity.Legendary,
	};

	// tier = gradeIdx + rankIdx. С перекрытием: Mid E (0+1=1) == Low D (1+0=1).
	public static int TierIndex(string grade, string rank)
	{
		int g = grade switch
		{
			"E" => 0, "D" => 1, "C" => 2, "B" => 3, "A" => 4, "S" => 5,
			_ => 0,
		};
		int r = rank switch
		{
			"low" => 0, "mid" => 1, "top" => 2,
			_ => 0,
		};
		int t = g + r;
		if (t < 0) t = 0;
		if (t >= BaseByTier.Length) t = BaseByTier.Length - 1;
		return t;
	}

	public static long BasePrice(string grade, string rank)
		=> BaseByTier[TierIndex(grade, rank)];

	// Сколько эссенции вернёт распыл предмета этой grade/tier/rarity.
	public static long DismantleEssence(string grade, string rank, ItemRarity rarity)
	{
		long b = BasePrice(grade, rank);
		int idx = (int)rarity;
		if (idx < 0) idx = 0;
		if (idx >= DismantleBonusPct.Length) idx = DismantleBonusPct.Length - 1;
		int bonus = DismantleBonusPct[idx];
		return b * (100 + bonus) / 100;
	}

	// Стоимость улучшения rarity → rarity+1 в эссенции. -1 если нельзя
	// (уже на потолке для grade или уже Legendary).
	// Math.Max(1, …) защищает от целочисленного зануления: для Low E (BasePrice=1)
	// Common→Uncommon = 1*20/100 = 0, а 0 эссенции ломает UI-условие "кнопка
	// активна если cost>0" и логически странно (бесплатный ап). Минимум 1.
	public static long UpgradeCost(string grade, string rank, ItemRarity currentRarity)
	{
		if (!CanUpgrade(grade, currentRarity)) return -1;
		int idx = (int)currentRarity;
		if (idx < 0) idx = 0;
		if (idx >= UpgradeCostPct.Length) idx = UpgradeCostPct.Length - 1;
		return Math.Max(1, BasePrice(grade, rank) * UpgradeCostPct[idx] / 100);
	}

	// Может ли предмет ещё подняться по rarity (учитывает кап grade).
	public static bool CanUpgrade(string grade, ItemRarity currentRarity)
	{
		if (currentRarity >= ItemRarity.Legendary) return false;
		var max = MaxRarityByGrade.TryGetValue(grade ?? "E", out var m) ? m : ItemRarity.Uncommon;
		return currentRarity < max;
	}

	public static ItemRarity NextRarity(ItemRarity r)
		=> (ItemRarity)Math.Min((int)r + 1, (int)ItemRarity.Legendary);

	public static ItemRarity MaxRarityFor(string grade)
		=> MaxRarityByGrade.TryGetValue(grade ?? "E", out var m) ? m : ItemRarity.Uncommon;

	// Стоимость реролла аффиксов. Не меняет rarity — только перекатывает
	// префиксы/суффиксы по тому же бюджету. 25% от базы.
	public static long RerollCost(string grade, string rank)
		=> Math.Max(1, BasePrice(grade, rank) / 4);
}
