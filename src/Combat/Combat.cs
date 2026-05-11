using Godot;
using System;
using System.Collections.Generic;

// Главный контроллер боя. Поддерживает несколько врагов одновременно,
// выбор цели для атак, одновременный ход всех живых врагов в конце хода игрока.
//
// Файлы:
//   Combat.cs            — состояние, _Ready, жизненный цикл боя, кнопки
//   Combat.Cards.cs      — игра карт, выбор цели, расчёт урона
//   Combat.UI.cs         — построение и обновление UI
//   Combat.Animations.cs — твины (всплывающий текст, вылет карты)
public partial class Combat : Control
{
	// Сигнал наверх (Main.cs) когда игрок хочет удалить текущего персонажа.
	[Signal]
	public delegate void ResetCharacterRequestedEventHandler();

	// Сигнал наверх когда бой завершён.
	//   advance=true  — победа, продвигаем по карте к этому узлу.
	//   advance=false — поражение или ручной выход; узел не помечается пройденным.
	[Signal]
	public delegate void CombatExitRequestedEventHandler(bool advance);

	// === Состояние боя (поля) ===
	private List<string> _deck = new();
	private List<string> _hand = new();
	private List<string> _discard = new();
	private List<EnemyData> _encounter = new();
	private bool _combatOver = false;
	private bool _victory = false;       // Бой завершён победой (для UX exit-кнопки).
	private int _turnCount = 0;

	// Индекс выбранной карты в _hand (для режима выбора цели). -1 = ничего не выбрано.
	private int _selectedHandIndex = -1;

	// Активный оверлей инвентаря (null когда закрыт).
	private InventoryOverlay _inventoryOverlay;

	// Активный оверлей лога (null когда закрыт). Сам лог хранится в _logLines.
	private CombatLogOverlay _logOverlay;
	private readonly System.Collections.Generic.List<string> _logLines = new();

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
		_combatOver = false;
		_victory = false;
		_turnCount = 0;
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;
		GameData.Instance.Character.ResetForCombat();

		_encounter = GameData.Instance.SpawnForCurrentNode();
		foreach (var e in _encounter) e.RollIntent();

		_deck = GameData.Instance.CurrentDeckIds();
		Shuffle(_deck);
		_hand.Clear();
		_discard.Clear();
		_endTurnButton.Disabled = false;

		var pc = GameData.Instance.Character;
		ClearLog();
		var node = GameData.Instance.CurrentRun?.CurrentNode();
		string nodeLabel = node?.Type == MapNodeType.Boss ? "БОСС" : "Стычка";
		Log($"[b]=== {GameData.Instance.CurrentLocationName()} — {nodeLabel} ===[/b]");
		Log($"Противников: {_encounter.Count}");
		Log($"Статы: STR {pc.Str}, INT {pc.Int}, CON {pc.Con}, WIT {pc.Wit}, MEN {pc.Men}, DEX {pc.Dex}");
		Log($"🎯 Крит каждые {pc.EffectiveCritEveryN()} атак × {pc.CritMultiplier():F2} урон");
		Log($"Оружие: {ItemsDB.DescribeWeapon(pc.Weapon)}");
		Log($"👕 {pc.Chest?.Name ?? "—"}  ⛑ {pc.Helmet?.Name ?? "—"}");
		Log($"🧤 {pc.Gloves?.Name ?? "—"}  👢 {pc.Boots?.Name ?? "—"}");
		Log($"Колода ({_deck.Count} карт): {DeckSummary()}");

