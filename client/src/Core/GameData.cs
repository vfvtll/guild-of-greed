using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GuildOfGreed.Shared.Commands;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;
using GuildOfGreed.Shared.Net;

// Глобальный стейт игры (autoload, синглтон).
//
// === Anti-cheat модель (v15+) =======================================
// Сервер — единственный источник правды для Character. Локальный Character
// здесь — это последняя авторитетная копия, полученная с сервера. Любая
// мутация (купить, надеть, форж, стэш, очки статов) — это:
//   1. await Net.<Command>Async(params) — сервер сам валидирует и применяет.
//   2. Replace Character копией из ответа (CharacterJson).
//   3. UI слушает CharacterUpdated и делает Refresh.
//
// Клиент НЕ применяет мутации локально (для town actions). В бою — отдельный
// механизм CSP с детерминированным CombatEngine.
public partial class GameData : Node
{
	public static GameData Instance { get; private set; }

	public CharacterData Character { get; private set; }
	public UserSession Session { get; private set; }

	// Сетевой клиент. Устанавливается из Main после handshake. Используется
	// всеми мутирующими методами ниже для отправки команд на сервер.
	public NetworkClient Net { get; set; }
	public int SelectedLoadout = 0;
	public int SelectedChest = 2;
	public int SelectedLocation = 0;

	// Карта текущего забега. null = игрок не в подземелье (на экране выбора локации
	// или в главном меню). Создаётся StartRun, обнуляется EndRun.
	public RunMap CurrentRun { get; private set; }

	// Эмитится каждый раз, когда Character заменяется свежей серверной копией
	// (после успешной команды или StartRun). UI-оверлеи могут подписаться,
	// чтобы реагировать на изменения без явных вызовов Refresh.
	[Signal] public delegate void CharacterUpdatedEventHandler();

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
	// нагрудник нового персонажа (используется в UI выбора при создании).
	public static readonly List<string> ChestList = new()
	{
		"robe_chest_power_low",
		"robe_chest_wisdom_low",
		"light_chest_strength_low",
		"light_chest_vigor_low",
	};

	public static readonly string[] LocationNames =
	{
		"Подземелье",
		"Тёмный лес (5 врагов)",
		"Логово босса",
		"Звериная балка",
		"Разбойничья застава",
	};

	public static readonly string[] LocationHints =
	{
		"Один обычный гоблин — стандартный бой",
		"Стая из 5 гоблинов — длинный забег, тест на выживаемость",
		"Тёмный рыцарь — высокий риск, высокая награда",
		"Глухая чаща с крепким зверьём: волки, вепри, росомаха.\n8 рядов карты, в конце ждёт бурый медведь.",
		"Бандитский притон в старых развалинах: разбойники, каторжники,\nнаёмники. 10 рядов — самый длинный забег. В конце — главарь шайки.",
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

	// Назначить активного персонажа (после SelectCharacter / CharacterCreation
	// / любой серверной команды). EmittERS CharacterUpdated.
	public void SetCharacter(CharacterData character)
	{
		Character = character;
		EmitSignal(SignalName.CharacterUpdated);
	}

	// === Run lifecycle (карта подземелья) ===
	// Старт забега: сид приходит ОТ СЕРВЕРА (StartRunResponse.RunSeed) —
	// один на всё подземелье, из него генерится карта и выводятся боевые
	// RNG-потоки. Карта и колода детерминированы от seed + character_snapshot.
	// Полный restore HP/MP делает сервер (см. Session.HandleStartRun) — здесь
	// только локальная карта.
	public void StartRun(int locationIndex, int seed)
	{
		if (locationIndex < 0 || locationIndex >= LocationNames.Length) locationIndex = 0;
		SelectedLocation = locationIndex;
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

	// === Колода + локация ===
	// Колода + спавн делегированы в shared (см. CardsDB.DeckFor / EnemyData.SpawnFor).
	// Server использует те же helpers — обе стороны заведомо согласованы.
	public List<string> CurrentDeckIds() => CardsDB.DeckFor(Character);

	public string CurrentLocationName() => LocationNames[SelectedLocation];

	// =========================================================================
	// Character commands (anti-cheat: всё через сервер)
	// =========================================================================

	private static readonly System.Text.Json.JsonSerializerOptions CharJsonOpts =
		new() { IncludeFields = true };

	// Парсит CharacterJson из CharacterCommandResponse и заменяет Character.
	// Returns true если успешно подменили; false — если JSON пустой/битый (тогда
	// Character остаётся прежним, UI всё равно вызовет Refresh).
	private bool ApplyCharacterFromResponse(string characterJson)
	{
		if (string.IsNullOrEmpty(characterJson)) return false;
		try
		{
			var fresh = System.Text.Json.JsonSerializer.Deserialize<CharacterData>(
				characterJson, CharJsonOpts);
			if (fresh == null) return false;
			Character = fresh;
			EmitSignal(SignalName.CharacterUpdated);
			return true;
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"GameData: failed to apply server character: {ex.Message}");
			return false;
		}
	}

	// Общая обёртка для всех команд. Шлёт RPC, при любом ответе (success/fail)
	// синхронизирует Character с серверной копией (сервер всегда отдаёт state
	// чтобы клиент мог восстановить рассинхрон). Возвращает Result для UI.
	public readonly struct CommandOutcome
	{
		public readonly bool Ok;
		public readonly string Error;
		public readonly long Value;
		public CommandOutcome(bool ok, string err, long v) { Ok = ok; Error = err; Value = v; }
	}

	private async Task<CommandOutcome> RunAsync(
		System.Func<NetworkClient, Task<CharacterCommandResponse>> call,
		string opName)
	{
		if (Net == null) return new CommandOutcome(false, "no_network", 0);
		try
		{
			var resp = await call(Net);
			ApplyCharacterFromResponse(resp.CharacterJson);
			return new CommandOutcome(resp.Success, resp.Error, resp.Value);
		}
		catch (ServerException ex)
		{
			GD.PrintErr($"GameData: {opName} server error: {ex.Code} {ex.Message}");
			return new CommandOutcome(false, ex.Code, 0);
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"GameData: {opName} network error: {ex.Message}");
			return new CommandOutcome(false, "network_error", 0);
		}
	}

