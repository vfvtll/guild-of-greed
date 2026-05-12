using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Реестр щитов. 3 типа на каждый Grade (для прототипа — только E-low).
//
// Эффекты применяются в CombatEngine:
//   Magic    — ApplyDamageToPlayer: при isPhys=false reflect EffectMagnitude%.
//   Physical — BeginPlayerTurn: добавить block = MaxHp() * EffectMagnitude/100.
//   Balanced — ApplyDamageToPlayer: навесить counter-buff (StatusEffect с
//              kind="phys_dmg_pct"/"magic_dmg_pct"), длительность CounterBuffDuration.
public static class ShieldsDB
{
	public static readonly Dictionary<string, ShieldData> Shields = new()
	{
		["shield_magic_low"] = new()
		{
			Id = "shield_magic_low", Name = "Магический бакрейн",
			Grade = "E", Tier = "low", Type = ShieldType.Magic,
			MagDef = 5,                  // только маг.защита
			EffectMagnitude = 10,        // 10% возврат
		},
		["shield_physical_low"] = new()
		{
			Id = "shield_physical_low", Name = "Кованый щит",
			Grade = "E", Tier = "low", Type = ShieldType.Physical,
			PhysDef = 5,                 // только физ.защита
			EffectMagnitude = 10,        // 10% MaxHp в блоке за начало хода
		},
		["shield_balanced_low"] = new()
		{
			Id = "shield_balanced_low", Name = "Адепт-щит",
			Grade = "E", Tier = "low", Type = ShieldType.Balanced,
			PhysDef = 2, MagDef = 2,     // обе по половине
			EffectMagnitude = 20,        // counter-buff +20%
			CounterBuffDuration = 1,
		},
	};

	public static ShieldData Get(string id)
		=> id != null && Shields.TryGetValue(id, out var s) ? s : null;
}
