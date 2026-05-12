using System.Text.Json.Serialization;

namespace GuildOfGreed.Shared.Domain;

// Аффиксы — модификаторы, которые катаются при ролле предмета. Каждый
// предмет (оружие/броня) имеет список AppliedAffix; их количество зависит
// от Rarity (см. AffixBudget в ItemGrade.cs).
//
// Аффикс делится на:
//   - Prefix (плоский бонус, например "+3 ФизАтк") — даёт абсолютный прирост.
//   - Suffix (процентный, например "+5% ФизАтк") — даёт пропорциональный.
//
// Каждый аффикс bound to AffixStatKind — какому стату он добавляет.
// Magnitude хранится в AppliedAffix; Slot и Kind тоже хранятся прямо там
// (не только Id), чтобы предметы оставались self-contained: если в будущем
// AffixesDB поменяет magnitudes или удалит аффикс, старые предметы продолжат
// работать с зафиксированными значениями.
//
// Сериализуется в JSON как часть ArmorData/WeaponData; пишется в SQLite через
// character_json. ProtocolVersion бампается при изменении полей здесь.

public enum AffixSlot
{
	Prefix = 0,
	Suffix = 1,
}

public enum AffixStatKind
{
	// Атака
	PhysAtk = 0,
	MagAtk  = 1,
	// Защита
	PhysDef = 2,
	MagDef  = 3,
	// Ресурсы
	Hp      = 4,
	Mp      = 5,
	HpRegen = 6,
	MpRegen = 7,
	// Зарезервировано (в будущем — элементальный урон/резисты, см.
	// .claude_design_items.md "Уникальные пассивные эффекты оружия").
	// Light = 8, Dark = 9, Poison = 10, ...
}

// Применённый к конкретному экземпляру предмета аффикс. POCO, сериализуется.
//
//   Prefix: Magnitude = плоское значение (+3 ФизАтк → Magnitude=3).
//   Suffix: Magnitude = проценты (+5% ФизАтк → Magnitude=5).
//
// Id — для резолва имени и описания через AffixesDB (он же стабильный
// идентификатор для UI / тултипов).
public class AppliedAffix
{
	public string Id;
	public AffixSlot Slot;
	public AffixStatKind Kind;
	public int Magnitude;

	public AppliedAffix() { }
	public AppliedAffix(string id, AffixSlot slot, AffixStatKind kind, int magnitude)
	{
		Id = id; Slot = slot; Kind = kind; Magnitude = magnitude;
	}

	public AppliedAffix Clone() => (AppliedAffix)MemberwiseClone();

	[JsonIgnore]
	public bool IsPercent => Slot == AffixSlot.Suffix;
}

// Определение аффикса в реестре AffixesDB. Не сериализуется (живёт только
// в коде); ролл превращает AffixDef → AppliedAffix с конкретной magnitude.
//
// BaseMagnitude — на E-grade. Для более высоких грейдов скейлится через
// AffixesDB.MagnitudeForGrade. ArmorOnly/WeaponOnly ограничивают пул при
// ролле, но это soft-pool: кросс-аффиксы разрешены (см. design doc), так
// что большинство аффиксов = both.
public class AffixDef
{
	public string Id;
	public string Name;            // Отображаемое имя ("Силы", "Жизни"). Русский.
	public AffixSlot Slot;
	public AffixStatKind Kind;
	public int BaseMagnitude;      // На E-grade.
	public bool ArmorOnly;
	public bool WeaponOnly;
}
