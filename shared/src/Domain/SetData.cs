using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Комплект (сет) — группа предметов, дающая дополнительные пассивные бонусы
// при сборе нескольких частей. Согласовано 2026-05-12:
//   - Минимум 3 сета на каждый Grade (low / mid / top — это варианты).
//   - Бонусы повторяются между грейдами с множителем (D=X, B=1.5X, ...).
//   - Бонус активируется при сборе N надетых частей сета.
//
// Привязка: предмет помечен `SetId` (см. ArmorData.SetId). Регистрация
// (какие предметы входят в какой сет) дублируется в SetData.PartIds — для
// валидации и для лёгкого подсчёта "сколько частей надето".
//
// Бонусы того же типа что и в AffixStatKind. Префиксы — IsPercent=false
// (плоский бонус, кладётся в SetFlat). Суффиксы — IsPercent=true (процент,
// в SetPct).
public class SetData
{
	public string Id;
	public string Name;
	public string Grade;                      // "E".."S"
	public string Variant;                    // "low" / "mid" / "top"
	public List<string> PartIds = new();      // ID частей (ArmorData.Id)
	public List<SetBonus> Bonuses = new();
}

public class SetBonus
{
	public int RequiredParts;                 // С какого количества активен
	public AffixStatKind Kind;
	public int Magnitude;
	public bool IsPercent;                    // true=%, false=плоский

	public SetBonus() { }
	public SetBonus(int parts, AffixStatKind kind, int magnitude, bool isPercent)
	{
		RequiredParts = parts; Kind = kind; Magnitude = magnitude; IsPercent = isPercent;
	}
}
