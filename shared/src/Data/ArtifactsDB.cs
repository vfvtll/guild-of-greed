using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Реестр артефактов подземелья. После каждого боя из RollChoices(player, ...)
// игроку показывается N штук — пул фильтруется по его экипировке: оружейные
// артефакты не выпадают, если у персонажа нет соответствующего оружия,
// бронные — если на Chest-слот не надет соответствующий тип.
//
// Магнитуды подобраны так, чтобы общие были скромнее, а специализированные —
// заметно сильнее: специализированный артефакт нужно "заслужить" билдом.
public static class ArtifactsDB
{
	public static readonly Dictionary<string, Artifact> All = new()
	{
		// =============== Общие — урон ====================================
		["amulet_warlord"] = new()
		{
			Id = "amulet_warlord",
			Name = "Амулет полководца",
			Description = "+20% физического урона.",
			Kind = Artifact.KindPhysDmgPct, Magnitude = 20,
		},
		["tome_wizardry"] = new()
		{
			Id = "tome_wizardry",
			Name = "Том чародея",
			Description = "+20% магического урона.",
			Kind = Artifact.KindMagDmgPct, Magnitude = 20,
		},
		// =============== Общие — выживаемость ============================
		["iron_skin"] = new()
		{
			Id = "iron_skin",
			Name = "Железная кожа",
			Description = "+5 к физической защите.",
			Kind = Artifact.KindPhysDefFlat, Magnitude = 5,
		},
		["crystal_aegis"] = new()
		{
			Id = "crystal_aegis",
			Name = "Хрустальный эгид",
			Description = "+5 к магической защите.",
			Kind = Artifact.KindMagDefFlat, Magnitude = 5,
		},
		["vampiric_fang"] = new()
		{
			Id = "vampiric_fang",
			Name = "Клык вампира",
			Description = "Лечит 10% от нанесённого физического урона.",
			Kind = Artifact.KindLifestealPct, Magnitude = 10,
		},
		["thorny_carapace"] = new()
		{
			Id = "thorny_carapace",
			Name = "Шипастый панцирь",
			Description = "Возвращает атакующему 5 урона при каждом физическом ударе.",
			Kind = Artifact.KindThornsFlat, Magnitude = 5,
		},
		["aegis_charge"] = new()
		{
			Id = "aegis_charge",
			Name = "Заряд эгиды",
			Description = "В начале каждого хода даёт 10 блока.",
			Kind = Artifact.KindBlockStartTurn, Magnitude = 10,
		},
		// =============== Общие — ресурсы =================================
		["quickdraw_belt"] = new()
		{
			Id = "quickdraw_belt",
			Name = "Пояс быстрой руки",
			Description = "+1 карта в руке.",
			Kind = Artifact.KindExtraDraw, Magnitude = 1,
		},
		["clarity_pendant"] = new()
		{
			Id = "clarity_pendant",
			Name = "Кулон ясности",
			Description = "+5 к восстановлению маны за ход.",
			Kind = Artifact.KindMpRegenFlat, Magnitude = 5,
		},
		["silver_chalice"] = new()
		{
			Id = "silver_chalice",
			Name = "Серебряная чаша",
			Description = "+5 к восстановлению здоровья за ход.",
			Kind = Artifact.KindHpRegenFlat, Magnitude = 5,
		},
		["runic_focus"] = new()
		{
			Id = "runic_focus",
			Name = "Рунический фокус",
			Description = "Криты случаются на 2 удара раньше.",
			Kind = Artifact.KindCritEveryNMinus, Magnitude = 2,
		},

		// =============== Оружейные =======================================
		// Условие — игрок носит соответствующий тип оружия в основной руке.
		["bloodsword_sigil"] = new()
		{
			Id = "bloodsword_sigil",
			Name = "Печать кровавого меча",
			Description = "+50% урона при одноручном мече.",
			Kind = Artifact.KindWeaponDmgPct, Magnitude = 50, Requires = "weapon:sword_1h",
		},
		["executioner_pact"] = new()
		{
			Id = "executioner_pact",
			Name = "Пакт палача",
			Description = "+50% урона при двуручном мече.",
			Kind = Artifact.KindWeaponDmgPct, Magnitude = 50, Requires = "weapon:sword_2h",
		},
		["bleeding_edge"] = new()
		{
			Id = "bleeding_edge",
			Name = "Кровящий край",
			Description = "Кровотечение наносит +50% урона.",
			Kind = Artifact.KindBleedAmpPct, Magnitude = 50, Requires = "weapon:sword_2h",
		},
		["assassin_emblem"] = new()
		{
			Id = "assassin_emblem",
			Name = "Эмблема убийцы",
			Description = "+75% урона при кинжале.",
			Kind = Artifact.KindWeaponDmgPct, Magnitude = 75, Requires = "weapon:knife",
		},
		["shadow_step"] = new()
		{
			Id = "shadow_step",
			Name = "Теневой шаг",
			Description = "Криты случаются на 3 удара раньше (только с кинжалом).",
			Kind = Artifact.KindCritEveryNMinus, Magnitude = 3, Requires = "weapon:knife",
		},
		["arcane_prism"] = new()
		{
			Id = "arcane_prism",
			Name = "Арканная призма",
			Description = "+50% магического урона при посохе.",
			Kind = Artifact.KindMagDmgPct, Magnitude = 50, Requires = "weapon:staff",
		},
		["mana_geyser"] = new()
		{
			Id = "mana_geyser",
			Name = "Гейзер маны",
			Description = "+8 к восстановлению маны (при посохе).",
			Kind = Artifact.KindMpRegenFlat, Magnitude = 8, Requires = "weapon:staff",
		},

		// =============== Бронные =========================================
		// Условие — в Chest-слот надета броня указанного Type.
		["robe_blessing"] = new()
		{
			Id = "robe_blessing",
			Name = "Благословение робы",
			Description = "+30% магического урона (требуется роба).",
			Kind = Artifact.KindMagDmgPct, Magnitude = 30, Requires = "armor:robe",
		},
		["mage_ward"] = new()
		{
			Id = "mage_ward",
			Name = "Ограда мага",
			Description = "+8 к магической защите (требуется роба).",
			Kind = Artifact.KindMagDefFlat, Magnitude = 8, Requires = "armor:robe",
		},
		["leather_pact"] = new()
		{
			Id = "leather_pact",
			Name = "Пакт кожи",
			Description = "+25% физического урона (требуется кожаная броня).",
			Kind = Artifact.KindPhysDmgPct, Magnitude = 25, Requires = "armor:light",
		},
		["hunter_resolve"] = new()
		{
			Id = "hunter_resolve",
			Name = "Стойкость охотника",
			Description = "+8 к физической защите (требуется кожаная броня).",
			Kind = Artifact.KindPhysDefFlat, Magnitude = 8, Requires = "armor:light",
		},
	};

