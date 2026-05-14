using GuildOfGreed.Shared.Data;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Commands;

// Команда крафта — partial-сплит CharacterCommands. Базовый уровень
// крафт-системы (только E/D предметы; см. .claude_design_crafting.md).
//
// Сервер вызывает Craft(ch, itemId, rng) с серверным RandomSource. Клиент
// никогда не катит rarity локально — replace state после ответа.
public static partial class CharacterCommands
{
	// Скрафтить предмет по рецепту. Шаги:
	//   1. Резолвим Recipe по itemId через CraftingDB.Resolve.
	//   2. Проверяем уровень скилла >= MinLevelForGrade(grade).
	//   3. Проверяем наличие всех ресурсов в инвентаре игрока.
	//   4. Проверяем свободный слот под результат (instance).
	//   5. Списываем ресурсы; ролим предмет через ItemGenerator с rarity-кэпом
	//      от уровня скилла. Имя крафтера = ch.CharacterName.
	//   6. Начисляем XP, не превышая cap для грейда (LevelCapForGrade).
	public static Result Craft(CharacterData ch, string itemId, RandomSource rng)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		var recipe = CraftingDB.Resolve(itemId);
		if (recipe == null) return Result.Fail(CharacterCommandError.NoRecipe);

		int skillXp = ch.Crafting?.GetXp(recipe.SkillId) ?? 0;
		int skillLvl = CraftingDB.LevelFromXp(skillXp);
		if (skillLvl < CraftingDB.MinLevelForGrade(recipe.Grade))
			return Result.Fail(CharacterCommandError.LowSkill);

		foreach (var ing in recipe.Ingredients)
			if (ch.Inventory.CountOf(ing.ResourceId) < ing.Count)
				return Result.Fail(CharacterCommandError.NoResources);

		if (ch.Inventory.IsFull) return Result.Fail(CharacterCommandError.NoSpace);

		// Расход ресурсов.
		foreach (var ing in recipe.Ingredients)
			ch.Inventory.Remove(ing.ResourceId, ing.Count);

		// Ролл результата: random rarity по весам, но clamp к cap'у скилла.
		var grade = ItemGrades.Parse(recipe.Grade);
		var rolled = ItemGenerator.RollRarity(grade, rng);
		var cap = CraftingDB.MaxRarityForSkillLevel(skillLvl);
		if ((int)rolled > (int)cap) rolled = cap;

		if (ItemsDB.GetWeapon(itemId) != null)
		{
			var w = ItemGenerator.RollWeapon(itemId, rng, rolled);
			if (w == null) return Result.Fail(CharacterCommandError.NoRecipe);
			w.CrafterName = ch.CharacterName;
			ch.Inventory.TryAddInstance(w);
		}
		else
		{
			var a = ItemGenerator.RollArmor(itemId, rng, rolled);
			if (a == null) return Result.Fail(CharacterCommandError.NoRecipe);
			a.CrafterName = ch.CharacterName;
			ch.Inventory.TryAddInstance(a);
		}

		// XP с грейд-капом: не пускаем XP выше уровня LevelCapForGrade.
		int gain = CraftingDB.CraftXp(recipe.Grade, recipe.Tier);
		int xpCap = CraftingDB.XpForLevel(CraftingDB.LevelCapForGrade(recipe.Grade));
		int newXp = skillXp + gain;
		if (newXp > xpCap) newXp = xpCap;
		if (ch.Crafting == null) ch.Crafting = new CraftingSkills();
		ch.Crafting.SetXp(recipe.SkillId, newXp);

		return Result.Success(gain);
	}
}
