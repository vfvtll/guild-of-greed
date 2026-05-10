// POCO-описание одного оружия. Лежит в Domain потому что CharacterData
// его держит как поле, и Domain не должен зависеть от Data.
//
// Конкретные экземпляры — в Data/ItemsDB.cs (статический реестр).
public class WeaponData
{
	public string Id;
	public string Name;
	public string Type;
	public string Grade;
	public string Tier;
	public int PhysAtk;
	public int MagicAtk;
	public float PhysMult = 1.0f;
	public float MagicMult = 1.0f;
	public int ExtraDraw;

	// Базовый кулдаун крита в атаках. DEX/10 уменьшает, нижний предел 2.
	public int CritEveryNAttacks = 999;

	public WeaponData Clone() => (WeaponData)MemberwiseClone();
}
