using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Прокачка скиллов крафта на персонажа. Ключ — string skillId
// ("craft_sword_1h", "craft_light", "craft_robe", ...).
// Значение — суммарный накопленный XP по этому скиллу. Уровень
// вычисляется детерминированно (см. CraftingDB.LevelFromXp).
//
// Сохраняется в JSON. Старые сейвы без поля → пустой словарь (default).
public class CraftingSkills
{
	public Dictionary<string, int> Xp = new();

	public int GetXp(string skillId)
	{
		if (string.IsNullOrEmpty(skillId) || Xp == null) return 0;
		return Xp.TryGetValue(skillId, out int v) ? v : 0;
	}

	public void AddXp(string skillId, int amount)
	{
		if (string.IsNullOrEmpty(skillId) || amount <= 0) return;
		if (Xp == null) Xp = new Dictionary<string, int>();
		Xp.TryGetValue(skillId, out int cur);
		Xp[skillId] = cur + amount;
	}

	public void SetXp(string skillId, int value)
	{
		if (string.IsNullOrEmpty(skillId)) return;
		if (Xp == null) Xp = new Dictionary<string, int>();
		Xp[skillId] = value < 0 ? 0 : value;
	}
}
