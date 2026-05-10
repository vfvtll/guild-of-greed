// POCO-описание одной части брони. Лежит в Domain потому что CharacterData
// держит ссылки на ArmorData как на 4 надетых куска.
//
// Конкретные экземпляры — в Data/ItemsDB.cs (статический реестр).
public enum ArmorSlot
{
	Chest,    // тело
	Helmet,   // шлем
	Gloves,   // перчатки
	Boots,    // сапоги
}

public class ArmorData
{
	public string Id;
	public string Name;
	public string Type;          // "robe" / "light"
	public ArmorSlot Slot;
	public string Grade;
	public string Tier;
	public int PhysDef;
	public int PhysAtkBonus;
	public int MagicAtkBonus;
	public int MagicAtkPct;
	public int MpMaxBonus;
	public int MpRegenBonus;
	public int HpBonus;
	public int ExtraDrawBonus;
	public string SuffixId;
	public string SuffixName;

	public ArmorData Clone() => (ArmorData)MemberwiseClone();
}
