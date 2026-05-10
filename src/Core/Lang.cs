using System.Collections.Generic;

// Локализация. Использование:
//   Lang.T("ui.combat.end_turn")
//   Lang.T("log.card_played", cardName, cost)
//
// Ключи — английский snake_case с пространством имён:
//   ui.<scene>.<element>     — UI-надписи
//   card.<id>.name|desc      — имена и описания карт
//   item.<id>.name           — имена предметов
//   log.<event>              — строки боевого лога
//   enemy.<id>.name|intent.X — имена и намерения врагов
//
// Для прототипа фаллбек: если ключ не найден в текущей локали — пробуем Ru, потом возвращаем сам ключ
// (видно в UI что нужно перевести).
//
// Когда таблицы вырастут — выносим в Locales/Ru.cs, Locales/En.cs или в JSON ресурсы.
public static class Lang
{
	public enum Locale { Ru, En }

	public static Locale Current = Locale.Ru;

	private static readonly Dictionary<Locale, Dictionary<string, string>> Strings = new()
	{
		[Locale.Ru] = new Dictionary<string, string>
		{
			// === UI ===
			["ui.combat.end_turn"]        = "Конец хода ▶",
			["ui.combat.weapon"]          = "⚔ Оружие",
			["ui.combat.armor"]           = "🛡 Броня",
			["ui.combat.location"]        = "🗺 Локация",
			["ui.combat.restart"]         = "↻ Новый бой",
			["ui.combat.reroll_stats"]    = "🎲 Статы",
			["ui.combat.player_panel"]    = "Игрок",
			["ui.combat.enemies_panel"]   = "Враги",
			["ui.combat.log_panel"]       = "Лог боя",
			["ui.combat.deck"]            = "Колода",
			["ui.combat.discard"]         = "Сброс",

			// === Заглушки для лога — расширяется по мере миграции ===
			["log.encounter_cleared"]     = "Локация зачищена!",
			["log.player_dead"]           = "Вы погибли. Все предметы потеряны.",
		},
		[Locale.En] = new Dictionary<string, string>
		{
			["ui.combat.end_turn"]        = "End Turn ▶",
			["ui.combat.weapon"]          = "⚔ Weapon",
			["ui.combat.armor"]           = "🛡 Armor",
			["ui.combat.location"]        = "🗺 Location",
			["ui.combat.restart"]         = "↻ New Fight",
			["ui.combat.reroll_stats"]    = "🎲 Stats",
			["ui.combat.player_panel"]    = "Player",
			["ui.combat.enemies_panel"]   = "Enemies",
			["ui.combat.log_panel"]       = "Combat Log",
			["ui.combat.deck"]            = "Deck",
			["ui.combat.discard"]         = "Discard",

			["log.encounter_cleared"]     = "Location cleared!",
			["log.player_dead"]           = "You died. All items lost.",
		},
	};

	public static string T(string key)
	{
		if (Strings.TryGetValue(Current, out var dict) && dict.TryGetValue(key, out var v))
			return v;
		// Fallback на русский
		if (Current != Locale.Ru && Strings[Locale.Ru].TryGetValue(key, out var ruFallback))
			return ruFallback;
		// Не нашли — возвращаем сам ключ (видно что надо перевести)
		return key;
	}

	public static string T(string key, params object[] args)
		=> string.Format(T(key), args);

	public static void SetLocale(Locale locale) => Current = locale;
}
