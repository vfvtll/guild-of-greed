using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Главный контроллер боя.
// Поддерживает несколько врагов одновременно, выбор цели для атак,
// одновременный ход всех живых врагов в конце хода игрока.
public partial class Combat : Control
{
	// === Состояние боя ===
	private List<string> _deck = new();
	private List<string> _hand = new();
	private List<string> _discard = new();
	private List<EnemyData> _encounter = new();
	private bool _combatOver = false;
	private int _turnCount = 0;

	// Индекс выбранной карты в _hand (для режима выбора цели). -1 = ничего не выбрано.
	private int _selectedHandIndex = -1;

	// === UI ===
	private Button _loadoutButton, _armorButton, _restartButton, _endTurnButton;
	private Label _playerNameLabel, _hpLabel, _mpLabel, _statsLabel, _equipLabel, _blockLabel, _buffsLabel;
	private ProgressBar _hpBar, _mpBar;
	private HBoxContainer _enemyArea;
	private Label _deckCountLabel, _discardCountLabel;
	private HBoxContainer _handContainer;
	private RichTextLabel _logText;
	private Label _targetingHint;
	private PanelContainer _targetingBanner;

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
		_turnCount = 0;
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;
		GameData.Instance.Character.ResetForCombat();

		_encounter = GameData.Instance.SpawnEnemies();
		foreach (var e in _encounter) e.RollIntent();

		_deck = GameData.Instance.CurrentDeckIds();
		Shuffle(_deck);
		_hand.Clear();
		_discard.Clear();
		_endTurnButton.Disabled = false;

		var pc = GameData.Instance.Character;
		ClearLog();
		Log($"[b]=== {GameData.Instance.CurrentLocationName()} — {GameData.Instance.CurrentLoadoutName()} ===[/b]");
		Log($"[i]{GameData.Instance.CurrentLocationHint()}[/i]");
		Log($"Противников: {_encounter.Count}");
		Log($"Статы: STR {pc.Str}, INT {pc.Int}, CON {pc.Con}, WIT {pc.Wit}, MEN {pc.Men}");
		Log($"Оружие: {ItemsDB.DescribeWeapon(pc.Weapon)}");
		Log($"Броня:  {ItemsDB.DescribeArmor(pc.Armor)}");
		Log($"Колода ({_deck.Count} карт): {DeckSummary()}");