		StartPlayerTurn();
	}

	private void StartPlayerTurn()
	{
		_turnCount++;
		var p = GameData.Instance.Character;

		// Реген МП (на 1м ходу мы на максимуме после ResetForCombat).
		if (_turnCount > 1)
		{
			p.CurrentMp = Math.Min(p.MaxMp(), p.CurrentMp + p.MpRegen());
			Log($"[color=#5dd]Реген маны: +{p.MpRegen()}[/color]");
		}

		// Блок не переносится между ходами игрока.
		p.CurrentBlock = 0;
		DrawToHand(p.HandSize());

		Log($"[b]--- Ход {_turnCount} ---[/b]");
		RefreshUI();
	}

	private void DrawToHand(int target)
	{
		while (_hand.Count < target)
		{
			if (_deck.Count == 0)
			{
				if (_discard.Count == 0) return;
				_deck = new List<string>(_discard);
				Shuffle(_deck);
				_discard.Clear();
				Log("[color=#888]Колода перетасована.[/color]");
			}
			var top = _deck[^1];
			_deck.RemoveAt(_deck.Count - 1);
			_hand.Add(top);
		}
	}

	private void OnEndTurnPressed()
	{
		if (_combatOver) return;
		CancelTargeting();

		// Сброс руки.
		foreach (var c in _hand) _discard.Add(c);
		_hand.Clear();

		// Тик эффектов игрока.
		GameData.Instance.Character.TickEffects();

		// Все живые враги бьют разом.
		EnemyTurn();
		if (_combatOver) return;

		// Перекидывают намерения для следующего хода.
		foreach (var e in _encounter)
			if (e.CurrentHp > 0) e.RollIntent();

		StartPlayerTurn();
	}

	private void EnemyTurn()
	{
		foreach (var enemy in _encounter)
		{
			if (enemy.CurrentHp <= 0) continue;
			enemy.CurrentBlock = 0;
			var intent = enemy.NextIntent;
			if (intent == null) continue;

			switch (intent.Type)
			{
				case "attack":
					Log($"[color=#f88]{enemy.EnemyName}: {intent.Name} ({intent.Amount} дмг)[/color]");
					ApplyDamageToPlayer(intent.Amount);
					if (GameData.Instance.Character.CurrentHp <= 0)
					{
						OnPlayerDead();
						return;
					}
					break;
				case "block":
					enemy.CurrentBlock += intent.Amount;
					Log($"{enemy.EnemyName}: {intent.Name} (+{intent.Amount} блок)");
					break;
			}

			enemy.TickEffects();
		}
	}

	private void OnPlayerDead()
	{
		_combatOver = true;
		_endTurnButton.Disabled = true;
		Log($"[color=#f44][b]{Lang.T("log.player_dead")}[/b][/color]");
		Log("[color=#f44]Нажмите «Новый бой» чтобы начать заново.[/color]");
	}

	private void OnAllEnemiesDead()
	{
		_combatOver = true;
		_victory = true;
		_endTurnButton.Disabled = true;
		Log($"[color=#7f7][b]{Lang.T("log.encounter_cleared")}[/b][/color]");
	}

	// =====================================================================
	// Кнопки
	// =====================================================================

	// Единственная кнопка выхода: меняет смысл по состоянию.
	//   В бою          — «Бежать»: возврат на карту, узел не помечается пройденным.
	//   После победы   — «На карту →»: продвигаемся к этому узлу.
	//   После смерти   — «Выйти из подземелья»: run прерывается.
	private void OnExitPressed()
	{
		bool advance = _combatOver && _victory;
		EmitSignal(SignalName.CombatExitRequested, advance);
	}

	// Использовать зелье из инвентаря (HP / MP / т.д.).
	// Зелья тратят слот в инвентаре, но не ход — можно пить мидл-комбо.
	private void OnUsePotion(string itemId)
	{
		if (_combatOver) return;
		var potion = PotionsDB.Get(itemId);
		if (potion == null) return;

		int hpBefore = GameData.Instance.Character.CurrentHp;
		int mpBefore = GameData.Instance.Character.CurrentMp;
		if (!GameData.Instance.UsePotion(itemId)) return;

		var p = GameData.Instance.Character;
		Log($"[color=#7fa]🧪 Применено: {potion.Name}[/color]");
		if (p.CurrentHp > hpBefore)
			SpawnFloatingText(new Vector2(150, 120), $"+{p.CurrentHp - hpBefore} ХП", UIStyle.HealGreen, 24);
		if (p.CurrentMp > mpBefore)
			SpawnFloatingText(new Vector2(150, 120), $"+{p.CurrentMp - mpBefore} МП", UIStyle.MpFill, 24);

		RefreshUI();
	}

	private void OnResetCharacterPressed()
	{
		// Делегируем удаление и переход в CharacterCreation роутеру (Main.cs).
		EmitSignal(SignalName.ResetCharacterRequested);
	}

	// Открывает модальный оверлей инвентаря поверх боя.
	// Бой не пересоздаётся — только меняется экипировка/инвентарь персонажа.
	// При закрытии оверлей шлёт Closed → пересчитываем UI боя.
	private void OnInventoryPressed()
	{
		if (_inventoryOverlay != null) return;
		// Во время активного боя инвентарь — только просмотр.
		// Можно менять экипировку и пить зелья из инвентаря только когда бой завершён.
		_inventoryOverlay = new InventoryOverlay { ReadOnly = !_combatOver };
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
		// Экипировка могла поменяться — пересчитываем HP/MP cap и т.п.
		var p = GameData.Instance.Character;
		if (p != null)
		{
			p.CurrentHp = Math.Min(p.CurrentHp, p.MaxHp());
			p.CurrentMp = Math.Min(p.CurrentMp, p.MaxMp());
		}
		RefreshUI();
	}

	// Открыть оверлей лога. Содержимое подгружается из буфера _logLines;
	// дальнейший лог дополняет оверлей в реальном времени через AppendLine.
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

	// Лог идёт в буфер. Если оверлей лога открыт — обновляем live.
	private void Log(string msg)
	{
		_logLines.Add(msg);
		_logOverlay?.AppendLine(msg);
	}

	private void ClearLog() => _logLines.Clear();

	private static void Shuffle<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = Rng.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
