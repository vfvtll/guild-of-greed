using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Реестр всех комплектов (сетов) в игре. Согласовано: минимум 3 на Grade.
//
// Сейчас на E-grade полностью покрыты только варианты low (light/strength)
// и mid (robe/wisdom). Top-варианты — TODO когда в ItemsDB появятся новые
// 4-piece группы (нужны отдельные шлем/перчатки/сапоги, чтобы не пересекались
// с low). См. .claude_design_items.md.
//
// Бонусы кладутся прогрессивно: чем больше частей сета — тем сильнее. Сейчас
// у нас сеты по 4 части (chest/helmet/gloves/boots), бонусы — на 2 и 4
// надетые части. Бижутерия (амулеты, кольца) в сеты пока не входит — слишком
// много вариаций; позже можно расширить через те же PartIds.
//
// Привязка предмета к сету = ArmorData.SetId. Регистрация дублируется здесь
// (PartIds) для валидации и UI-отображения "сколько ты надел".
public static class SetsDB
{
	public static readonly Dictionary<string, SetData> Sets = new()
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

		// === E-grade / top — TODO: пока используем robe/power chest как 4-piece,
		// но шлем/перчатки/сапоги те же — это конфликт с robe_e_mid. Чтобы не
		// дублировать parts, top-вариант временно из 1 части (chest only) с
		// одним бонусом. Полноценный 4-piece top появится с новыми предметами.
		["robe_e_top"] = new()
		{
			Id = "robe_e_top", Name = "Печать чаромага",
			Grade = "E", Variant = "top",
			PartIds = new() { "robe_chest_power_low" },
			Bonuses = new()
			{
				new SetBonus(1, AffixStatKind.MagAtk, 8, isPercent: true),
			},
		},
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
