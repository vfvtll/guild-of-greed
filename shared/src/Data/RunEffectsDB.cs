using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Реестр эффектов подземелья — пул, из которого после каждого боя клиенту
// предлагается 3 случайных варианта на выбор. Магнитуды подобраны так, чтобы
// каждый эффект был ощутимо двусторонним (помогает либо вредит в зависимости
// от билда и встречи).
public static class RunEffectsDB
{
	public static readonly Dictionary<string, RunEffect> All = new()
	{
		// === Кровотечение всем ===========================================
		["bleed_field_5"] = new()
		{
			Id = "bleed_field_5",
			Name = "Кровавая пыльца",
			Description = "В начале каждого хода всем (и игроку, и врагам) добавляется +5 кровотечения.",
			Kind = "bleed_all_per_turn", Magnitude = 5,
		},
		["bleed_field_10"] = new()
		{
			Id = "bleed_field_10",
			Name = "Густой туман гнили",
			Description = "В начале каждого хода всем добавляется +10 кровотечения.",
			Kind = "bleed_all_per_turn", Magnitude = 10,
		},

		// === Общий мультипликатор урона ==================================
		["all_dmg_plus_50"] = new()
		{
			Id = "all_dmg_plus_50",
			Name = "Ярость крови",
			Description = "+50% урона всем — и игроку, и врагам.",
			Kind = "all_dmg_pct", Magnitude = 50,
		},
		["all_dmg_plus_100"] = new()
		{
			Id = "all_dmg_plus_100",
			Name = "Алчная мгла",
			Description = "+100% урона всем — и игроку, и врагам.",
			Kind = "all_dmg_pct", Magnitude = 100,
		},
		["all_dmg_minus_30"] = new()
		{
			Id = "all_dmg_minus_30",
			Name = "Туман усталости",
			Description = "−30% урона всем. Дольше, но безопаснее.",
			Kind = "all_dmg_pct", Magnitude = -30,
		},

		// === Бонус по типу оружия ========================================
		// Каждый из этих эффектов одинаково применяется и к игроку (если он
		// носит соответствующее оружие), и к врагам с intent.WeaponType == Param.
		["weapon_knife_200"] = new()
		{
			Id = "weapon_knife_200",
			Name = "Шёпот кинжалов",
			Description = "+200% урона от кинжалов (у всех, у кого они есть).",
			Kind = "weapon_dmg_pct", Magnitude = 200, Param = "knife",
		},
		["weapon_sword_1h_150"] = new()
		{
			Id = "weapon_sword_1h_150",
			Name = "Сталь рук",
			Description = "+150% урона от одноручных мечей.",
			Kind = "weapon_dmg_pct", Magnitude = 150, Param = "sword_1h",
		},
		["weapon_sword_2h_150"] = new()
		{
			Id = "weapon_sword_2h_150",
			Name = "Расколотый камень",
			Description = "+150% урона от двуручных мечей.",
			Kind = "weapon_dmg_pct", Magnitude = 150, Param = "sword_2h",
		},
		["weapon_staff_150"] = new()
		{
			Id = "weapon_staff_150",
			Name = "Пляска маны",
			Description = "+150% урона от посохов.",
			Kind = "weapon_dmg_pct", Magnitude = 150, Param = "staff",
		},
	};

	public static RunEffect Get(string id)
		=> id != null && All.TryGetValue(id, out var e) ? e : null;

	// 3 случайных эффекта на выбор. Исключаем уже подобранные в забеге
	// (commented for now — пока разрешаем дубли, чтобы игрок мог стакать).
	// Детерминированно от seed (см. callsite — обычно battleSeed после победы).
	public static List<RunEffect> RollChoices(int count, RandomSource rng, IEnumerable<string> exclude = null)
	{
		var pool = new List<RunEffect>(All.Values);
		var picked = new List<RunEffect>();
		while (picked.Count < count && pool.Count > 0)
		{
			int idx = rng.Next(pool.Count);
			var e = pool[idx];
			pool.RemoveAt(idx);
			picked.Add(e);
		}
		return picked;
	}
}
