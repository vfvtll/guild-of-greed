namespace GuildOfGreed.Shared.Domain;

// Валюта хранится одним числом в медяках (Inventory.Money). Перевод:
//   100 медяков = 1 серебро
//   100 серебра = 1 золото  (т.е. 1 золото = 10000 медяков)
//
// Split / форматирование — общая логика, чтобы UI инвентаря, лог боя и
// будущий магазин показывали одинаково. shared/Domain намеренно не знает
// про BBCode — UI добавляет цвета сам.
public static class Currency
{
	public const long SilverPerGold  = 100;
	public const long CopperPerSilver = 100;
	public const long CopperPerGold   = CopperPerSilver * SilverPerGold; // 10000

	public static (long Gold, long Silver, long Copper) Split(long totalCopper)
	{
		if (totalCopper < 0) totalCopper = 0;
		long gold   = totalCopper / CopperPerGold;
		long rem    = totalCopper % CopperPerGold;
		long silver = rem / CopperPerSilver;
		long copper = rem % CopperPerSilver;
		return (gold, silver, copper);
	}

	// Короткая запись: "5з 12с 3м". Лидирующие нули-номиналы пропускаются,
	// но хотя бы один номинал всегда присутствует (0 → "0м").
	public static string FormatShort(long totalCopper)
	{
		var (g, s, c) = Split(totalCopper);
		if (g > 0) return $"{g}з {s}с {c}м";
		if (s > 0) return $"{s}с {c}м";
		return $"{c}м";
	}
}