	// === Лавка ===
	public Task<CommandOutcome> BuyOneAsync(string itemId)
		=> RunAsync(net => net.BuyItemAsync(itemId), "BuyItem");

	public Task<CommandOutcome> SellSlotAsync(int slotIndex)
		=> RunAsync(net => net.SellSlotAsync(slotIndex), "SellSlot");

	// === Экипировка ===
	public Task<CommandOutcome> EquipFromInventoryAsync(int slotIndex)
		=> RunAsync(net => net.EquipFromInventoryAsync(slotIndex), "Equip");

	public Task<CommandOutcome> UnequipSlotAsync(CharacterCommands.EquipSlotKind slot)
		=> RunAsync(net => net.UnequipSlotAsync((int)slot), "Unequip");

	// === Зелье вне боя ===
	public Task<CommandOutcome> UsePotionAsync(string itemId)
		=> RunAsync(net => net.UsePotionAsync(itemId), "UsePotion");

	// === Стэш ===
	public Task<CommandOutcome> DepositToStashAsync(int invSlotIndex)
		=> RunAsync(net => net.DepositToStashAsync(invSlotIndex), "DepositStash");

	public Task<CommandOutcome> WithdrawFromStashAsync(int stashSlotIndex)
		=> RunAsync(net => net.WithdrawFromStashAsync(stashSlotIndex), "WithdrawStash");

	// === Кузница ===
	public Task<CommandOutcome> ForgeDismantleAsync(int slotIndex)
		=> RunAsync(net => net.ForgeDismantleAsync(slotIndex), "ForgeDismantle");

	public Task<CommandOutcome> ForgeUpgradeAsync(int slotIndex)
		=> RunAsync(net => net.ForgeUpgradeAsync(slotIndex), "ForgeUpgrade");

	public Task<CommandOutcome> ForgeRerollAsync(int slotIndex)
		=> RunAsync(net => net.ForgeRerollAsync(slotIndex), "ForgeReroll");

	// === Очки статов ===
	public Task<CommandOutcome> SpendStatPointAsync(string stat)
		=> RunAsync(net => net.SpendStatPointAsync(stat), "SpendStat");

	// === Крафт ===
	public Task<CommandOutcome> CraftItemAsync(string itemId)
		=> RunAsync(net => net.CraftItemAsync(itemId), "Craft");

	// === Грейд (Гильдия) ===
	public Task<CommandOutcome> PromoteGradeAsync()
		=> RunAsync(net => net.PromoteGradeAsync(), "PromoteGrade");
}
