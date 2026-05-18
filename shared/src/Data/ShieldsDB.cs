using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Реестр щитов: 3 типа × {E-low, D-low/mid/top} = 12 экземпляров.
//
// Эффекты применяются в CombatEngine:
//   Magic    — ApplyDamageToPlayer: при isPhys=false reflect EffectMagnitude%.
//   Physical — BeginPlayerTurn: добавить block = MaxHp() * EffectMagnitude/100.
//   Balanced — ApplyDamageToPlayer: навесить counter-buff (StatusEffect с
//              kind="phys_dmg_pct"/"magic_dmg_pct"), длительность CounterBuffDuration.
//
// Базы scaling'уются по TierProgression: PhysDef/MagDef → round(base × Mult(grade, tier)).
// EffectMagnitude растёт мягче (см. _effectMagnitude*) чтобы 10% reflect на D-top
// не превращался в 29% — это слишком сильно. Counter-buff остаётся 20% на всех
// рангах: магнитуда увеличивается через PhysDef/MagDef стат.
public static class ShieldsDB
{
	// Базовые значения на E-low — отсюда генерим mid/top/D через TierProgression.
	// Поддерживаем backward-compat: id вида "shield_<type>_<grade>_<tier>",
	// исключение — E-low оставлен под старым id "shield_<type>_low" (он мог
	// успеть попасть в сейвы).
	private static readonly Dictionary<string, ShieldData> _shields = BuildShields();

	public static readonly Dictionary<string, ShieldData> Shields = _shields;

	public static ShieldData Get(string id)
		=> id != null && _shields.TryGetValue(id, out var s) ? s : null;

	private static Dictionary<string, ShieldData> BuildShields()
	{
		var d = new Dictionary<string, ShieldData>();

		// === E-low (legacy id'ы без "_e_low" — оставлены для существующих сейвов) ===
		d["shield_magic_low"] = MakeMagic("shield_magic_low", "Магический бакрейн", "E", "low");
		d["shield_physical_low"] = MakePhysical("shield_physical_low", "Кованый щит", "E", "low");
		d["shield_balanced_low"] = MakeBalanced("shield_balanced_low", "Адепт-щит", "E", "low");

		// === D-grade (low/mid/top × 3 типа) ===
		string[] tiers = { "low", "mid", "top" };
		string[] magicNames = { "Чародейский щит", "Защита аркана", "Зерцало мистика" };
		string[] physNames  = { "Башенный щит", "Бастион", "Кулак крепости" };
		string[] balNames   = { "Щит-страж", "Двойственный щит", "Хранитель равновесия" };
		for (int i = 0; i < tiers.Length; i++)
		{
			string t = tiers[i];
			d[$"shield_magic_d_{t}"]    = MakeMagic($"shield_magic_d_{t}",    magicNames[i], "D", t);
			d[$"shield_physical_d_{t}"] = MakePhysical($"shield_physical_d_{t}", physNames[i],  "D", t);
			d[$"shield_balanced_d_{t}"] = MakeBalanced($"shield_balanced_d_{t}", balNames[i],   "D", t);
		}

		return d;
	}

	private static ShieldData MakeMagic(string id, string name, string grade, string tier)
	{
		float m = TierProgression.Mult(grade, tier);
		return new ShieldData
		{
			Id = id, Name = name, Grade = grade, Tier = tier,
			Type = ShieldType.Magic,
			MagDef = Scale(5, m),
			EffectMagnitude = ScaleEffect(10, grade),
		};
	}

	private static ShieldData MakePhysical(string id, string name, string grade, string tier)
	{
		float m = TierProgression.Mult(grade, tier);
		return new ShieldData
		{
			Id = id, Name = name, Grade = grade, Tier = tier,
			Type = ShieldType.Physical,
			PhysDef = Scale(5, m),
			EffectMagnitude = ScaleEffect(10, grade),
		};
	}

	private static ShieldData MakeBalanced(string id, string name, string grade, string tier)
	{
		float m = TierProgression.Mult(grade, tier);
		return new ShieldData
		{
			Id = id, Name = name, Grade = grade, Tier = tier,
			Type = ShieldType.Balanced,
			PhysDef = Scale(2, m),
			MagDef = Scale(2, m),
			EffectMagnitude = 20,    // counter-buff остаётся 20% — растёт через PhysDef/MagDef
			CounterBuffDuration = 1,
		};
	}

	private static int Scale(int baseValue, float mult)
		=> (int)System.Math.Round(baseValue * mult);

	// EffectMagnitude растёт мягче чем PhysDef/MagDef — на E это 10%, на D 12-13%,
	// иначе reflect-щит на D-top становится 29% (слишком сильно для маг.билдов).
	private static int ScaleEffect(int baseValue, string grade) => grade switch
	{
		"E" => baseValue,
		"D" => baseValue + 2,    // 10 → 12
		"C" => baseValue + 4,
		"B" => baseValue + 6,
		_   => baseValue,
	};
}
