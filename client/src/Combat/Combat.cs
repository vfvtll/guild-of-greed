using Godot;
using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Combat;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

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

	// === Боевое состояние — через shared engine ===
	private BattleState _state;

	// UI state.
	private int _selectedHandIndex = -1;
	private InventoryOverlay _inventoryOverlay;
	private CombatLogOverlay _logOverlay;
	private readonly List<string> _logLines = new();

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUI();
		StartNewCombat();
	}

	// =====================================================================
	// Жизненный цикл боя
	// =====================================================================

	private void StartNewCombat()
	{
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;

		var character = GameData.Instance.Character;
		var enemies = GameData.Instance.SpawnForCurrentNode();
		var deck = GameData.Instance.CurrentDeckIds();

		// Seed выдадим локально на И5в.3; в И5в.4 он будет приходить от сервера
		// в BattleStarted, и оба калькулятора (этот клиент и сервер) поедут
		// с одинаковой Rng-последовательностью.
		int seed = Rng.Next(int.MaxValue);

		var (state, events) = CombatEngine.StartBattle(character, enemies, deck, seed);
		_state = state;
		_endTurnButton.Disabled = false;

		ClearLog();
		LogIntro();
		ApplyEvents(events, playedView: null);
		RefreshUI();
	}

	private void LogIntro()
	{
		var pc = _state.Player;
		var node = GameData.Instance.CurrentRun?.CurrentNode();
		string nodeLabel = node?.Type == MapNodeType.Boss ? "БОСС" : "Стычка";
		Log($"[b]=== {GameData.Instance.CurrentLocationName()} — {nodeLabel} ===[/b]");
		Log($"Противников: {_state.Enemies.Count}");
		Log($"Статы: STR {pc.Str}, INT {pc.Int}, CON {pc.Con}, WIT {pc.Wit}, MEN {pc.Men}, DEX {pc.Dex}");
		Log($"🎯 Крит каждые {pc.EffectiveCritEveryN()} атак × {pc.CritMultiplier():F2} урон");
		Log($"Оружие: {ItemsDB.DescribeWeapon(pc.Weapon)}");
		Log($"👕 {pc.Chest?.Name ?? "—"}  ⛑ {pc.Helmet?.Name ?? "—"}");
		Log($"🧤 {pc.Gloves?.Name ?? "—"}  👢 {pc.Boots?.Name ?? "—"}");
		Log($"Колода ({_state.Deck.Count} карт): {DeckSummary()}");
	}

	// Единственная точка применения BattleAction: гонит через engine и
	// рендерит результирующие events. playedView — захваченная CardView
	// (если action — PlayCard), чтобы AnimateCardOut получил правильный
	// узел до того как карта уйдёт из руки.
	private void ApplyAction(BattleAction action, CardView playedView)
	{
		if (_state == null || _state.CombatOver) return;
		var events = CombatEngine.Apply(_state, action);
		ApplyEvents(events, playedView);
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

				case BattleEventType.BattleEnded:
					_endTurnButton.Disabled = true;
					if (ev.Victory)
						Log($"[color=#7f7][b]{Lang.T("log.encounter_cleared")}[/b][/color]");
					break;
			}
		}
	}

	// =====================================================================
	// Кнопки и user input
	// =====================================================================

	private void OnEndTurnPressed()
	{
		if (_state == null || _state.CombatOver) return;
		CancelTargeting();
		ApplyAction(new BattleAction { Type = BattleActionType.EndTurn }, playedView: null);
	}

	// Зелья тратят слот, но не ход — игрок может пить мидл-комбо.
	private void OnUsePotion(string itemId)
	{
		if (_state == null || _state.CombatOver) return;
		var potion = PotionsDB.Get(itemId);
		if (potion == null) return;
		Log($"[color=#7fa]🧪 Применено: {potion.Name}[/color]");
		ApplyAction(new BattleAction
		{
			Type = BattleActionType.UsePotion,
			PotionId = itemId,
		}, playedView: null);
	}

	// Выход:
	//   В бою          — Flee через engine (state.CombatOver=true, victory=false).
	//   После победы   — просто emit signal с advance=true.
	//   После смерти   — просто emit signal с advance=false.
	private void OnExitPressed()
	{
		if (_state == null)
		{
			EmitSignal(SignalName.CombatExitRequested, false);
			return;
		}
		if (!_state.CombatOver)
		{
			ApplyAction(new BattleAction { Type = BattleActionType.Flee }, playedView: null);
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
		_inventoryOverlay = new InventoryOverlay { ReadOnly = !combatOver };
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
