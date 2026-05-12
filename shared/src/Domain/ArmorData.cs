using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// POCO-описание одной части брони. Лежит в Domain потому что CharacterData
// держит ссылки на ArmorData как на 4 надетых куска.
//
// Конкретные экземпляры — в Data/ItemsDB.cs (статический реестр).
//
// Поля Phys*/Magic*/MpMax*/Hp*/ExtraDraw — БАЗОВЫЕ значения предмета. К ним
// сверху накатываются аффиксы (Affixes — префиксы и суффиксы из AffixesDB).
// Расчёт эффективных стат-вкладов делается через AffixContribution helper'ы
// в CharacterData.SumArmor*.
public enum ArmorSlot
{
	Chest,    // тело
	Helmet,   // шлем
	Gloves,   // перчатки
	Boots,    // сапоги
	Amulet,   // амулет / ожерелье
	Ring1,    // кольцо 1 (предметы-кольца в БД помечены этим слотом по умолчанию;
	          //  при надевании движок сам выберет Ring1 или Ring2 если есть свободное)
	Ring2,    // кольцо 2 (используется только в SetArmorSlot/GetArmorSlot)
}

public class ArmorData
{
	public string Id;
	public string Name;
	public string Type;          // "robe" / "light"
	public ArmorSlot Slot;
	public string Grade;
	public string Tier;
	public ItemRarity Rarity = ItemRarity.Common;
	public int PhysDef;
	public int PhysAtkBonus;
	public int MagicAtkBonus;
	public int MagicAtkPct;
	public int MpMaxBonus;
	public int MpRegenBonus;
	public int HpBonus;
	public int ExtraDrawBonus;
	// Принадлежность к сету (И6.2 D-инкремент). Пустая строка = не в сете.
	// SetsDB.GetBySetId(SetId) даёт описание комплекта и бонусы.
	public string SetId = "";
	// Аффиксы экземпляра (И6.2). Пустой список = базовый предмет без ролла
	// (например стартовый light_chest_strength_low до того, как был улучшен).
	public List<AppliedAffix> Affixes = new();

	public ArmorData Clone()
	{
		var c = (ArmorData)MemberwiseClone();
		c.Affixes = new List<AppliedAffix>(Affixes.Count);
		foreach (var a in Affixes) c.Affixes.Add(a.Clone());
		return c;
	}
}
