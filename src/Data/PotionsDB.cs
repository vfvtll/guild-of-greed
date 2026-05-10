using System.Collections.Generic;

// Реестр зелий. POCO PotionData — в Domain/PotionData.cs.
public static class PotionsDB
{
	public static readonly Dictionary<string, PotionData> Potions = new()
	{
		["potion_hp_small"] = new()
		{
			Id = "potion_hp_small",
			Name = "Зелье жизни",
			Icon = "🧪",
			Description = "Восстанавливает 40 ХП.",
			HpRestore = 40,
		},
		["potion_mp_small"] = new()
		{
			Id = "potion_mp_small",
			Name = "Зелье маны",
			Icon = "💧",
			Description = "Восстанавливает 30 МП.",
			MpRestore = 30,
		},
	};

	public static PotionData Get(string id)
		=> Potions.TryGetValue(id, out var p) ? p : null;

	public static IEnumerable<PotionData> All() => Potions.Values;
}