	public static Artifact Get(string id)
		=> id != null && All.TryGetValue(id, out var a) ? a : null;

	// Проверяет, подходит ли артефакт под текущую экипировку игрока. Используется
	// и для фильтрации пула при ролле, и для подавления эффекта в CombatEngine,
	// если игрок снял оружие/броню уже после взятия артефакта.
	public static bool Matches(Artifact artifact, CharacterData player)
	{
		if (artifact == null) return false;
		var req = artifact.Requires;
		if (string.IsNullOrEmpty(req)) return true;
		if (player == null) return false;
		if (req.StartsWith("weapon:"))
		{
			var t = req.Substring(7);
			return player.Weapon != null && player.Weapon.Type == t;
		}
		if (req.StartsWith("armor:"))
		{
			var t = req.Substring(6);
			return player.Chest != null && player.Chest.Type == t;
		}
		return false;
	}

	// Ролл N артефактов под игрока. Из общего пула вычитаются те, у которых
	// Requires не подходит к экипу. Уже взятые в текущем забеге исключаются —
	// дубли артефактов не допускаются (в отличие от RunEffect).
	public static List<Artifact> RollChoices(
		int count, RandomSource rng, CharacterData player, IEnumerable<string> exclude = null)
	{
		var excludeSet = new HashSet<string>();
		if (exclude != null) foreach (var id in exclude) excludeSet.Add(id);

		var pool = new List<Artifact>();
		foreach (var kv in All)
		{
			if (excludeSet.Contains(kv.Key)) continue;
			if (!Matches(kv.Value, player)) continue;
			pool.Add(kv.Value);
		}

		var picked = new List<Artifact>();
		while (picked.Count < count && pool.Count > 0)
		{
			int idx = rng.Next(pool.Count);
			picked.Add(pool[idx]);
			pool.RemoveAt(idx);
		}
		return picked;
	}

	// Сумма магнитуд активных артефактов указанного Kind, отфильтрованных по
	// текущему экипу игрока (чтобы снятая под-условие экипировка отключала
	// артефакт сразу, без обнуления списка).
	public static int MagnitudeSum(IEnumerable<Artifact> artifacts, CharacterData player, string kind)
	{
		if (artifacts == null) return 0;
		int sum = 0;
		foreach (var a in artifacts)
		{
			if (a == null || a.Kind != kind) continue;
			if (!Matches(a, player)) continue;
			sum += a.Magnitude;
		}
		return sum;
	}
}
