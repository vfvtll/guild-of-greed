using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// POCO-описание одного оружия. Лежит в Domain потому что CharacterData
// его держит как поле, и Domain не должен зависеть от Data.
//
// Конкретные экземпляры — в Data/ItemsDB.cs (статический реестр).
//
// PhysAtk/MagicAtk — БАЗОВЫЕ значения; аффиксы (Affixes) накатываются сверху.
// Кросс-аффиксы разрешены: меч может ролл'нуть MagAtk, посох — PhysAtk.
//
// TODO (Инк. E): уникальные пассивные эффекты по типу оружия —
//   - knife: повышенный crit chance/damage
//   - sword_1h / sword_2h: шанс наложить bleeding effect при ударе
//   - элементальный урон (Light/Dark/Poison) — отдельные AffixStatKind
// Сейчас Passives = пустой каркас; реализации будут в отдельном инкременте.
public class WeaponData
{
	public string Id;
	public string Name;
	public string Type;
	public string Grade;
	public string Tier;
	public ItemRarity Rarity = ItemRarity.Common;
	public int PhysAtk;
	public int MagicAtk;
	public float PhysMult = 1.0f;
	public float MagicMult = 1.0f;
	public int ExtraDraw;

	// Базовый кулдаун крита в атаках. DEX/10 уменьшает, нижний предел 2.
	public int CritEveryNAttacks = 999;

	// Аффиксы экземпляра (И6.2). См. ArmorData.Affixes для семантики.
	public List<AppliedAffix> Affixes = new();

	public WeaponData Clone()
	{
		var c = (WeaponData)MemberwiseClone();
		c.Affixes = new List<AppliedAffix>(Affixes.Count);
		foreach (var a in Affixes) c.Affixes.Add(a.Clone());
		return c;
	}
}
