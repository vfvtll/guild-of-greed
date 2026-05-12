namespace GuildOfGreed.Shared.Domain;

// Уникальный пассивный эффект оружия. POCO с двумя числовыми параметрами —
// разные эффекты используют их по-своему. Если эффекту нужно только одно
// число, Magnitude2 = 0 и игнорируется.
//
// Согласовано 2026-05-12 / 2026-05-12 (продолжение):
//   - bleed_on_hit (двуручные мечи): каждый физ.удар добавляет bleed по
//     гиперболической формуле dmg² × Magnitude / (100 × (dmg + 200)).
//     Реализация — CombatEngine.ApplyDamageToEnemy.
//   - power_per_non_attack (одноручные мечи): физ.урон карты × (1 + N × Magnitude/100),
//     где N — количество карт в руке с Effect != "damage_*" (non-attack).
//   - magic_chain (посох): каждое следующее атакующее маг.заклинание в одном
//     ходу даёт +Magnitude% урона и +Magnitude2% маны. Счётчик
//     SpellsCastThisTurn в BattleState; сбрасывается в начале нового хода.
//   - crit_bonus (ножи): зарезервировано, реализации пока нет.
//   - mana_leech, элементальные — будущие итерации.
//
// Kind — английский snake_case идентификатор.
public class WeaponPassive
{
	public string Kind;
	public int Magnitude;
	public int Magnitude2;

	public WeaponPassive() { }
	public WeaponPassive(string kind, int magnitude, int magnitude2 = 0)
	{
		Kind = kind; Magnitude = magnitude; Magnitude2 = magnitude2;
	}

	// Известные Kind'ы. Хранить как const'ы, чтобы typo в ItemsDB ловила
	// IDE/grep, а не дебаг боя.
	public const string BleedOnHit          = "bleed_on_hit";
	public const string PowerPerNonAttack   = "power_per_non_attack";
	public const string MagicChain          = "magic_chain";
	public const string CritBonus           = "crit_bonus";
}
