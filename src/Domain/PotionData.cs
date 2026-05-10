// POCO-описание зелья. Лежит в Domain.
// Реестр конкретных зелий — в Data/PotionsDB.cs.
//
// Зелье применяется через Combat.UsePotion(itemId) — там вычитается из инвентаря
// и применяются эффекты (HpRestore, MpRestore, и т.д.).
public class PotionData
{
	public string Id;
	public string Name;
	public string Description;
	public string Icon;        // emoji для UI

	// Эффекты при использовании.
	public int HpRestore;
	public int MpRestore;
	// Дальше можно добавить: ApplyEffect (status), DrawCards и т.д.
}
