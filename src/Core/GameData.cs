using System.Collections.Generic;
using Godot;

// Глобальный стейт игры (autoload, синглтон).
// В будущем сюда переедут: GlobalStash, экономика, грейд, крафт, аукцион.
public partial class GameData : Node
{
	public static GameData Instance { get; private set; }

	public CharacterData Character { get; private set; }
	public UserSession Session { get; private set; }
	public int SelectedLoadout = 0;
	public int SelectedArmor = 2;
	public int SelectedLocation = 0;

	public class Loadout
	{
		public string Name;
		public string WeaponId;
		public string Deck;     // "warrior" / "mage"
		public string Hint;
	}

	public static readonly List<Loadout> Loadouts = new()
	{
		new Loadout { Name = "Воин (одноручный меч)", WeaponId = "sword_1h_low", Deck = "warrior", Hint = "Меньше урона, но +1 карта в начале хода" },
		new Loadout { Name = "Воин (двуручный меч)", WeaponId = "sword_2h_low", Deck = "warrior", Hint = "Больше физического урона, без бонусной карты" },
		new Loadout { Name = "Маг (посох)",          WeaponId = "staff_low",    Deck = "mage",    Hint = "Высокая магическая атака" },
	};

	public static readonly List<string> ArmorsList = new()
	{
		"robe_power_low",
		"robe_wisdom_low",
		"light_strength_low",
		"light_vigor_low",
	};

	// === Локации ===
	public static readonly string[] LocationNames =
	{
		"Подземелье",
		"Тёмный лес (5 врагов)",
		"Логово босса",
	};

	public static readonly string[] LocationHints =
	{
		"Один обычный гоблин — стандартный бой",
		"Стая из 5 гоблинов — длинный забег, тест на выживаемость",
		"Тёмный рыцарь — высокий риск, высокая награда",
	};

	public override void _Ready()
	{
		Instance = this;
		// Сидируем портативный Rng от Godot-генератора, чтобы Domain/Data не зависели от Godot.
		GD.Randomize();
		Rng.Seed((int)GD.Randi());
		Session = new UserSession();
		// Character назначается извне через SetCharacter() — либо после создания
		// в CharacterCreation, либо после загрузки из SaveGame. Делает это Main.cs.
	}

	// Назначить активного персонажа. Применяет дефолтный лоадаут (оружие/броня).
	public void SetCharacter(CharacterData character)
	{
		Character = character;
		if (character != null) ApplyLoadout();
	}

	public void CycleLoadout()
	{
		SelectedLoadout = (SelectedLoadout + 1) % Loadouts.Count;
		ApplyLoadout();
	}

	public void CycleArmor()
	{
		SelectedArmor = (SelectedArmor + 1) % ArmorsList.Count;
		ApplyLoadout();
	}

	public void CycleLocation()
	{
		SelectedLocation = (SelectedLocation + 1) % LocationNames.Length;
	}

	public void ApplyLoadout()
	{
		var ld = Loadouts[SelectedLoadout];
		Character.Weapon = ItemsDB.GetWeapon(ld.WeaponId)?.Clone();
		var armorId = ArmorsList[SelectedArmor];
		// 50% шанс выпадения брони с суффиксом — для демо.
		Character.Armor = Rng.Chance(0.5f)
			? ItemsDB.RollArmorWithSuffix(armorId)
			: ItemsDB.GetArmor(armorId)?.Clone();
	}

	public List<string> CurrentDeckIds()
	{
		var kind = Loadouts[SelectedLoadout].Deck;
		return new List<string>(kind == "warrior" ? CardsDB.WarriorDeck : CardsDB.MageDeck);
	}

	public string CurrentLoadoutName() => Loadouts[SelectedLoadout].Name;
	public string CurrentLoadoutHint() => Loadouts[SelectedLoadout].Hint;
	public string CurrentLocationName() => LocationNames[SelectedLocation];
	public string CurrentLocationHint() => LocationHints[SelectedLocation];

	// Спавн врагов для текущей локации.
	public List<EnemyData> SpawnEnemies()
	{
		var list = new List<EnemyData>();
		switch (SelectedLocation)
		{
			case 0:
				list.Add(EnemyData.CreateGoblin());
				break;
			case 1:
				for (int i = 0; i < 5; i++) list.Add(EnemyData.CreateForestGoblin());
				break;
			case 2:
				list.Add(EnemyData.CreateBoss());
				break;
			default:
				list.Add(EnemyData.CreateGoblin());
				break;
		}
		return list;
	}
}
