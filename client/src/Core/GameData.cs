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

	// Назначить активного персонажа. И6.2: экипировка теперь хранится как
	// инстансы прямо в CharacterData — резолва не требуется.
	public void SetCharacter(CharacterData character)
	{
		Character = character;
		if (character == null) return;
		EnsureDefaults(character);
	}

	// Бэкфилл для legacy-сейвов (до И6.1): если IsNewCharacter=false и
	// экипировка пустая — проставляем дефолтный лоадаут и стартовый набор.
	// Новый персонаж (IsNewCharacter=true) приходит голый, получает экипу
	// со стартового боя.
	private void EnsureDefaults(CharacterData ch)
	{
		if (ch.IsNewCharacter) return;

		if (ch.Weapon == null) ch.Weapon = ItemsDB.GetWeapon(Loadouts[SelectedLoadout].WeaponId)?.Clone();
		if (ch.Chest  == null) ch.Chest  = ItemsDB.GetArmor(ChestList[SelectedChest])?.Clone();
		if (ch.Helmet == null) ch.Helmet = ItemsDB.GetArmor(DefaultHelmetId)?.Clone();
		if (ch.Gloves == null) ch.Gloves = ItemsDB.GetArmor(DefaultGlovesId)?.Clone();
		if (ch.Boots  == null) ch.Boots  = ItemsDB.GetArmor(DefaultBootsId)?.Clone();

		// Стартовый набор для legacy-перса с пустым инвентарём.
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
	// Используется только для стакаемых предметов (зелья) или базовых
	// (без аффиксов) Id-предметов. Для аффиксированных — Inventory.TryAddInstance.
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

	// === Run lifecycle (карта подземелья) ===
	// StartRun вызывается из LocationSelectView при входе игрока в локацию.
	// Карта эфемерная — не сохраняется между сессиями.
	//
	// Здесь делаем полный restore HP/MP. Между узлами одного забега HP/MP
	// переносятся (carry over), но вход в новое подземелье = "вы отдохнули
	// в хабе перед заходом".
	public void StartRun(int locationIndex)
	{
		if (locationIndex < 0 || locationIndex >= LocationNames.Length) locationIndex = 0;
		SelectedLocation = locationIndex;
		Character?.ResetForCombat();
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
	//
	// API работает по индексу слота инвентаря, потому что аффиксированные
	// предметы — instance'ы (Inventory.Slots[i].WeaponInstance/ArmorInstance):
	// два меча с разными аффиксами хранятся в разных слотах, по Id их
	// различить нельзя. Стакаемые baseId-предметы тоже работают по индексу —
	// при equip забирается одна штука из стека.
	public bool EquipFromInventory(int slotIndex)
	{
		if (Character == null) return false;
		var slots = Character.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count) return false;
		var st = slots[slotIndex];

		// Резолвим какой именно объект надеваем: instance-payload или свежий
		// клон из ItemsDB по baseId. Для зелий — фейлим (надеть нельзя).
		WeaponData asWeapon = st.WeaponInstance ?? ItemsDB.GetWeapon(st.ItemId)?.Clone();
		ArmorData  asArmor  = st.ArmorInstance  ?? ItemsDB.GetArmor(st.ItemId)?.Clone();
		if (asWeapon == null && asArmor == null) return false;

		if (asWeapon != null)
		{
			// Снимаем 1 шт из слота. Для instance — слот удаляется целиком,
			// для baseId-стека — декрементируется Count.
			TakeOneFromSlot(slotIndex);
			// Старое оружие — обратно в инвентарь как instance.
			if (Character.Weapon != null)
				Character.Inventory.TryAddInstance(Character.Weapon);
			Character.Weapon = asWeapon;
			return true;
		}

		// Броня.
		ArmorSlot target = asArmor.Slot;
		// Кольца: если в Ring1 уже занято, а Ring2 пуст — надеваем в Ring2.
		if (target == ArmorSlot.Ring1 && Character.Ring1 != null && Character.Ring2 == null)
			target = ArmorSlot.Ring2;

		TakeOneFromSlot(slotIndex);
		var old = Character.GetArmorSlot(target);
		if (old != null) Character.Inventory.TryAddInstance(old);
		Character.SetArmorSlot(target, asArmor);
		return true;
	}

	// Снять оружие в инвентарь. False если инвентарь полон или оружия нет.
	public bool UnequipWeapon()
	{
		if (Character == null || Character.Weapon == null) return false;
		if (Character.Inventory.IsFull) return false;
		Character.Inventory.TryAddInstance(Character.Weapon);
		Character.Weapon = null;
		return true;
	}

	// Снять кусок брони в инвентарь.
	public bool UnequipSlot(ArmorSlot slot)
	{
		if (Character == null) return false;
		var item = Character.GetArmorSlot(slot);
		if (item == null) return false;
		if (Character.Inventory.IsFull) return false;
		Character.Inventory.TryAddInstance(item);
		Character.SetArmorSlot(slot, null);
		return true;
	}

	// Удалить 1 единицу из слота инвентаря. Для instance — удалить слот
	// целиком; для стака — декремент.
	private void TakeOneFromSlot(int slotIndex)
	{
		var slots = Character.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count) return;
		var s = slots[slotIndex];
		if (s.WeaponInstance != null || s.ArmorInstance != null)
		{
			Character.Inventory.RemoveAt(slotIndex);
			return;
		}
		s.Count--;
		if (s.Count <= 0) Character.Inventory.RemoveAt(slotIndex);
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

	// Колода + спавн делегированы в shared (см. CardsDB.DeckFor / EnemyData.SpawnFor).
	// Server использует те же helpers — обе стороны заведомо согласованы.
	public List<string> CurrentDeckIds() => CardsDB.DeckFor(Character);

	public string CurrentLocationName() => LocationNames[SelectedLocation];
}