		StartPlayerTurn();
	}

	private void StartPlayerTurn()
	{
		_turnCount++;
		var p = GameData.Instance.Character;

		if (_turnCount > 1)
		{
			p.CurrentMp = Math.Min(p.MaxMp(), p.CurrentMp + p.MpRegen());
			Log($"[color=#5dd]Реген маны: +{p.MpRegen()}[/color]");
		}

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

	// =====================================================================
	// Игра карты + выбор цели
	// =====================================================================

	private void OnCardClicked(CardView view)
	{
		if (_combatOver) return;

		// Найти индекс кликнутой карты в руке (по позиции в HBox).
		int idx = -1;
		for (int i = 0; i < _handContainer.GetChildCount(); i++)
			if (_handContainer.GetChild(i) == view) { idx = i; break; }
		if (idx < 0 || idx >= _hand.Count) return;

		var card = view.CardData;
		var p = GameData.Instance.Character;

		if (p.CurrentMp < card.Cost)
		{
			Log($"[color=#888]Недостаточно маны: {card.Name} стоит {card.Cost}.[/color]");
			return;
		}

		bool needsTarget = card.Effect == "damage_phys"
			|| card.Effect == "damage_magic"
			|| card.Effect == "debuff_phys";

		if (!needsTarget)
		{
			PlayCard(idx, null);
			return;
		}

		var alive = _encounter.Where(e => e.CurrentHp > 0).ToList();
		if (alive.Count == 0) return;
		if (alive.Count == 1)
		{
			PlayCard(idx, alive[0]);
			return;
		}

		// Несколько целей — переходим в режим выбора.
		if (_selectedHandIndex == idx)
		{
			CancelTargeting();
		}
		else
		{
			_selectedHandIndex = idx;
			_targetingHint.Text = $"Выберите цель для «{card.Name}» (ESC / ПКМ — отмена)";
			_targetingBanner.Visible = true;
		}
		RefreshUI();
	}

	private void OnEnemyTargeted(EnemyView view)
	{
		if (_selectedHandIndex < 0) return;
		if (view.Enemy == null || view.Enemy.CurrentHp <= 0) return;
		int idx = _selectedHandIndex;
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;
		PlayCard(idx, view.Enemy);
	}

	private void CancelTargeting()
	{
		if (_selectedHandIndex < 0) return;
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;
	}

	private void PlayCard(int handIndex, EnemyData target)
	{
		if (handIndex < 0 || handIndex >= _hand.Count) return;

		// Захватываем view ДО изменения данных — для анимации розыгрыша.
		CardView playedView = null;
		if (handIndex < _handContainer.GetChildCount())
			playedView = _handContainer.GetChild(handIndex) as CardView;

		var cardId = _hand[handIndex];
		var card = CardsDB.GetCard(cardId);
		var p = GameData.Instance.Character;

		p.CurrentMp -= card.Cost;
		Log($"[b]Сыграна карта:[/b] {card.Name} [color=#5af](-{card.Cost} MP)[/color]");

		switch (card.Effect)
		{
			case "damage_phys":
			{
				int dmg = CardsDB.ComputePhysDamage(card, p, target);
				ApplyDamageToEnemy(target, dmg, true);
				break;
			}
			case "damage_magic":
			{
				int dmg = CardsDB.ComputeMagicDamage(card, p, target);
				ApplyDamageToEnemy(target, dmg, false);
				break;
			}
			case "block":
			{
				int amount = CardsDB.ComputeBlock(card, p);
				p.CurrentBlock += amount;
				Log($"Блок: +{amount} (всего {p.CurrentBlock})");
				SpawnFloatingText(new Vector2(150, 110), $"+{amount} БЛОК", new Color(0.7f, 0.9f, 1.0f), 22);
				break;
			}
			case "heal":
			{
				int amount = CardsDB.ComputeHeal(card, p);
				int before = p.CurrentHp;
				p.CurrentHp = Math.Min(p.CurrentHp + amount, p.MaxHp());
				int healed = p.CurrentHp - before;
				Log($"[color=#7fa]Исцеление: +{healed} ХП[/color]");
				SpawnFloatingText(new Vector2(150, 110), $"+{healed} ХП", new Color(0.55f, 1.0f, 0.6f), 22);
				break;
			}
			case "debuff_phys":
				if (target != null)
				{
					target.AddEffect("armor_break", "phys_taken_pct", card.AmountPct, card.Duration);
					Log($"{target.EnemyName} получает +{card.AmountPct}% физ. урона на {card.Duration} х.");
				}
				break;
			case "buff_magic":
				p.AddEffect("magic_focus", "magic_dmg_pct", card.AmountPct, card.Duration);
				Log($"Вы наносите +{card.AmountPct}% маг. урона {card.Duration} ходов.");
				break;
		}

		_hand.RemoveAt(handIndex);
		_discard.Add(cardId);

		// Анимация розыгрыша: карта вылетает вверх и тает.
		if (playedView != null) AnimateCardOut(playedView);

		if (AllEnemiesDead()) OnAllEnemiesDead();
		RefreshUI();
	}

	private bool AllEnemiesDead()
	{
		foreach (var e in _encounter)
			if (e.CurrentHp > 0) return false;
		return true;
	}

	// === Применение урона ===

	private void ApplyDamageToEnemy(EnemyData enemy, int dmg, bool isPhys)
	{
		if (enemy == null) return;
		int defense = isPhys ? enemy.PhysDef : enemy.MagicDef;
		dmg = Math.Max(1, dmg - defense);
		int absorbed = 0;
		if (enemy.CurrentBlock > 0)
		{
			absorbed = Math.Min(enemy.CurrentBlock, dmg);
			enemy.CurrentBlock -= absorbed;
			dmg -= absorbed;
		}
		enemy.CurrentHp = Math.Max(0, enemy.CurrentHp - dmg);
		string kind = isPhys ? "Физ" : "Маг";
		Log(absorbed > 0
			? $"→ {enemy.EnemyName}: {kind} урон {dmg} (поглощено блоком: {absorbed})"
			: $"→ {enemy.EnemyName}: {kind} урон {dmg}");

		// Визуал: всплывающее число + красная вспышка.
		var ev = FindEnemyView(enemy);
		if (ev != null)
		{
			var pos = ev.GlobalPosition + new Vector2(ev.Size.X / 2f, ev.Size.Y * 0.25f);
			var color = isPhys ? new Color(1.0f, 0.6f, 0.35f) : new Color(0.75f, 0.55f, 1.0f);
			SpawnFloatingText(pos, $"-{dmg}", color, 28);
			ev.Flash();
		}

		if (enemy.CurrentHp <= 0)
			Log($"[color=#7f7]✓ {enemy.EnemyName} повержен.[/color]");
	}

	private void ApplyDamageToPlayer(int dmg)
	{
		var p = GameData.Instance.Character;
		dmg = Math.Max(1, dmg - p.PhysDef());
		int absorbed = 0;
		if (p.CurrentBlock > 0)
		{
			absorbed = Math.Min(p.CurrentBlock, dmg);
			p.CurrentBlock -= absorbed;
			dmg -= absorbed;
		}
		p.CurrentHp = Math.Max(0, p.CurrentHp - dmg);
		Log(absorbed > 0
			? $"[color=#f88]Получен урон: {dmg} (поглощено: {absorbed})[/color]"
			: $"[color=#f88]Получен урон: {dmg}[/color]");

		// Визуал у панели игрока (приблизительный центр верха).
		SpawnFloatingText(new Vector2(150, 100), $"-{dmg}", new Color(1.0f, 0.4f, 0.4f), 28);
	}

	// =====================================================================
	// Анимации
	// =====================================================================

	// Всплывающий текст (урон, исцеление, блок) — поднимается и тает.
	private void SpawnFloatingText(Vector2 globalPos, string text, Color color, int fontSize)
	{
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		label.AddThemeConstantOverride("outline_size", 5);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.MouseFilter = MouseFilterEnum.Ignore;
		label.ZIndex = 100;
		label.CustomMinimumSize = new Vector2(80, 0);
		AddChild(label);
		label.GlobalPosition = globalPos - new Vector2(40, 0);

		var t = CreateTween().SetParallel(true);
		t.TweenProperty(label, "position:y", label.Position.Y - 55, 0.75f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(label, "modulate:a", 0f, 0.75f).SetDelay(0.25f);
		t.Chain().TweenCallback(Callable.From(label.QueueFree));
	}

	// Карта вылетает из руки и тает.
	private void AnimateCardOut(CardView view)
	{
		if (view == null || !GodotObject.IsInstanceValid(view)) return;
		var globalPos = view.GlobalPosition;
		view.GetParent()?.RemoveChild(view);
		AddChild(view);
		view.GlobalPosition = globalPos;
		view.MouseFilter = MouseFilterEnum.Ignore;
		view.ZIndex = 50;

		var t = CreateTween().SetParallel(true);
		t.TweenProperty(view, "position:y", view.Position.Y - 60, 0.35f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(view, "modulate:a", 0.0f, 0.35f);
		t.TweenProperty(view, "scale", new Vector2(1.15f, 1.15f), 0.35f);
		t.Chain().TweenCallback(Callable.From(view.QueueFree));
	}

	private EnemyView FindEnemyView(EnemyData enemy)
	{
		foreach (Node child in _enemyArea.GetChildren())
			if (child is EnemyView ev && ev.Enemy == enemy) return ev;
		return null;
	}

	// === Окончание боя ===

	private void OnPlayerDead()
	{
		_combatOver = true;
		_endTurnButton.Disabled = true;
		Log("[color=#f44][b]Вы погибли. Все предметы потеряны.[/b][/color]");
		Log("[color=#f44]Нажмите «Новый бой» чтобы начать заново.[/color]");
	}

	private void OnAllEnemiesDead()
	{
		_combatOver = true;
		_endTurnButton.Disabled = true;
		Log("[color=#7f7][b]Локация зачищена![/b][/color]");
		Log("[color=#7f7]Лут (демо): +0 (для теста экстракции реализуем позже).[/color]");
	}

	// =====================================================================
	// Кнопки
	// =====================================================================

	private void OnLoadoutPressed()
	{
		GameData.Instance.CycleLoadout();
		StartNewCombat();
	}

	private void OnArmorPressed()
	{
		GameData.Instance.CycleArmor();
		StartNewCombat();
	}

	private void OnRestartPressed() => StartNewCombat();

	private void OnRerollStatsPressed()
	{
		GameData.Instance.Character.RerollStats();
		StartNewCombat();
	}

	private void OnLocationPressed()
	{
		GameData.Instance.CycleLocation();
		StartNewCombat();
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
	// UI
	// =====================================================================

	private void BuildUI()
	{
		var bg = new ColorRect { Color = UIStyle.BgDeep };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		bg.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(bg);

		// === Top bar ===
		var top = new HBoxContainer { Position = new Vector2(20, 12) };
		top.AddThemeConstantOverride("separation", 10);
		AddChild(top);

		_loadoutButton = new Button { Text = "⚔ Оружие" };
		UIStyle.StyleButton(_loadoutButton);
		_loadoutButton.Pressed += OnLoadoutPressed;
		top.AddChild(_loadoutButton);

		_armorButton = new Button { Text = "🛡 Броня" };
		UIStyle.StyleButton(_armorButton);
		_armorButton.Pressed += OnArmorPressed;
		top.AddChild(_armorButton);

		var locationButton = new Button { Text = "🗺 Локация" };
		UIStyle.StyleButton(locationButton);
		locationButton.Pressed += OnLocationPressed;
		top.AddChild(locationButton);

		_restartButton = new Button { Text = "↻ Новый бой" };
		UIStyle.StyleButton(_restartButton);
		_restartButton.Pressed += OnRestartPressed;
		top.AddChild(_restartButton);

		var rerollButton = new Button { Text = "🎲 Статы" };
		UIStyle.StyleButton(rerollButton);
		rerollButton.Pressed += OnRerollStatsPressed;
		top.AddChild(rerollButton);

		// === Player Panel ===
		var (pp, pv) = MakeTitledPanel("Игрок", new Vector2(20, 60), new Vector2(260, 320));
		AddChild(pp);

		_playerNameLabel = UIStyle.MakeLabel("", 16, UIStyle.TextPrimary);
		pv.AddChild(_playerNameLabel);

		_hpBar = MakeBar(UIStyle.HpFill, UIStyle.HpEmpty);
		pv.AddChild(_hpBar);
		_hpLabel = UIStyle.MakeLabel("", 13, UIStyle.TextPrimary);
		pv.AddChild(_hpLabel);

		_mpBar = MakeBar(UIStyle.MpFill, UIStyle.MpEmpty);
		pv.AddChild(_mpBar);
		_mpLabel = UIStyle.MakeLabel("", 13, UIStyle.TextPrimary);
		pv.AddChild(_mpLabel);

		_statsLabel = UIStyle.MakeLabel("", 12, UIStyle.TextPrimary);
		_statsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pv.AddChild(_statsLabel);

		_equipLabel = UIStyle.MakeLabel("", 11, UIStyle.TextSecondary);
		_equipLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pv.AddChild(_equipLabel);

		_blockLabel = UIStyle.MakeLabel("", 13, UIStyle.BlockCyan);
		pv.AddChild(_blockLabel);

		_buffsLabel = UIStyle.MakeLabel("", 11, UIStyle.BlockCyan);
		_buffsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pv.AddChild(_buffsLabel);

		// === Enemy Area (HBox, 5 слотов) ===
		var (ep, ev) = MakeTitledPanel("Враги", new Vector2(290, 60), new Vector2(670, 320));
		AddChild(ep);
		_enemyArea = new HBoxContainer();
		_enemyArea.AddThemeConstantOverride("separation", 8);
		_enemyArea.SizeFlagsVertical = SizeFlags.ExpandFill;
		_enemyArea.Alignment = BoxContainer.AlignmentMode.Center;
		ev.AddChild(_enemyArea);

		// === Log Panel ===
		var (lp, lv) = MakeTitledPanel("Лог боя", new Vector2(970, 60), new Vector2(290, 320));
		AddChild(lp);
		_logText = new RichTextLabel
		{
			BbcodeEnabled = true,
			ScrollFollowing = true,
			CustomMinimumSize = new Vector2(0, 250),
		};
		_logText.SizeFlagsVertical = SizeFlags.ExpandFill;
		lv.AddChild(_logText);

		// === Targeting hint banner ===
		_targetingBanner = new PanelContainer
		{
			Position = new Vector2(20, 380),
			Visible = false,
		};
		_targetingBanner.AddThemeStyleboxOverride("panel", UIStyle.BannerStyle(UIStyle.GoldBright));
		_targetingHint = UIStyle.MakeLabel("", 16, UIStyle.GoldBright);
		_targetingBanner.AddChild(_targetingHint);
		AddChild(_targetingBanner);

		// === Hand container ===
		_handContainer = new HBoxContainer
		{
			Position = new Vector2(20, 410),
			Size = new Vector2(1240, 240),
		};
		_handContainer.AddThemeConstantOverride("separation", 12);
		AddChild(_handContainer);

		// === Bottom row: счётчики колоды/сброса ===
		var deckMini = new PanelContainer { Position = new Vector2(20, 660) };
		deckMini.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		_deckCountLabel = UIStyle.MakeLabel("Колода: 0", 14, UIStyle.TextPrimary);
		deckMini.AddChild(_deckCountLabel);
		AddChild(deckMini);

		var discardMini = new PanelContainer { Position = new Vector2(170, 660) };
		discardMini.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		_discardCountLabel = UIStyle.MakeLabel("Сброс: 0", 14, UIStyle.TextPrimary);
		discardMini.AddChild(_discardCountLabel);
		AddChild(discardMini);

		_endTurnButton = new Button
		{
			Text = "Конец хода ▶",
			Position = new Vector2(1080, 655),
			Size = new Vector2(180, 50),
		};
		UIStyle.StyleButton(_endTurnButton, primary: true);
		_endTurnButton.Pressed += OnEndTurnPressed;
		AddChild(_endTurnButton);
	}

	private ProgressBar MakeBar(Color fill, Color empty)
	{
		var bar = new ProgressBar
		{
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 22),
		};
		UIStyle.StyleProgressBar(bar, fill, empty);
		return bar;
	}

	private (PanelContainer panel, VBoxContainer body) MakeTitledPanel(string title, Vector2 pos, Vector2 sz)
	{
		var panel = new PanelContainer
		{
			Position = pos,
			Size = sz,
			CustomMinimumSize = sz,
		};
		panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 8);
		panel.AddChild(v);

		v.AddChild(UIStyle.MakeSectionTitle(title));
		var sep = new HSeparator();
		var sepStyle = new StyleBoxFlat
		{
			BgColor = UIStyle.GoldDark,
			ContentMarginTop = 1, ContentMarginBottom = 1,
		};
		sep.AddThemeStyleboxOverride("separator", sepStyle);
		sep.AddThemeConstantOverride("separation", 4);
		v.AddChild(sep);

		return (panel, v);
	}

	// =====================================================================
	// Refresh
	// =====================================================================

	private void RefreshUI()
	{
		var p = GameData.Instance.Character;

		_playerNameLabel.Text = $"{p.CharacterName} ({p.Grade} grade, lvl {p.Level})";

		_hpBar.MaxValue = p.MaxHp();
		_hpBar.Value = p.CurrentHp;
		_hpLabel.Text = $"ХП: {p.CurrentHp} / {p.MaxHp()}";

		_mpBar.MaxValue = p.MaxMp();
		_mpBar.Value = p.CurrentMp;
		_mpLabel.Text = $"МП: {p.CurrentMp} / {p.MaxMp()}  (реген +{p.MpRegen()})";

		_statsLabel.Text = $"STR {p.Str}  INT {p.Int}  CON {p.Con}\nWIT {p.Wit}  MEN {p.Men}";
		_equipLabel.Text = $"⚔ {ItemsDB.DescribeWeapon(p.Weapon)}\n🛡 {ItemsDB.DescribeArmor(p.Armor)}";

		if (p.CurrentBlock > 0)
		{
			_blockLabel.Text = $"🛡 Блок: {p.CurrentBlock}";
			_blockLabel.AddThemeColorOverride("font_color", UIStyle.BlockCyan);
		}
		else
		{
			_blockLabel.Text = "🛡 Блок: —";
			_blockLabel.AddThemeColorOverride("font_color", UIStyle.TextDim);
		}
		_buffsLabel.Text = $"Эффекты: {DescribeEffects(p.Effects)}";

		_deckCountLabel.Text = $"Колода: {_deck.Count}";
		_discardCountLabel.Text = $"Сброс: {_discard.Count}";

		RefreshEnemyArea();
		RefreshHand();
	}

	private void RefreshEnemyArea()
	{
		bool targetingActive = _selectedHandIndex >= 0;

		// Если состав encounter изменился — пересобираем. Иначе обновляем
		// существующие view "на месте", чтобы не прерывать анимации (Flash).
		bool needRebuild = _enemyArea.GetChildCount() != _encounter.Count;
		if (!needRebuild)
		{
			for (int i = 0; i < _encounter.Count; i++)
			{
				if (_enemyArea.GetChild(i) is not EnemyView ev || ev.Enemy != _encounter[i])
				{
					needRebuild = true;
					break;
				}
			}
		}

		if (needRebuild)
		{
			foreach (Node child in _enemyArea.GetChildren())
			{
				_enemyArea.RemoveChild(child);
				child.QueueFree();
			}
			foreach (var enemy in _encounter)
			{
				var view = new EnemyView();
				_enemyArea.AddChild(view);
				view.SetEnemy(enemy, targetingActive && enemy.CurrentHp > 0);
				view.EnemyClicked += OnEnemyTargeted;
			}
		}
		else
		{
			for (int i = 0; i < _encounter.Count; i++)
			{
				if (_enemyArea.GetChild(i) is EnemyView ev)
					ev.SetEnemy(_encounter[i], targetingActive && _encounter[i].CurrentHp > 0);
			}
		}
	}

	private void RefreshHand()
	{
		foreach (Node child in _handContainer.GetChildren())
		{
			_handContainer.RemoveChild(child);
			child.QueueFree();
		}
		var p = GameData.Instance.Character;
		var firstAlive = _encounter.FirstOrDefault(e => e.CurrentHp > 0);
		for (int i = 0; i < _hand.Count; i++)
		{
			var cardId = _hand[i];
			var view = new CardView();
			_handContainer.AddChild(view);
			view.SetCard(cardId, p, firstAlive);
			var card = CardsDB.GetCard(cardId);
			view.SetPlayable(p.CurrentMp >= card.Cost && !_combatOver);
			view.SetSelected(_selectedHandIndex == i);
			view.CardClicked += OnCardClicked;
		}
	}

	private string DescribeEffects(List<StatusEffect> effects)
	{
		if (effects.Count == 0) return "—";
		var parts = new List<string>();
		foreach (var e in effects)
		{
			parts.Add(e.Type switch
			{
				"phys_taken_pct" => $"Пролом брони +{(int)e.Amount}% ({e.Remaining})",
				"magic_dmg_pct"  => $"Маг. фокус +{(int)e.Amount}% ({e.Remaining})",
				_                => $"{e.Id} ({e.Remaining})",
			});
		}
		return string.Join(", ", parts);
	}

	private string DeckSummary()
	{
		var counts = new Dictionary<string, int>();
		foreach (var id in _deck)
			counts[id] = counts.GetValueOrDefault(id, 0) + 1;
		var parts = new List<string>();
		foreach (var kv in counts)
			parts.Add($"{CardsDB.GetCard(kv.Key).Name}×{kv.Value}");
		return string.Join(", ", parts);
	}

	private void Log(string msg) => _logText.AppendText(msg + "\n");
	private void ClearLog() => _logText.Clear();

	private static void Shuffle<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = (int)(GD.Randi() % (uint)(i + 1));
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
