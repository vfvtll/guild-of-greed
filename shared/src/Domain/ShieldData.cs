using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Щит — предмет для off-hand слота. Альтернатива второму одноручному оружию.
// Согласовано 2026-05-12:
//   - HandSize −1 (5 → 4 по дефолту).
//   - Три типа с уникальной защитной механикой (см. ShieldType).
//   - Поля Phys/MagDef работают как у брони (накапливаются в PhysDef()/MagDef()).
//   - Аффиксы — отдельным инкрементом (пока без них; пул будет защитный).
//
// Сериализуется в JSON напрямую как CharacterData.Shield.
public enum ShieldType
{
	// Магический: возвращает 10% полученного маг.урона врагу. Только MagDef.
	Magic      = 0,
	// Физический: в начале хода игрока даёт block = 10% от MaxHp. Только PhysDef.
	Physical   = 1,
	// Сбалансированный: половина обеих защит. Counter-buff: при получении
	// физ.урона → +20% маг.урона следующий ход; при маг.уроне → +20% физ.
	Balanced   = 2,
}

public class ShieldData
{
	public string Id;
	public string Name;
	public string Grade;
	public string Tier;
	public ItemRarity Rarity = ItemRarity.Common;
	public ShieldType Type;

	public int PhysDef;
	public int MagDef;

	// Magnitude эффекта типа в процентах. Для каждого типа значит своё:
	//   Magic    — % возврата урона (10 = 10%)
	//   Physical — % MaxHp в блоке в начале хода
	//   Balanced — % counter-buff (20 = +20% урона другого типа)
	public int EffectMagnitude;

	// Длительность counter-buff'а от balanced shield, ходов. Игнорируется
	// другими типами. Default 1 — следующий ход после получения урона.
	public int CounterBuffDuration = 1;

	public List<AppliedAffix> Affixes = new();

	public ShieldData Clone()
	{
		var c = (ShieldData)MemberwiseClone();
		c.Affixes = new List<AppliedAffix>(Affixes.Count);
		foreach (var a in Affixes) c.Affixes.Add(a.Clone());
		return c;
	}
}
