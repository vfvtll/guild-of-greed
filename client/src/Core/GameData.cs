using System.Collections.Generic;
using Godot;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;
using GuildOfGreed.Shared.Net;

// Глобальный стейт игры (autoload, синглтон).
// В будущем сюда переедут: GlobalStash, экономика, грейд, крафт, аукцион.
public partial class GameData : Node
{
	public static GameData Instance { get; private set; }

	public CharacterData Character { get; private set; }
	public UserSession Session { get; private set; }

	// Сетевой клиент — устанавливается из Main после handshake. Используется
	// для пуша Character на сервер после ЛЮБОЙ локальной мутации (equip,
	// buy/sell, стэш, очки статов). Без этого экипа/деньги/инвентарь не
	// доезжают в БД до следующего боя и пропадают при кросс-логине.
	public NetworkClient Net { get; set; }
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
		"Заброшенные катакомбы",
		"Развалины старого замка",
	};

	public static readonly string[] LocationHints =
	{
		"Один обычный гоблин — стандартный бой",
		"Стая из 5 гоблинов — длинный забег, тест на выживаемость",
		"Тёмный рыцарь — высокий риск, высокая награда",
		"Нежить и тёмные культисты. 8 рядов карты, упор на магический урон —\nбез запаса MagDef не выйдешь живым.",
		"Бывшая крепость, кишащая разбойниками и наёмниками. 10 рядов карты —\nсамый длинный забег: бьют тяжело, в конце ждёт капитан гарнизона.",
	};

	// Минимальный уровень персонажа, нужный для входа в локацию.
	// Индексы совпадают с LocationNames; 1 — нет требований. Длинные подземелья
	// "Заброшенные катакомбы" и "Развалины старого замка" заточены под персов
	// 5+ ур.: рядовые враги бьют по 30-50, что у голого левел-1 снесёт HP за пару
	// ходов. UI блокирует "Войти →" пока перс не дорос (LocationSelectView).
	public static readonly int[] LocationRequiredLevel =
	{
		1, 1, 1,
		5, 5,
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
	// Старт забега: сид приходит ОТ СЕРВЕРА (StartRunResponse.RunSeed) —
	// один на всё подземелье, из него генерится карта и выводятся боевые
	// RNG-потоки. Карта и колода детерминированы от seed + character_snapshot.
	public void StartRun(int locationIndex, int seed)
	{
		if (locationIndex < 0 || locationIndex >= LocationNames.Length) locationIndex = 0;
		SelectedLocation = locationIndex;
		Character?.ResetForCombat();   // idempotent — основной reset уже был перед PushCharacter
		CurrentRun = MapGenerator.Generate(locationIndex, seed);
		// Замораживаем колоду на забег. Все бои внутри run используют эту копию.
		// Пересчёт — только при следующем StartRun.
		CurrentRun.LockedDeck = CardsDB.DeckFor(Character);
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

		bool ok;
		if (asWeapon != null) ok = EquipWeapon(asWeapon, slotIndex);
		else if (asShield != null) ok = EquipShield(asShield, slotIndex);
		else
		{
			// Броня.
			ArmorSlot target = asArmor.Slot;
			// Кольца: если в Ring1 уже занято, а Ring2 пуст — надеваем в Ring2.
			if (target == ArmorSlot.Ring1 && Character.Ring1 != null && Character.Ring2 == null)
				target = ArmorSlot.Ring2;

			TakeOneFromSlot(slotIndex);
			var old = Character.GetArmorSlot(target);
			if (old != null) Character.Inventory.TryAddInstance(old);
			Character.SetArmorSlot(target, asArmor);
			ok = true;
		}
		if (ok) PushCharacterToServer();
		return ok;
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
		PushCharacterToServer();
		return true;
	}

	public bool UnequipOffhand()
	{
		if (Character == null || Character.Offhand == null) return false;
		if (Character.Inventory.IsFull) return false;
		Character.Inventory.TryAddInstance(Character.Offhand);
		Character.Offhand = null;
		PushCharacterToServer();
		return true;
	}

	public bool UnequipShield()
	{
		if (Character == null || Character.Shield == null) return false;
		if (Character.Inventory.IsFull) return false;
		Character.Inventory.TryAddInstance(Character.Shield);
		Character.Shield = null;
		PushCharacterToServer();
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
		PushCharacterToServer();
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
		PushCharacterToServer();
		return true;
	}

	// Колода + спавн делегированы в shared (см. CardsDB.DeckFor / EnemyData.SpawnFor).
	// Server использует те же helpers — обе стороны заведомо согласованы.
	public List<string> CurrentDeckIds() => CardsDB.DeckFor(Character);

	public string CurrentLocationName() => LocationNames[SelectedLocation];

	// === Server persistence =============================================
	//
	// Пушит текущий Character на сервер. Зовётся ПОСЛЕ любой локальной
	// мутации (equip/buy/sell/стэш/spend stat). Fire-and-forget: ошибка
	// сети не блокирует UI, сервер просто получит следующий пуш.
	private static readonly System.Text.Json.JsonSerializerOptions PushJsonOpts =
		new() { IncludeFields = true };

	public void PushCharacterToServer()
	{
		if (Net == null || Character == null) return;
		string json;
		try
		{
			json = System.Text.Json.JsonSerializer.Serialize(Character, PushJsonOpts);
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"GameData: serialize character failed: {ex.Message}");
			return;
		}
		_ = PushAsync(json);
	}

	private async System.Threading.Tasks.Task PushAsync(string json)
	{
		try
		{
			var resp = await Net.PushCharacterAsync(json);
			if (!resp.Success)
				GD.PrintErr($"GameData: PushCharacter rejected: {resp.Error}");
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"GameData: PushCharacter network error: {ex.Message}");
		}
	}

	// Синхронная версия пуша: дожидается ответа сервера. Используется в Main
	// перед StartRun/EndRun, чтобы серверный снэпшот опирался на свежий DB-state.
	public async System.Threading.Tasks.Task PushCharacterAndAwaitAsync()
	{
		if (Net == null || Character == null) return;
		string json;
		try { json = System.Text.Json.JsonSerializer.Serialize(Character, PushJsonOpts); }
		catch (System.Exception ex)
		{
			GD.PrintErr($"GameData: serialize character failed: {ex.Message}");
			return;
		}
		await PushAsync(json);
	}

	// === Город: магазин / стэш =========================================
	//
	// ВАЖНО: изменения чисто клиентские. На сервер уходят при следующем
	// сохранении персонажа — а это происходит только в HandleBattleAction
	// при завершении боя. Если игрок зайдёт в город, накупит/продаст и
	// вылетит без боя — изменения потеряются. Та же модель, что и для
	// equip/unequip; правильный фикс — отдельный RPC PushCharacter.

	// Купить 1 единицу предмета из ассортимента лавки. Возвращает
	// (ok, errorReason). errorReason: "no_money" / "no_space" / "not_for_sale".
	//
	// Оружие/броня кладутся как INSTANCE (Clone базы, без аффиксов) — иначе
	// при попытке надеть TryAdd-стак не превратится в WeaponInstance. Зелья и
	// прочие стакаемые — через Inventory.TryAdd.
	public (bool ok, string reason) BuyOne(string itemId)
	{
		if (Character == null) return (false, "no_character");
		var price = ShopDB.BuyPrice(itemId);
		if (price == null) return (false, "not_for_sale");
		if (Character.Inventory.Money < price.Value) return (false, "no_money");

		// Оружие — клон базы как instance.
		var weaponBase = ItemsDB.GetWeapon(itemId);
		if (weaponBase != null)
		{
			if (Character.Inventory.IsFull) return (false, "no_space");
			Character.Inventory.TryAddInstance(weaponBase.Clone());
			Character.Inventory.Money -= price.Value;
			PushCharacterToServer();
			return (true, null);
		}

		// Броня — то же (на будущее: щиты/доспехи в лавке).
		var armorBase = ItemsDB.GetArmor(itemId);
		if (armorBase != null)
		{
			if (Character.Inventory.IsFull) return (false, "no_space");
			Character.Inventory.TryAddInstance(armorBase.Clone());
			Character.Inventory.Money -= price.Value;
			PushCharacterToServer();
			return (true, null);
		}

		// Стакаемые (зелья / прочее).
		int maxStack = PotionsDB.Get(itemId) != null ? 9 : 1;
		long moneyBefore = Character.Inventory.Money;
		if (!Character.Inventory.TryAdd(itemId, 1, maxStack))
			return (false, "no_space");
		Character.Inventory.Money = moneyBefore - price.Value;
		PushCharacterToServer();
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
		PushCharacterToServer();
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
		PushCharacterToServer();
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
		PushCharacterToServer();
		return true;
	}

	// === Кузница =======================================================
	//
	// Все три операции — на slotIndex в инвентаре. instance-only (weapon/armor/shield),
	// зелья игнорируются. Push после успешной мутации.

	// Распылить слот в эссенцию. Возвращает кол-во полученной эссенции, 0 если не вышло.
	public long ForgeDismantle(int slotIndex)
	{
		if (Character == null) return 0;
		var slots = Character.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count) return 0;
		var stack = slots[slotIndex];

		string grade, rank;
		ItemRarity rarity;
		if (stack.WeaponInstance != null)
		{ grade = stack.WeaponInstance.Grade; rank = stack.WeaponInstance.Tier; rarity = stack.WeaponInstance.Rarity; }
		else if (stack.ArmorInstance != null)
		{ grade = stack.ArmorInstance.Grade; rank = stack.ArmorInstance.Tier; rarity = stack.ArmorInstance.Rarity; }
		else if (stack.ShieldInstance != null)
		{ grade = stack.ShieldInstance.Grade; rank = stack.ShieldInstance.Tier; rarity = stack.ShieldInstance.Rarity; }
		else
			return 0;  // стакаемые (зелья, базы) — не распыляются.

		long yield = ForgeDB.DismantleEssence(grade, rank, rarity);
		Character.Inventory.RemoveAt(slotIndex);
		Character.Inventory.Essence += yield;
		PushCharacterToServer();
		return yield;
	}

	// Улучшить rarity на одну ступень. Возвращает (ok, reason).
	// reason: "no_essence" / "cant_upgrade" / "not_upgradable" / "no_item".
	public (bool ok, string reason) ForgeUpgrade(int slotIndex)
	{
		if (Character == null) return (false, "no_item");
		var slots = Character.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count) return (false, "no_item");
		var stack = slots[slotIndex];

		if (stack.WeaponInstance != null)
		{
			var w = stack.WeaponInstance;
			if (!ForgeDB.CanUpgrade(w.Grade, w.Rarity)) return (false, "cant_upgrade");
			long cost = ForgeDB.UpgradeCost(w.Grade, w.Tier, w.Rarity);
			if (Character.Inventory.Essence < cost) return (false, "no_essence");
			var newRarity = ForgeDB.NextRarity(w.Rarity);
			// Перекатываем предмет на новой rarity — ItemGenerator с forceRarity
			// клонирует базу и катает новый набор аффиксов под бюджет newRarity.
			var fresh = ItemGenerator.RollWeapon(w.Id, null, newRarity);
			if (fresh == null) return (false, "not_upgradable");
			stack.WeaponInstance = fresh;
			Character.Inventory.Essence -= cost;
			PushCharacterToServer();
			return (true, null);
		}
		if (stack.ArmorInstance != null)
		{
			var a = stack.ArmorInstance;
			if (!ForgeDB.CanUpgrade(a.Grade, a.Rarity)) return (false, "cant_upgrade");
			long cost = ForgeDB.UpgradeCost(a.Grade, a.Tier, a.Rarity);
			if (Character.Inventory.Essence < cost) return (false, "no_essence");
			var newRarity = ForgeDB.NextRarity(a.Rarity);
			var fresh = ItemGenerator.RollArmor(a.Id, null, newRarity);
			if (fresh == null) return (false, "not_upgradable");
			stack.ArmorInstance = fresh;
			Character.Inventory.Essence -= cost;
			PushCharacterToServer();
			return (true, null);
		}
		// Щиты в текущей системе без affix-генератора — улучшение поднимает
		// только rarity-поле; UI это покажет, реальный геймплейный эффект
		// добавится когда ItemGenerator научится катать щиты.
		if (stack.ShieldInstance != null)
		{
			var s = stack.ShieldInstance;
			if (!ForgeDB.CanUpgrade(s.Grade, s.Rarity)) return (false, "cant_upgrade");
			long cost = ForgeDB.UpgradeCost(s.Grade, s.Tier, s.Rarity);
			if (Character.Inventory.Essence < cost) return (false, "no_essence");
			s.Rarity = ForgeDB.NextRarity(s.Rarity);
			Character.Inventory.Essence -= cost;
			PushCharacterToServer();
			return (true, null);
		}
		return (false, "not_upgradable");
	}

	// Реролл аффиксов — та же rarity, новые префиксы/суффиксы по бюджету.
	public (bool ok, string reason) ForgeReroll(int slotIndex)
	{
		if (Character == null) return (false, "no_item");
		var slots = Character.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count) return (false, "no_item");
		var stack = slots[slotIndex];

		if (stack.WeaponInstance != null)
		{
			var w = stack.WeaponInstance;
			long cost = ForgeDB.RerollCost(w.Grade, w.Tier);
			if (Character.Inventory.Essence < cost) return (false, "no_essence");
			var fresh = ItemGenerator.RollWeapon(w.Id, null, w.Rarity);
			if (fresh == null) return (false, "not_rerollable");
			stack.WeaponInstance = fresh;
			Character.Inventory.Essence -= cost;
			PushCharacterToServer();
			return (true, null);
		}
		if (stack.ArmorInstance != null)
		{
			var a = stack.ArmorInstance;
			long cost = ForgeDB.RerollCost(a.Grade, a.Tier);
			if (Character.Inventory.Essence < cost) return (false, "no_essence");
			var fresh = ItemGenerator.RollArmor(a.Id, null, a.Rarity);
			if (fresh == null) return (false, "not_rerollable");
			stack.ArmorInstance = fresh;
			Character.Inventory.Essence -= cost;
			PushCharacterToServer();
			return (true, null);
		}
		return (false, "not_rerollable");
	}
}
