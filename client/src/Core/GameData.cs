using System.Collections.Generic;
using Godot;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Глобальный стейт игры (autoload, синглтон).
// В будущем сюда переедут: GlobalStash, экономика, грейд, крафт, аукцион.
public partial class GameData : Node
{
	public static GameData Instance { get; private set; }

	public CharacterData Character { get; private set; }
	public UserSession Session { get; private set; }
	public int SelectedLoadout = 0;
	public int SelectedChest = 2;
	public int SelectedLocation = 0;

	// Карта текущего забега. null = игрок не в подземелье (на экране выбора локации
	// или в главном меню). Создаётся StartRun, обнуляется EndRun.
	public RunMap CurrentRun { get; private set; }

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

	// Список нагрудников: индекс SelectedChest определяет дефолтный
	// нагрудник нового персонажа в EnsureDefaults.
	public static readonly List<string> ChestList = new()
	{
		"robe_chest_power_low",
		"robe_chest_wisdom_low",
		"light_chest_strength_low",
		"light_chest_vigor_low",
	};

	// Дефолтные шлем/перчатки/сапоги для нового персонажа (потом игрок может менять
	// через инвентарь, на прототипе — фикс).
	private const string DefaultHelmetId = "light_helmet_low";
	private const string DefaultGlovesId = "light_gloves_low";
	private const string DefaultBootsId  = "light_boots_low";

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
		GD.Randomize();
		Rng.Seed((int)GD.Randi());
		Session = new UserSession();
	}

	// Назначить активного персонажа: проставить дефолтную экипировку для нового
	// персонажа и зарезолвить ID-ы в реальные WeaponData/ArmorData.
	public void SetCharacter(CharacterData character)
	{
		Character = character;
		if (character == null) return;
		EnsureDefaults(character);
		ResolveEquipment();
	}

	// Если персонаж только что создан (или сейв старый, без полей экипировки),
	// проставляем стартовые айтемы и зелья.
	private void EnsureDefaults(CharacterData ch)
	{
		if (string.IsNullOrEmpty(ch.EquippedWeaponId))
			ch.EquippedWeaponId = Loadouts[SelectedLoadout].WeaponId;
		if (string.IsNullOrEmpty(ch.EquippedChestId))
			ch.EquippedChestId = ChestList[SelectedChest];
		if (string.IsNullOrEmpty(ch.EquippedHelmetId))
			ch.EquippedHelmetId = DefaultHelmetId;
		if (string.IsNullOrEmpty(ch.EquippedGlovesId))
			ch.EquippedGlovesId = DefaultGlovesId;
		if (string.IsNullOrEmpty(ch.EquippedBootsId))
			ch.EquippedBootsId = DefaultBootsId;

		// Стартовый набор для нового перса с пустым инвентарём.
		if (ch.Inventory != null && ch.Inventory.Slots.Count == 0)
		{
			AddItem(ch, "potion_hp_small", 3);
			AddItem(ch, "potion_mp_small", 2);
			// Бижутерия — не надета, чтобы игрок сам испытал слоты
			AddItem(ch, "amulet_might_low",  1);
			AddItem(ch, "ring_power_low",    1);
			AddItem(ch, "ring_focus_low",    1);
		}
	}

	// Обёртка над Inventory.TryAdd — определяет maxStack по типу предмета.
	// Вызывается из всех мест где надо положить лут в инвентарь.
	public bool AddItem(string itemId, int count = 1)
	{
		if (Character == null) return false;
		return AddItem(Character, itemId, count);
	}

	private static bool AddItem(CharacterData ch, string itemId, int count)
	{
		int maxStack = PotionsDB.Get(itemId) != null ? 9 : 1;
		return ch.Inventory.TryAdd(itemId, count, maxStack);
	}

	// Резолв: ID → реальный объект. Вызывается после SetCharacter и после
	// смены экипировки (equip из инвентаря).
	private void ResolveEquipment()
	{
		if (Character == null) return;
		Character.Weapon = ItemsDB.GetWeapon(Character.EquippedWeaponId)?.Clone();
		Character.Chest  = ItemsDB.GetArmor(Character.EquippedChestId)?.Clone();
		Character.Helmet = ItemsDB.GetArmor(Character.EquippedHelmetId)?.Clone();
		Character.Gloves = ItemsDB.GetArmor(Character.EquippedGlovesId)?.Clone();
		Character.Boots  = ItemsDB.GetArmor(Character.EquippedBootsId)?.Clone();
		Character.Amulet = ItemsDB.GetArmor(Character.EquippedAmuletId)?.Clone();
		Character.Ring1  = ItemsDB.GetArmor(Character.EquippedRing1Id)?.Clone();
		Character.Ring2  = ItemsDB.GetArmor(Character.EquippedRing2Id)?.Clone();
	}

	// === Run lifecycle (карта подземелья) ===
	// StartRun вызывается из LocationSelectView при входе игрока в локацию.
	// Карта эфемерная — не сохраняется между сессиями.
	public void StartRun(int locationIndex)
	{
		if (locationIndex < 0 || locationIndex >= LocationNames.Length) locationIndex = 0;
		SelectedLocation = locationIndex;
		CurrentRun = MapGenerator.Generate(locationIndex);
	}

	public void EndRun() => CurrentRun = null;

	public void AdvanceTo(int nodeId)
	{
		if (CurrentRun == null) return;
		if (!CurrentRun.CanAdvanceTo(nodeId)) return;
		CurrentRun.Advance(nodeId);
	}

	// === Equip / unequip из инвентаря ===
	// Берём предмет из инвентаря и надеваем. Если в слоте уже что-то есть,
	// старое уходит в инвентарь (свап). Возвращает true при успехе.
	public bool EquipFromInventory(string itemId)
	{
		if (Character == null) return false;
		if (!Character.Inventory.Has(itemId)) return false;

		var weapon = ItemsDB.GetWeapon(itemId);
		if (weapon != null)
		{
			Character.Inventory.Remove(itemId, 1);
			if (!string.IsNullOrEmpty(Character.EquippedWeaponId))
				AddItem(Character, Character.EquippedWeaponId, 1);
			Character.EquippedWeaponId = itemId;
			ResolveEquipment();
			return true;
		}

		var armor = ItemsDB.GetArmor(itemId);
		if (armor != null)
		{
			ArmorSlot target = armor.Slot;
			// Кольца: если Slot=Ring1 а Ring1 занят и Ring2 пуст — кладём в Ring2.
			if (target == ArmorSlot.Ring1
				&& !string.IsNullOrEmpty(Character.EquippedRing1Id)
				&& string.IsNullOrEmpty(Character.EquippedRing2Id))
			{
				target = ArmorSlot.Ring2;
			}
			Character.Inventory.Remove(itemId, 1);
			string oldId = GetEquippedArmorId(target);
			if (!string.IsNullOrEmpty(oldId)) AddItem(Character, oldId, 1);
			SetEquippedArmorId(target, itemId);
			ResolveEquipment();
			return true;
		}

		return false;
	}

	// Снять оружие в инвентарь. Не пройдёт если инвентарь полный.
	public bool UnequipWeapon()
	{
		if (Character == null) return false;
		if (string.IsNullOrEmpty(Character.EquippedWeaponId)) return false;
		if (Character.Inventory.IsFull) return false;
		AddItem(Character, Character.EquippedWeaponId, 1);
		Character.EquippedWeaponId = "";
		ResolveEquipment();
		return true;
	}

	// Снять кусок брони в инвентарь.
	public bool UnequipSlot(ArmorSlot slot)
	{
		if (Character == null) return false;
		string id = GetEquippedArmorId(slot);
		if (string.IsNullOrEmpty(id)) return false;
		if (Character.Inventory.IsFull) return false;
		AddItem(Character, id, 1);
		SetEquippedArmorId(slot, "");
		ResolveEquipment();
		return true;
	}

	private string GetEquippedArmorId(ArmorSlot slot) => slot switch
	{
		ArmorSlot.Chest  => Character.EquippedChestId,
		ArmorSlot.Helmet => Character.EquippedHelmetId,
		ArmorSlot.Gloves => Character.EquippedGlovesId,
		ArmorSlot.Boots  => Character.EquippedBootsId,
		ArmorSlot.Amulet => Character.EquippedAmuletId,
		ArmorSlot.Ring1  => Character.EquippedRing1Id,
		ArmorSlot.Ring2  => Character.EquippedRing2Id,
		_                => "",
	};

	private void SetEquippedArmorId(ArmorSlot slot, string id)
	{
		switch (slot)
		{
			case ArmorSlot.Chest:  Character.EquippedChestId  = id; break;
			case ArmorSlot.Helmet: Character.EquippedHelmetId = id; break;
			case ArmorSlot.Gloves: Character.EquippedGlovesId = id; break;
			case ArmorSlot.Boots:  Character.EquippedBootsId  = id; break;
			case ArmorSlot.Amulet: Character.EquippedAmuletId = id; break;
			case ArmorSlot.Ring1:  Character.EquippedRing1Id  = id; break;
			case ArmorSlot.Ring2:  Character.EquippedRing2Id  = id; break;
		}
	}

	// === Использование зелья ===
	// Возвращает true если зелье было выпито.
	// Применяет HpRestore / MpRestore мгновенно и навешивает Buff* как
	// StatusEffect на длительность BuffDuration.
	public bool UsePotion(string itemId)
	{
		if (Character == null) return false;
		if (!Character.Inventory.Has(itemId)) return false;
		var potion = PotionsDB.Get(itemId);
		if (potion == null) return false;

		Character.Inventory.Remove(itemId, 1);
		if (potion.HpRestore > 0)
		{
			Character.CurrentHp = System.Math.Min(
				Character.MaxHp(), Character.CurrentHp + potion.HpRestore);
		}
		if (potion.MpRestore > 0)
		{
			Character.CurrentMp = System.Math.Min(
				Character.MaxMp(), Character.CurrentMp + potion.MpRestore);
		}
		if (!string.IsNullOrEmpty(potion.BuffType) && potion.BuffDuration > 0)
		{
			Character.AddEffect(potion.Id, potion.BuffType, potion.BuffAmount, potion.BuffDuration);
		}
		return true;
	}

	public List<string> CurrentDeckIds()
	{
		var kind = Loadouts[SelectedLoadout].Deck;
		return new List<string>(kind == "warrior" ? CardsDB.WarriorDeck : CardsDB.MageDeck);
	}

	public string CurrentLocationName() => LocationNames[SelectedLocation];

	// Спавн врагов для текущего узла карты. Если CurrentRun == null
	// (на прототипе — старый запуск без карты), фоллбек к одному гоблину.
	public List<EnemyData> SpawnForCurrentNode()
	{
		var list = new List<EnemyData>();
		var node = CurrentRun?.CurrentNode();
		if (node == null)
		{
			list.Add(EnemyData.CreateGoblin());
			return list;
		}

		if (node.Type == MapNodeType.Boss)
		{
			list.Add(EnemyData.CreateBoss());
			return list;
		}

		// Battle (и на этом инкременте — Elite, пока без отдельных моделей):
		// набор врагов зависит от локации. Не пять goblin'ов разом — это был
		// формат "вся локация = один бой". Теперь локация = много узлов.
		switch (SelectedLocation)
		{
			case 0:
				list.Add(EnemyData.CreateGoblin());
				break;
			case 1:
				list.Add(EnemyData.CreateForestGoblin());
				list.Add(EnemyData.CreateForestGoblin());
				break;
			case 2:
				list.Add(EnemyData.CreateGoblin());
				break;
			default:
				list.Add(EnemyData.CreateGoblin());
				break;
		}
		return list;
	}
}
