namespace GuildOfGreed.Shared.Domain;

// Артефакт забега. В отличие от RunEffect, действует только на игрока и
// всегда положителен (RunEffect двусторонний — бьёт по обоим). Выпадает с
// комнат подземелья (выбор после каждого боя), копится в RunMap.ActiveArtifacts.
// Сбрасывается при EndRun.
//
// Требование (Requires) фильтрует пул при ролле: артефакт показывается игроку
// только если он подходит к его экипировке. Формат:
//   ""              — общий, без требований
//   "weapon:<type>" — оружие данного типа (Type из WeaponData: sword_1h /
//                     sword_2h / knife / staff)
//   "armor:<type>"  — броня данного типа надета в Chest-слот ("robe" / "light")
//
// Применение в CombatEngine: см. ApplyArtifactPlayerDmgPct, ApplyArtifactDef,
// ApplyArtifactLifesteal, ApplyArtifactThorns, BeginPlayerTurn. Магнитуда
// читается через ArtifactMagnitudeSum/ArtifactMagnitudeSumForWeapon.
public class Artifact
{
	public string Id;
	public string Name;
	public string Description;
	public string Kind;
	public int Magnitude;
	public int Magnitude2;
	public string Requires;   // см. формат выше; пусто = общий

	// Известные Kind'ы. Хранить как const'ы — typo в DB поймает grep/IDE.
	// Все эффекты применяются ТОЛЬКО к игроку.
	public const string KindPhysDmgPct       = "phys_dmg_pct";        // +M% физ.урона игрока
	public const string KindMagDmgPct        = "mag_dmg_pct";         // +M% маг.урона игрока
	public const string KindWeaponDmgPct     = "weapon_dmg_pct";      // +M% урона при оружии Requires
	public const string KindPhysDefFlat      = "phys_def_flat";       // +M к PhysDef
	public const string KindMagDefFlat       = "mag_def_flat";        // +M к MagDef
	public const string KindLifestealPct     = "lifesteal_pct";       // лечит M% от нанесённого физ.урона
	public const string KindThornsFlat       = "thorns_flat";         // +M возвратного урона при получении физ.удара
	public const string KindExtraDraw        = "extra_draw";          // +M к HandSize
	public const string KindMpRegenFlat      = "mp_regen_flat";       // +M к MP-regen на ход
	public const string KindHpRegenFlat      = "hp_regen_flat";       // +M к HP-regen на ход
	public const string KindCritEveryNMinus  = "crit_every_n_minus";  // снижает EffectiveCritEveryN на M
	public const string KindBlockStartTurn   = "block_start_turn";    // +M блока в начале каждого хода игрока
	public const string KindBleedAmpPct      = "bleed_amp_pct";       // +M% к ущербу от bleed (тик)
}
