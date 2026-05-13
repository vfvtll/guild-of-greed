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

		// Резолвим объект из слота. Один из трёх: WeaponData, ArmorData,
		// ShieldData. Для зелий — фейлим (надеть нельзя).
		WeaponData asWeapon = st.WeaponInstance ?? ItemsDB.GetWeapon(st.ItemId)?.Clone();
		ArmorData  asArmor  = st.ArmorInstance  ?? ItemsDB.GetArmor(st.ItemId)?.Clone();
		ShieldData asShield = st.ShieldInstance ?? ShieldsDB.Get(st.ItemId)?.Clone();
		if (asWeapon == null && asArmor == null && asShield == null) return false;

		if (asWeapon != null) return EquipWeapon(asWeapon, slotIndex);
		if (asShield != null) return EquipShield(asShield, slotIndex);

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

	// Логика надевания оружия с учётом 1H/2H/dual-wield (И6.4):
	//   2H weapon: занимает Weapon, выкидывает Offhand и Shield обратно в инвентарь.
	//   1H weapon: если Weapon пуст или это 2H — заменить Weapon.
	//              Если Weapon — 1H и Offhand пуст и Shield пуст — пойдёт в Offhand.
	//              Иначе свапает с Weapon (старое уходит в инвентарь).
	private bool EquipWeapon(WeaponData w, int slotIndex)
	{
		TakeOneFromSlot(slotIndex);

		if (w.IsTwoHanded)
		{
			// Двуручное занимает все руки.
			if (Character.Weapon != null)  Character.Inventory.TryAddInstance(Character.Weapon);
			if (Character.Offhand != null) Character.Inventory.TryAddInstance(Character.Offhand);
			if (Character.Shield != null)  Character.Inventory.TryAddInstance(Character.Shield);
			Character.Weapon = w;
			Character.Offhand = null;
			Character.Shield = null;
			return true;
		}

		// Одноручное:
		bool mainSlotIs2H = Character.Weapon != null && Character.Weapon.IsTwoHanded;
		if (Character.Weapon == null || mainSlotIs2H)
		{
			// Освобождаем main: если там было 2H — оно уходит в инвентарь.
			if (Character.Weapon != null) Character.Inventory.TryAddInstance(Character.Weapon);
			Character.Weapon = w;
			return true;
		}

		// Main занят одноручным. Off-hand?
		if (Character.Offhand == null && Character.Shield == null)
		{
			Character.Offhand = w;
			return true;
		}

		// Оба слота заняты — свапаем с main (Offhand/Shield остаются).
		Character.Inventory.TryAddInstance(Character.Weapon);
		Character.Weapon = w;
		return true;
	}

	// Щит в off-hand. Если основное оружие 2H — оно выкидывается (нужна
	// свободная рука). Если в off-hand уже что-то — свап в инвентарь.
	private bool EquipShield(ShieldData s, int slotIndex)
	{
		TakeOneFromSlot(slotIndex);

		if (Character.Weapon != null && Character.Weapon.IsTwoHanded)
		{
			Character.Inventory.TryAddInstance(Character.Weapon);
			Character.Weapon = null;
		}
		if (Character.Offhand != null)
		{
			Character.Inventory.TryAddInstance(Character.Offhand);
			Character.Offhand = null;
		}
		if (Character.Shield != null)
			Character.Inventory.TryAddInstance(Character.Shield);
		Character.Shield = s;
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

	public bool UnequipOffhand()
	{
		if (Character == null || Character.Offhand == null) return false;
		if (Character.Inventory.IsFull) return false;
		Character.Inventory.TryAddInstance(Character.Offhand);
		Character.Offhand = null;
		return true;
	}

	public bool UnequipShield()
	{
		if (Character == null || Character.Shield == null) return false;
		if (Character.Inventory.IsFull) return false;
		Character.Inventory.TryAddInstance(Character.Shield);
		Character.Shield = null;
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
		if (s.WeaponInstance != null || s.ArmorInstance != null || s.ShieldInstance != null)
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

	// === Город: магазин / стэш =========================================
	//
	// ВАЖНО: изменения чисто клиентские. На сервер уходят при следующем
	// сохранении персонажа — а это происходит только в HandleBattleAction
	// при завершении боя. Если игрок зайдёт в город, накупит/продаст и
	// вылетит без боя — изменения потеряются. Та же модель, что и для
	// equip/unequip; правильный фикс — отдельный RPC PushCharacter.

	// Купить 1 единицу базового стакаемого предмета (зелья). Возвращает
	// (ok, errorReason). errorReason: "no_money" / "no_space" / "not_for_sale".
	public (bool ok, string reason) BuyOne(string itemId)
	{
		if (Character == null) return (false, "no_character");
		var price = ShopDB.BuyPrice(itemId);
		if (price == null) return (false, "not_for_sale");
		if (Character.Inventory.Money < price.Value) return (false, "no_money");

		int maxStack = PotionsDB.Get(itemId) != null ? 9 : 1;
		// Симулируем добавление: TryAdd либо положит всё, либо вернёт false.
		// Money не списываем до успешного добавления.
		long moneyBefore = Character.Inventory.Money;
		if (!Character.Inventory.TryAdd(itemId, 1, maxStack))
			return (false, "no_space");
		Character.Inventory.Money = moneyBefore - price.Value;
		return (true, null);
	}

	// Продать слот инвентаря целиком. Возвращает реальную сумму, добавленную
	// в кошель (0 если не удалось).
	public long SellSlot(int slotIndex)
	{
		if (Character == null) return 0;
		var slots = Character.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count) return 0;
		var stack = slots[slotIndex];
		long price = ShopDB.SellPriceForStack(stack);
		if (price <= 0) return 0;
		Character.Inventory.RemoveAt(slotIndex);
		Character.Inventory.Money += price;
		return price;
	}

	// Переложить слот целиком из инвентаря в стэш. False — если стэш полон.
	public bool DepositToStash(int invSlotIndex)
	{
		if (Character == null) return false;
		var slots = Character.Inventory.Slots;
		if (invSlotIndex < 0 || invSlotIndex >= slots.Count) return false;
		if (Character.Stash.IsFull) return false;
		var stack = slots[invSlotIndex];
		Character.Inventory.RemoveAt(invSlotIndex);
		Character.Stash.TryAddStack(stack);
		return true;
	}

	// Переложить слот целиком из стэша в инвентарь. False — если инвентарь полон.
	public bool WithdrawFromStash(int stashSlotIndex)
	{
		if (Character == null) return false;
		var slots = Character.Stash.Slots;
		if (stashSlotIndex < 0 || stashSlotIndex >= slots.Count) return false;
		if (Character.Inventory.IsFull) return false;
		var stack = slots[stashSlotIndex];
		Character.Stash.RemoveAt(stashSlotIndex);
		Character.Inventory.TryAddStack(stack);
		return true;
	}
}
