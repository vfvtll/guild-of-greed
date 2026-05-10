// POCO-описание зелья.
// Реестр конкретных зелий — в Data/PotionsDB.cs.
//
// Зелье применяется через Combat.UsePotion(itemId) — вычитается из инвентаря,
// применяются эффекты:
//   - HpRestore / MpRestore — мгновенное восстановление
//   - Buff* — на игрока вешается StatusEffect (как от карты buff_magic)
public class PotionData
{
	public string Id;
	public string Name;
	public string Description;
	public string Icon;
	public ItemRarity Rarity = ItemRarity.Common;

	// Мгновенные эффекты.
	public int HpRestore;
	public int MpRestore;

	// Длительный эффект (опционально). Если BuffType пуст — не применяется.
	// Тип совпадает с типами в StatusEffect: "phys_dmg_pct", "magic_dmg_pct", "phys_taken_pct".
	public string BuffType;
	public float BuffAmount;
	public int BuffDuration;
}
