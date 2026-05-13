using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GuildOfGreed.Shared.Combat;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;
using GuildOfGreed.Shared.Net;

// Главный контроллер боя.
//
// С И5в.3 вся боевая логика живёт в shared/Combat/CombatEngine. Этот файл —
// View-обёртка: собирает BattleAction из user input, прокатывает через
// engine, рендерит итоговые BattleEvent (лог, анимации, всплывающий текст).
// Это та же модель, что побежит на сервере — base для CSP.
//
// Файлы:
//   Combat.cs            — состояние боя, _Ready, ApplyEvents, кнопки
//   Combat.Cards.cs      — обработка кликов по картам/врагам, выбор цели
//   Combat.UI.cs         — построение и обновление UI
//   Combat.Animations.cs — твины (всплывающий текст, вылет карты)
public partial class Combat : Control
{
	[Signal] public delegate void ResetCharacterRequestedEventHandler();

	// Бой завершён: advance=true — победа (продвижение по карте);
	// advance=false — поражение или ручной выход (узел не отмечается).
	[Signal] public delegate void CombatExitRequestedEventHandler(bool advance);

	// Сетевой клиент. Передаётся из Main через property, потому что у Godot Node
	// нет нормальных конструкторов с параметрами.
	public NetworkClient Net { get; set; }

	// Override'ы для боёв вне забега (стартовый Tutorial-бой). Когда заданы —
	// игнорируют GameData.CurrentRun. Если null — берётся обычный путь
	// (SelectedLocation + текущий узел карты).
	public int? LocationOverride { get; set; }
	public int? NodeTypeOverride { get; set; }

	// === Боевое состояние — через shared engine ===
	private BattleState _state;

