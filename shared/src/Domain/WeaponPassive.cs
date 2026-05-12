namespace GuildOfGreed.Shared.Domain;

// Уникальный пассивный эффект оружия. POCO с параметром Magnitude —
// разные оружия одного типа могут иметь разную силу эффекта.
//
// Согласовано 2026-05-12:
//   - bleed_on_hit (двуручные мечи): каждый физический удар добавляет
//     bleed.stack врагу = Magnitude% от нанесённого по HP урона. В конце
//     хода врага стак уменьшается на enemy.HpRegen и наносит урон.
//     Стак НЕ сбрасывается между ходами — копится.
//   - crit_bonus (ножи): зарезервировано, реализации пока нет.
//   - mana_leech, элементальные — будущие итерации.
//
// Kind — английский snake_case идентификатор. Magnitude — целое число,
// семантика зависит от Kind:
//   bleed_on_hit: процент от урона (5 = 5%).
//   crit_bonus:   процент к шансу/урону крита (TODO).
public class WeaponPassive
{
	public string Kind;
	public int Magnitude;

	public WeaponPassive() { }
	public WeaponPassive(string kind, int magnitude)
	{
		Kind = kind; Magnitude = magnitude;
	}

	// Известные Kind'ы. Хранить как const'ы, чтобы typo в ItemsDB ловила
	// IDE/grep, а не дебаг боя.
	public const string BleedOnHit = "bleed_on_hit";
	public const string CritBonus  = "crit_bonus";
}
