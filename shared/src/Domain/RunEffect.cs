namespace GuildOfGreed.Shared.Domain;

// Эффект подземелья ("affliction" / "blessing"). Применяется ко всему бою —
// и к игроку, и к врагам. Накапливается за время забега выбором после боя.
// При выходе из подземелья сбрасывается вместе с RunMap.
//
// Kind — что именно делает эффект. Сейчас три вида:
//   "bleed_all_per_turn"  — в начале каждого хода игрока всем (включая игрока)
//                            добавляется Magnitude кровотечения. Тикает по
//                            существующему механизму BleedStack.
//   "all_dmg_pct"         — Magnitude добавляется в % ко всему наносимому
//                            урону (и игрока, и врагов). Может быть < 0.
//   "weapon_dmg_pct"      — Magnitude добавляется в % к урону, наносимому
//                            конкретным типом оружия (Param = WeaponType:
//                            "sword_1h" / "sword_2h" / "knife" / "staff").
//                            Применяется и к игроку (если у него такое
//                            оружие), и к врагам с intent.WeaponType == Param.
//
// POCO. Все поля сериализуемы. Для UI используется Name/Description.
public class RunEffect
{
	public string Id;
	public string Name;
	public string Description;
	public string Kind;
	public int Magnitude;
	public string Param;     // null если неприменимо (например, для all_dmg_pct)
}