	// UI state.
	private int _selectedHandIndex = -1;
	private bool _busy;                 // блокирует input на время network round-trip.
	private InventoryOverlay _inventoryOverlay;
	private CombatLogOverlay _logOverlay;
	private readonly List<string> _logLines = new();

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUI();
		_ = StartNewCombatAsync();
	}

	// =====================================================================
	// Жизненный цикл боя
	// =====================================================================

	// Старт боя теперь требует сервер: он выдаёт seed, мы прокатываем тот же
	// StartBattle с теми же входными данными — обе стороны идут в синхрон.
	private async Task StartNewCombatAsync()
	{
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;

		var node = GameData.Instance.CurrentRun?.CurrentNode();
		int locationIndex = LocationOverride ?? GameData.Instance.SelectedLocation;
		int nodeType = NodeTypeOverride ?? (int)(node?.Type ?? MapNodeType.Battle);
		// nodeId нужен серверу для вывода детерминированного battleSeed из
		// (runSeed, nodeId). Туториал (CurrentRun == null) — отправляем -1.
		int nodeId = node?.Id ?? -1;

		BattleStartedResponse resp;
		try
		{
			resp = await Net.StartBattleAsync(locationIndex, nodeType, nodeId);
		}
		catch (ServerException ex)
		{
			GD.PrintErr($"Combat: server refused StartBattle: {ex.Code}");
			EmitSignal(SignalName.CombatExitRequested, false);
			return;
		}
		catch (Exception ex)
		{
			// Транспортный сбой — NetworkClient уже эмитнул Disconnected,
			// Main покажет ReconnectOverlay. Молча выходим, не дублируем UI.
			GD.PrintErr($"Combat: StartBattle network error: {ex.Message}");
			return;
		}
		if (!resp.Success)
		{
			GD.PrintErr($"Combat: server refused StartBattle: {resp.Error}");
			EmitSignal(SignalName.CombatExitRequested, false);
			return;
		}

		// Сервер уже знает encounter/deck — мы вычисляем то же из shared helpers.
		// Для run-боёв берём замороженную колоду забега (CurrentRun.LockedDeck),
		// для туториала — DeckFor от текущего персонажа. Server строит колоду
		// идентично: из своего _runSnapshot или живого character (см. Session).
		var character = GameData.Instance.Character;
		var enemies = EnemyData.SpawnFor(locationIndex, (MapNodeType)nodeType);
		var lockedDeck = GameData.Instance.CurrentRun?.LockedDeck;
		var deck = lockedDeck != null && lockedDeck.Count > 0
			? new List<string>(lockedDeck)
			: CardsDB.DeckFor(character);

		var (state, events) = CombatEngine.StartBattle(character, enemies, deck, resp.Seed);
		_state = state;
		_endTurnButton.Disabled = false;
		_busy = false;

		ClearLog();
		LogIntro();
		ApplyEvents(events, playedView: null);
		RefreshUI();
	}

	private void LogIntro()
	{
		var pc = _state.Player;
		var node = GameData.Instance.CurrentRun?.CurrentNode();
		bool isTutorial = NodeTypeOverride == (int)MapNodeType.Tutorial;
		string nodeLabel = isTutorial ? "ОБУЧЕНИЕ"
			: node?.Type == MapNodeType.Boss ? "БОСС"
			: "Стычка";
		string locationLabel = isTutorial ? "Опушка леса" : GameData.Instance.CurrentLocationName();
		Log($"[b]=== {locationLabel} — {nodeLabel} ===[/b]");
		Log($"Противников: {_state.Enemies.Count}");
		Log($"Уровень {pc.Level} ({pc.Exp}/{pc.XpForNextCharacterLevel()} XP)");
		Log($"Статы: STR {pc.Str}, INT {pc.Int}, CON {pc.Con}, WIT {pc.Wit}, MEN {pc.Men}, DEX {pc.Dex}");
		Log($"🎯 Крит каждые {pc.EffectiveCritEveryN()} атак × {pc.CritMultiplier():F2} урон");
		Log($"Оружие: {ItemsDB.DescribeWeapon(pc.Weapon)}");
		if (pc.Weapon != null)
		{
			int wlvl = pc.GetWeaponLevel(pc.Weapon.Type);
			int wxp  = pc.GetWeaponXp(pc.Weapon.Type);
			int wnext = pc.XpForNextWeaponLevel(pc.Weapon.Type);
			Log($"Навык оружия: ур.{wlvl} ({wxp}/{wnext} XP)");
		}
		Log($"👕 {pc.Chest?.Name ?? "—"}  ⛑ {pc.Helmet?.Name ?? "—"}");
		Log($"🧤 {pc.Gloves?.Name ?? "—"}  👢 {pc.Boots?.Name ?? "—"}");
		Log($"Колода ({_state.Deck.Count} карт): {DeckSummary()}");
	}

	// Единая точка действия игрока. Pattern:
	//   1. Optimistic apply через локальный engine — игрок видит результат сразу.
	//   2. Параллельно шлём action на server — он применяет ту же логику
	//      с тем же seed и подтверждает Confirmed=true (или Rejected при mismatch).
	//   3. На время round-trip UI заблокирован (_busy=true) — простой защитный
	//      механизм от спама. При Rejected — выкидываем игрока в LocationSelect.
	private async Task ApplyActionAsync(BattleAction action, CardView playedView)
	{
		if (_state == null || _state.CombatOver) return;
		if (_busy) return;
		_busy = true;
		try
		{
			var events = CombatEngine.Apply(_state, action);
			ApplyEvents(events, playedView);
			RefreshUI();

			if (Net == null) return;
			BattleActionResponse resp;
			try
			{
				resp = await Net.SendBattleActionAsync(
					(int)action.Type, action.HandIndex, action.TargetEnemyIndex, action.PotionId);
			}
			catch (ServerException ex)
			{
				GD.PrintErr($"Combat: server rejected action: {ex.Code}");
				EmitSignal(SignalName.CombatExitRequested, false);
				return;
			}
			catch (Exception ex)
			{
				// Транспорт упал — overlay уже работает. Не дёргаем CombatExit.
				GD.PrintErr($"Combat: action network error: {ex.Message}");
				return;
			}
			if (!resp.Confirmed)
			{
				GD.PrintErr($"Combat: server rejected action: {resp.Error} — fetching authoritative state");
				Log("[color=#fc4]⚠ Рассинхронизация — состояние восстановлено с сервера.[/color]");
				await RefreshStateFromServerAsync();
			}
		}
		finally
		{
			_busy = false;
		}
	}

	// При rejection клиент берёт авторитетную копию боя у сервера и
	// перестраивает _state. RNG догоняется через AdvanceTo, чтобы потоки
	// случайностей снова совпали — следующее действие должно подтвердиться.
	private async Task RefreshStateFromServerAsync()
	{
		BattleStateResponse snap;
		try
		{
			snap = await Net.GetBattleStateAsync();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Combat: GetBattleState failed: {ex.Message}");
			return;
		}
		if (!snap.Success)
		{
			GD.PrintErr($"Combat: GetBattleState error: {snap.Error}");
			return;
		}

		var jsonOpts = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
		var newPlayer = System.Text.Json.JsonSerializer.Deserialize<CharacterData>(snap.PlayerJson, jsonOpts);
		var newEnemies = System.Text.Json.JsonSerializer.Deserialize<List<EnemyData>>(snap.EnemiesJson, jsonOpts);
		if (newPlayer == null || newEnemies == null) return;

		// Replace global Character reference — UI слои (LocationSelectView и
		// MapView) читают GameData.Character, _state.Player должен указывать туда же.
		GameData.Instance.SetCharacter(newPlayer);

		_state.Player = newPlayer;
		_state.Enemies = newEnemies;
		_state.Deck = snap.Deck ?? new List<string>();
		_state.Hand = snap.Hand ?? new List<string>();
		_state.Discard = snap.Discard ?? new List<string>();
		_state.TurnCount = snap.TurnCount;
		_state.Seed = snap.Seed;
		_state.CombatOver = snap.CombatOver;
		_state.Victory = snap.Victory;
		_state.Rng = new RandomSource(snap.Seed);
		_state.Rng.AdvanceTo(snap.RngCalls);

		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;
		_endTurnButton.Disabled = _state.CombatOver;
		RefreshUI();
	}

	// Прокатывает список events: лог, всплывающий текст, анимации, vibration.
	private void ApplyEvents(List<BattleEvent> events, CardView playedView)
	{
		foreach (var ev in events)
		{
			switch (ev.Type)
			{
				case BattleEventType.BattleStarted: break;

				case BattleEventType.TurnStarted:
					Log($"[b]--- Ход {ev.Amount} ---[/b]");
					break;

				case BattleEventType.DeckShuffled:
					Log("[color=#888]Колода перетасована.[/color]");
					break;

				case BattleEventType.CardDrawn: break;       // визуала добора пока нет

				case BattleEventType.MpSpent:
					Log($"[color=#5af](−{ev.Amount} MP)[/color]");
					break;

				case BattleEventType.MpRegenerated:
					Log($"[color=#5dd]Реген маны: +{ev.Amount}[/color]");
					break;

				case BattleEventType.HpHealed:
					Log($"[color=#7fa]Исцеление: +{ev.Amount} ХП[/color]");
					SpawnFloatingText(new Vector2(150, 110), $"+{ev.Amount} ХП",
						UIStyle.HealGreen, 22);
					break;

				case BattleEventType.BlockGained:
					Log($"Блок: +{ev.Amount} (всего {_state.Player.CurrentBlock})");
					SpawnFloatingText(new Vector2(150, 110), $"+{ev.Amount} БЛОК",
						UIStyle.BlockCyan, 22);
					break;

				case BattleEventType.DamageDealtToEnemy:
					RenderDamageToEnemy(ev);
					break;

				case BattleEventType.DamageDealtToPlayer:
					RenderDamageToPlayer(ev);
					break;

				case BattleEventType.EffectApplied:
					RenderEffectApplied(ev);
					break;

				case BattleEventType.EffectTicked: break;     // нет UI-эффекта

				case BattleEventType.EnemyIntentRolled: break; // отрисует EnemyView через Refresh

				case BattleEventType.EnemyAction:
					if (ev.EnemyIndex >= 0 && ev.EnemyIndex < _state.Enemies.Count)
						Log($"{_state.Enemies[ev.EnemyIndex].EnemyName}: {ev.IntentName} (+{ev.Amount} блок)");
					break;

				case BattleEventType.CardDiscarded:
					if (playedView != null) { AnimateCardOut(playedView); playedView = null; }
					break;

				case BattleEventType.EnemyDied:
					if (ev.EnemyIndex >= 0 && ev.EnemyIndex < _state.Enemies.Count)
					{
						Log($"[color=#7f7]✓ {_state.Enemies[ev.EnemyIndex].EnemyName} повержен.[/color]");
						Input.VibrateHandheld(120);
					}
					break;

				case BattleEventType.LootDropped:
					RenderLootDropped(ev);
					break;

				case BattleEventType.PlayerDied:
					_endTurnButton.Disabled = true;
					Log($"[color=#f44][b]{Lang.T("log.player_dead")}[/b][/color]");
					break;

				case BattleEventType.BleedStacked:
					if (ev.EnemyIndex >= 0 && ev.EnemyIndex < _state.Enemies.Count)
						Log($"[color=#f55]🩸 {_state.Enemies[ev.EnemyIndex].EnemyName}: +{ev.Amount} кровотечения (стак {_state.Enemies[ev.EnemyIndex].BleedStack})[/color]");
					break;

				case BattleEventType.BleedTicked:
					if (ev.EnemyIndex >= 0 && ev.EnemyIndex < _state.Enemies.Count)
						Log($"[color=#f55]🩸 {_state.Enemies[ev.EnemyIndex].EnemyName} — кровотечение: −{ev.Amount} HP[/color]");
					break;

				case BattleEventType.XpGained:
					Log($"[color=#7fb]+{ev.Amount} опыта[/color]");
					break;

				case BattleEventType.WeaponXpGained:
					// Намеренно НЕ логируем — слишком спамно (тикает на каждой
					// атакующей карте). Игрок видит итог в инвентаре и при
					// WeaponLevelUp.
					break;

				case BattleEventType.CharacterLevelUp:
					Log($"[color=#fa3][b]⭐ Уровень повышен — {ev.Amount}![/b][/color]");
					Log($"[color=#fa3]+{CharacterData.StatPointsPerLevel} очка на распределение (в инвентаре после боя).[/color]");
					Input.VibrateHandheld(150);
					break;

				case BattleEventType.WeaponLevelUp:
					Log($"[color=#fa3][b]🗡 Оружие прокачено: {ItemsDB.WeaponTypeName(ev.EffectType)} → ур. {ev.Amount}[/b][/color]");
					Log($"[color=#fa3]Колода обновится перед следующим боем.[/color]");
					Input.VibrateHandheld(150);
					break;

				case BattleEventType.BattleEnded:
					if (ev.Victory)
					{
						_endTurnButton.Text = "🗺 Переход на карту";
						_endTurnButton.Disabled = false;
						Log($"[color=#7f7][b]{Lang.T("log.encounter_cleared")}[/b][/color]");
					}
					else
					{
						_endTurnButton.Disabled = true;
					}
					break;
			}
		}
	}

	// =====================================================================
	// Кнопки и user input
	// =====================================================================

	private async void OnTurnOrAdvancePressed()
	{
		// После победы кнопка превращается в "Переход на карту" — эмитим
		// exit с advance=true (как обычный CombatExit при победе).
		if (_state != null && _state.CombatOver && _state.Victory)
		{
			EmitSignal(SignalName.CombatExitRequested, true);
			return;
		}
		if (_state == null || _state.CombatOver || _busy) return;
		CancelTargeting();
		await ApplyActionAsync(new BattleAction { Type = BattleActionType.EndTurn }, playedView: null);
	}

	// Зелья тратят слот, но не ход — игрок может пить мидл-комбо.
	private async void OnUsePotion(string itemId)
	{
		if (_state == null || _state.CombatOver || _busy) return;
		var potion = PotionsDB.Get(itemId);
		if (potion == null) return;
		Log($"[color=#7fa]🧪 Применено: {potion.Name}[/color]");
		await ApplyActionAsync(new BattleAction
		{
			Type = BattleActionType.UsePotion,
			PotionId = itemId,
		}, playedView: null);
	}

	// Выход:
	//   В бою          — Flee через engine (state.CombatOver=true, victory=false).
	//   После победы   — просто emit signal с advance=true.
	//   После смерти   — просто emit signal с advance=false.
	private async void OnExitPressed()
	{
		if (_state == null)
		{
			EmitSignal(SignalName.CombatExitRequested, false);
			return;
		}
		if (!_state.CombatOver)
		{
			await ApplyActionAsync(new BattleAction { Type = BattleActionType.Flee }, playedView: null);
		}
		bool advance = _state.CombatOver && _state.Victory;
		EmitSignal(SignalName.CombatExitRequested, advance);
	}

	private void OnResetCharacterPressed()
		=> EmitSignal(SignalName.ResetCharacterRequested);

	// Открывает модальный оверлей инвентаря поверх боя.
	// Во время активного боя — только просмотр; после завершения — полный доступ.
	private void OnInventoryPressed()
	{
		if (_inventoryOverlay != null) return;
		bool combatOver = _state != null && _state.CombatOver;
		// В бою (CurrentRun!=null) — экипировка заморожена даже после конца боя
		// (выход из подземелья = выход из забега). Зелья и stat-points раздаются
		// только в городе.
		bool inRun = GameData.Instance.CurrentRun != null;
		_inventoryOverlay = new InventoryOverlay { ReadOnly = !combatOver, RunLocked = inRun };
		_inventoryOverlay.Closed += OnInventoryClosed;
		AddChild(_inventoryOverlay);
	}

	private void OnInventoryClosed()
	{
		if (_inventoryOverlay != null)
		{
			RemoveChild(_inventoryOverlay);
			_inventoryOverlay.QueueFree();
			_inventoryOverlay = null;
		}
		var p = GameData.Instance.Character;
		if (p != null)
		{
			p.CurrentHp = Math.Min(p.CurrentHp, p.MaxHp());
			p.CurrentMp = Math.Min(p.CurrentMp, p.MaxMp());
		}
		RefreshUI();
	}

	private void OnLogPressed()
	{
		if (_logOverlay != null) return;
		_logOverlay = new CombatLogOverlay();
		_logOverlay.Closed += OnLogClosed;
		AddChild(_logOverlay);
		_logOverlay.SetContent(string.Join("\n", _logLines));
	}

	private void OnLogClosed()
	{
		if (_logOverlay == null) return;
		RemoveChild(_logOverlay);
		_logOverlay.QueueFree();
		_logOverlay = null;
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is InputEventKey key && key.Pressed && key.Keycode == Key.Escape && _selectedHandIndex >= 0)
		{
			CancelTargeting();
			RefreshUI();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right && _selectedHandIndex >= 0)
		{
			CancelTargeting();
			RefreshUI();
			GetViewport().SetInputAsHandled();
		}
	}

	// =====================================================================
	// Утилиты
	// =====================================================================

	private void Log(string msg)
	{
		_logLines.Add(msg);
		_logOverlay?.AppendLine(msg);
	}

	private void ClearLog() => _logLines.Clear();
}
