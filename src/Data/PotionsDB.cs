using System.Collections.Generic;

// Реестр зелий. POCO PotionData — в Domain/PotionData.cs.
public static class PotionsDB
{
	public static readonly Dictionary<string, PotionData> Potions = new()
	{
		// === Малые (Common) — стартовый запас и обычный лут ===
		["potion_hp_small"] = new()
		{
			Id = "potion_hp_small",
			Name = "Малое зелье жизни",
			Icon = "🧪",
			Description = "Восстанавливает 40 ХП.",
			HpRestore = 40,
			Rarity = ItemRarity.Common,
		},
		["potion_mp_small"] = new()
		{
			Id = "potion_mp_small",
			Name = "Малое зелье маны",
			Icon = "💧",
			Description = "Восстанавливает 30 МП.",
			MpRestore = 30,
			Rarity = ItemRarity.Common,
		},

		// === Средние (Uncommon) — бошее восстановление, доступнее на грейде D ===
		["potion_hp_medium"] = new()
		{
			Id = "potion_hp_medium",
			Name = "Зелье жизни",
			Icon = "🧪",
			Description = "Восстанавливает 80 ХП.",
			HpRestore = 80,
			Rarity = ItemRarity.Uncommon,
		},
		["potion_mp_medium"] = new()
		{
			Id = "potion_mp_medium",
			Name = "Зелье маны",
			Icon = "💧",
			Description = "Восстанавливает 60 МП.",
			MpRestore = 60,
			Rarity = ItemRarity.Uncommon,
		},

		// === Бафф-эликсиры (Uncommon/Rare) ===
		["potion_strength"] = new()
		{
			Id = "potion_strength",
			Name = "Эликсир ярости",
			Icon = "💪",
			Description = "+30% физического урона на 3 хода.",
			Rarity = ItemRarity.Uncommon,
			BuffType = "phys_dmg_pct",
			BuffAmount = 30,
			BuffDuration = 3,
		},
		["potion_focus"] = new()
		{
			Id = "potion_focus",
			Name = "Эликсир фокуса",
			Icon = "✨",
			Description = "+30% магического урона на 3 хода.",
			Rarity = ItemRarity.Uncommon,
			BuffType = "magic_dmg_pct",
			BuffAmount = 30,
			BuffDuration = 3,
		},

		// === Полное восстановление (Rare) ===
		["potion_full"] = new()
		{
			Id = "potion_full",
			Name = "Великое зелье жизни и маны",
			Icon = "💎",
			Description = "Полностью восстанавливает ХП и МП.",
			HpRestore = 9999,
			MpRestore = 9999,
			Rarity = ItemRarity.Rare,
		},
	};

	public static PotionData Get(string id)
		=> Potions.TryGetValue(id, out var p) ? p : null;

	public static IEnumerable<PotionData> All() => Potions.Values;
}
