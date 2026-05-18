using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Реестр всех аффиксов в игре. Делится на префиксы (плоские бонусы) и
// суффиксы (процентные). При ролле предмета (Инк. C — ItemGenerator)
// выбирается N префиксов и M суффиксов из соответствующих пулов с учётом
// ArmorOnly/WeaponOnly.
//
// Magnitude в AffixDef — базовая, для E-grade. На более высоких грейдах
// автоматически масштабируется через MagnitudeForGrade. Округление —
// математическое (Round), потому что magnitudes маленькие и floor округлял
// бы слабые аффиксы в 0.
//
// Imя аффикса (Name) — на русском. ID — английский snake_case.
public static class AffixesDB
{
	// Множитель magnitude по грейду. E=1.0 — база. На S magnitudes примерно
	// в 4 раза мощнее E, что компенсирует выросшую сложность контента и
	// число надетых частей сета (см. Инк. D).
	private static readonly float[] _gradeMultiplier =
	{
		1.0f,   // E
		1.5f,   // D
		2.0f,   // C
		2.6f,   // B
		3.2f,   // A
		4.0f,   // S
	};

	// =====================================================================
	// Префиксы (плоские)
	// Магnitude = абсолютная прибавка к стату на E-grade.
	// =====================================================================
	public static readonly Dictionary<string, AffixDef> Prefixes = new()
	{
		// Атака
		["sharp"]      = new() { Id = "sharp",      Name = "Острый",       Slot = AffixSlot.Prefix, Kind = AffixStatKind.PhysAtk, BaseMagnitude = 3 },
		["arcane"]     = new() { Id = "arcane",     Name = "Чарованный",   Slot = AffixSlot.Prefix, Kind = AffixStatKind.MagAtk,  BaseMagnitude = 3 },
		// Защита (по дизайну — только на броне/щите, на оружие НЕ катится)
		["sturdy"]     = new() { Id = "sturdy",     Name = "Крепкий",      Slot = AffixSlot.Prefix, Kind = AffixStatKind.PhysDef, BaseMagnitude = 2, ArmorOnly = true },
		["warded"]     = new() { Id = "warded",     Name = "Защищённый",   Slot = AffixSlot.Prefix, Kind = AffixStatKind.MagDef,  BaseMagnitude = 2, ArmorOnly = true },
		// Ресурсы
		["robust"]     = new() { Id = "robust",     Name = "Здоровый",     Slot = AffixSlot.Prefix, Kind = AffixStatKind.Hp,      BaseMagnitude = 12 },
		["mystic"]     = new() { Id = "mystic",     Name = "Мистический",  Slot = AffixSlot.Prefix, Kind = AffixStatKind.Mp,      BaseMagnitude = 10 },
		["regenerate"] = new() { Id = "regenerate", Name = "Восполняющий", Slot = AffixSlot.Prefix, Kind = AffixStatKind.HpRegen, BaseMagnitude = 1 },
		["channeled"]  = new() { Id = "channeled",  Name = "Сосредоточен", Slot = AffixSlot.Prefix, Kind = AffixStatKind.MpRegen, BaseMagnitude = 2 },
	};

	// =====================================================================
	// Суффиксы (процентные)
	// Магnitude = процент прибавки к стату на E-grade.
	// =====================================================================
	public static readonly Dictionary<string, AffixDef> Suffixes = new()
	{
		// Атака %
		["of_power"]     = new() { Id = "of_power",     Name = "Силы",       Slot = AffixSlot.Suffix, Kind = AffixStatKind.PhysAtk, BaseMagnitude = 5 },
		["of_magic"]     = new() { Id = "of_magic",     Name = "Магии",      Slot = AffixSlot.Suffix, Kind = AffixStatKind.MagAtk,  BaseMagnitude = 5 },
		// Защита % (по дизайну — только на броне/щите, на оружие НЕ катится)
		["of_stone"]     = new() { Id = "of_stone",     Name = "Камня",      Slot = AffixSlot.Suffix, Kind = AffixStatKind.PhysDef, BaseMagnitude = 8, ArmorOnly = true },
		["of_warding"]   = new() { Id = "of_warding",   Name = "Оберегания", Slot = AffixSlot.Suffix, Kind = AffixStatKind.MagDef,  BaseMagnitude = 8, ArmorOnly = true },
		// Ресурсы %
		["of_vitality"]  = new() { Id = "of_vitality",  Name = "Жизни",      Slot = AffixSlot.Suffix, Kind = AffixStatKind.Hp,      BaseMagnitude = 6 },
		["of_mana"]      = new() { Id = "of_mana",      Name = "Маны",       Slot = AffixSlot.Suffix, Kind = AffixStatKind.Mp,      BaseMagnitude = 8 },
		["of_recovery"]  = new() { Id = "of_recovery",  Name = "Восстановления", Slot = AffixSlot.Suffix, Kind = AffixStatKind.HpRegen, BaseMagnitude = 10 },
		["of_wisdom"]    = new() { Id = "of_wisdom",    Name = "Мудрости",   Slot = AffixSlot.Suffix, Kind = AffixStatKind.MpRegen, BaseMagnitude = 10 },
	};

	// =====================================================================
	// Доступ
	// =====================================================================

	public static AffixDef Get(string id)
	{
		if (id == null) return null;
		if (Prefixes.TryGetValue(id, out var p)) return p;
		if (Suffixes.TryGetValue(id, out var s)) return s;
		return null;
	}

	// Magnitude аффикса с учётом грейда базы. Используется ItemGenerator
	// при ролле и UI при отображении.
	public static int MagnitudeForGrade(AffixDef def, ItemGrade grade)
	{
		if (def == null) return 0;
		float mult = _gradeMultiplier[(int)grade];
		int rolled = (int)System.Math.Round(def.BaseMagnitude * mult);
		return System.Math.Max(1, rolled);
	}

	// Создание AppliedAffix из определения с учётом грейда. Удобно для генератора
	// и для unit-style инициализации стартовых предметов.
	public static AppliedAffix Apply(string id, ItemGrade grade)
	{
		var def = Get(id);
		if (def == null) return null;
		return new AppliedAffix(def.Id, def.Slot, def.Kind, MagnitudeForGrade(def, grade));
	}

	// =====================================================================
	// Описания
	// =====================================================================

	// Короткое описание для строки в карточке предмета. Префикс — "+3 ФизАтк",
	// суффикс — "+5% ФизАтк". Имя аффикса показывается отдельно UI'ем.
	public static string DescribeShort(AppliedAffix a)
	{
		if (a == null) return "";
		string stat = StatName(a.Kind);
		return a.Slot == AffixSlot.Prefix
			? $"+{a.Magnitude} {stat}"
			: $"+{a.Magnitude}% {stat}";
	}

	// Имя аффикса из реестра. Если аффикс был удалён из БД (но остался в
	// сейве) — fallback на Id.
	public static string DisplayName(AppliedAffix a)
	{
		var def = Get(a?.Id);
		return def?.Name ?? a?.Id ?? "—";
	}

	public static string StatName(AffixStatKind k) => k switch
	{
		AffixStatKind.PhysAtk => "ФизАтк",
		AffixStatKind.MagAtk  => "МагАтк",
		AffixStatKind.PhysDef => "ФизЗащ",
		AffixStatKind.MagDef  => "МагЗащ",
		AffixStatKind.Hp      => "ХП",
		AffixStatKind.Mp      => "МП",
		AffixStatKind.HpRegen => "РегХП",
		AffixStatKind.MpRegen => "РегМП",
		_                     => "?",
	};
}
