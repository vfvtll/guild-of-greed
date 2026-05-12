namespace GuildOfGreed.Shared.Domain;

// POCO-описание одной карты. Лежит в Domain.
// Конкретные экземпляры и расчётные хелперы — в Data/CardsDB.cs.
//
// Card.Effect — что делает карта при разыгрывании:
//   damage_phys  — физ. урон по врагу
//   damage_magic — маг. урон по врагу
//   block        — щит игроку
//   heal         — восстановление ХП
//   debuff_phys  — на врага: +X% получаемого физ. урона N ходов
//   buff_magic   — на игрока: +X% наносимого маг. урона N ходов
public class CardData
{
	public string Id;
	public string Name;
	public string Archetype;   // "warrior" / "mage"
	public string Type;        // "physical" / "magic"
	public int Cost;
	public int BaseDamage;
	public int Block;
	public int Heal;
	public int AmountPct;
	public int Duration;
	public string Effect;
	public string Icon;        // emoji в центре карты

	// Карта наносит прямой урон по врагу. Используется пассивами оружия
	// (power_per_non_attack считает руки без attack-карт; magic_chain
	// тикает только на attack маг.заклинаниях). В будущем заменится
	// более богатой системой типов (см. roadmap).
	public bool IsAttack => Effect == "damage_phys" || Effect == "damage_magic";
	public bool IsMagicAttack => Effect == "damage_magic";
}
