using Godot;
using System.Collections.Generic;
using System.Linq;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Combat — построение и обновление UI боя.
public partial class Combat
{
	// === UI узлы ===
	private Button _exitButton, _endTurnButton;
	private Label _playerNameLabel, _hpLabel, _mpLabel, _statsLabel, _blockLabel, _buffsLabel;
	private ProgressBar _hpBar, _mpBar;
	private HBoxContainer _enemyArea;
	private Label _deckCountLabel, _discardCountLabel;
	private HBoxContainer _handContainer;
	private Label _targetingHint;
	private PanelContainer _targetingBanner;
	private HBoxContainer _potionsRow;
	private HBoxContainer _equipArmorRow, _equipJewelryRow;

	private void BuildUI()
	{
		var bg = new ColorRect { Color = UIStyle.BgDeep };
		UIStyle.FillParent(bg);
		bg.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(bg);

		// === Top bar ===
		var top = new HBoxContainer { Position = new Vector2(20, 12) };
		top.AddThemeConstantOverride("separation", 10);
		AddChild(top);

		_exitButton = new Button { Text = "🏳 Бежать" };
		UIStyle.StyleButton(_exitButton);
		_exitButton.Pressed += OnExitPressed;
		top.AddChild(_exitButton);

		var inventoryBtn = new Button { Text = "🎒 Инвентарь" };
		UIStyle.StyleButton(inventoryBtn);
		inventoryBtn.Pressed += OnInventoryPressed;
		top.AddChild(inventoryBtn);

		var logBtn = new Button { Text = "📜 Лог" };
		UIStyle.StyleButton(logBtn);
		logBtn.Pressed += OnLogPressed;
		top.AddChild(logBtn);

		var resetCharBtn = new Button { Text = "👤 Новый персонаж" };
		UIStyle.StyleButton(resetCharBtn);
		resetCharBtn.Pressed += OnResetCharacterPressed;
		top.AddChild(resetCharBtn);

		// === Player Panel ===
		// Расширен до 340 (было 260) — лог-панель убрана, освободилось место.
		var (pp, pv) = MakeTitledPanel(Lang.T("ui.combat.player_panel"), new Vector2(20, 60), new Vector2(340, 320));
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

		// Экипировка иконками: первый ряд — броня (5), второй ряд — бижутерия (3).
		_equipArmorRow = new HBoxContainer();
		_equipArmorRow.AddThemeConstantOverride("separation", 4);
		_equipArmorRow.Alignment = BoxContainer.AlignmentMode.Center;
		pv.AddChild(_equipArmorRow);

		_equipJewelryRow = new HBoxContainer();
		_equipJewelryRow.AddThemeConstantOverride("separation", 4);
		_equipJewelryRow.Alignment = BoxContainer.AlignmentMode.Center;
		pv.AddChild(_equipJewelryRow);

		_blockLabel = UIStyle.MakeLabel("", 13, UIStyle.BlockCyan);
		pv.AddChild(_blockLabel);

		_buffsLabel = UIStyle.MakeLabel("", 11, UIStyle.BlockCyan);
		_buffsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pv.AddChild(_buffsLabel);

		// === Зелья (динамически отображаются те, что есть в инвентаре) ===
		_potionsRow = new HBoxContainer();
		_potionsRow.AddThemeConstantOverride("separation", 6);
		pv.AddChild(_potionsRow);

		// === Enemy Area ===
		// Расширена до 890 (было 670) — лог-панель убрана с экрана, теперь
		// открывается отдельным оверлеем по кнопке 📜.
		var (ep, ev) = MakeTitledPanel(Lang.T("ui.combat.enemies_panel"), new Vector2(370, 60), new Vector2(890, 320));
		AddChild(ep);
		_enemyArea = new HBoxContainer();
		_enemyArea.AddThemeConstantOverride("separation", 12);
		_enemyArea.SizeFlagsVertical = SizeFlags.ExpandFill;
		_enemyArea.Alignment = BoxContainer.AlignmentMode.Center;
		ev.AddChild(_enemyArea);

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
		_deckCountLabel = UIStyle.MakeLabel("", 14, UIStyle.TextPrimary);
		deckMini.AddChild(_deckCountLabel);
		AddChild(deckMini);

		var discardMini = new PanelContainer { Position = new Vector2(170, 660) };
		discardMini.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		_discardCountLabel = UIStyle.MakeLabel("", 14, UIStyle.TextPrimary);
		discardMini.AddChild(_discardCountLabel);
		AddChild(discardMini);

		_endTurnButton = new Button
		{
			Text = Lang.T("ui.combat.end_turn"),
			Position = new Vector2(1080, 655),
			Size = new Vector2(180, 50),
		};
		UIStyle.StyleButton(_endTurnButton, primary: true);
		// Один handler с переключаемой ролью: во время боя — "Конец хода",
		// после победы — "Переход на карту". См. OnTurnOrAdvancePressed.
		_endTurnButton.Pressed += OnTurnOrAdvancePressed;
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

		_playerNameLabel.Text = $"{p.CharacterName} ({p.Grade}-грейд, ур. {p.Level})";

		_hpBar.MaxValue = p.MaxHp();
		_hpBar.Value = p.CurrentHp;
		_hpLabel.Text = $"ХП: {p.CurrentHp} / {p.MaxHp()}";

		_mpBar.MaxValue = p.MaxMp();
		_mpBar.Value = p.CurrentMp;
		_mpLabel.Text = $"МП: {p.CurrentMp} / {p.MaxMp()}  (реген +{p.MpRegen()})";

		string critInfo;
		if (p.Weapon != null)
		{
			int eff = p.EffectiveCritEveryN();
			critInfo = $"🎯 Крит каждые {eff} ат. ({p.AttacksSinceLastCrit}/{eff}) × {p.CritMultiplier():F2}";
		}
		else
		{
			critInfo = "🎯 Крит: —";
		}
		_statsLabel.Text =
			$"STR {p.Str}  INT {p.Int}  CON {p.Con}\n" +
			$"WIT {p.Wit}  MEN {p.Men}  DEX {p.Dex}\n" +
			critInfo;
		RefreshEquipSlots();

		string blockText = p.CurrentBlock > 0
			? $"🛡 Блок: {p.CurrentBlock}"
			: "🛡 Блок: —";
		// Кровотечение игрока (от run-эффекта bleed_all_per_turn). Тикает
		// в начале EndTurn — игнорирует броню и блок.
		if (p.BleedStack > 0)
			blockText += $"    🩸 {p.BleedStack}";
		_blockLabel.Text = blockText;
		_blockLabel.AddThemeColorOverride("font_color",
			p.CurrentBlock > 0 ? UIStyle.BlockCyan : UIStyle.TextDim);
		_buffsLabel.Text = $"Эффекты: {DescribeEffects(p.Effects)}";

		_deckCountLabel.Text = $"{Lang.T("ui.combat.deck")}: {_state?.Deck.Count ?? 0}";
		_discardCountLabel.Text = $"{Lang.T("ui.combat.discard")}: {_state?.Discard.Count ?? 0}";

		RefreshPotionsRow();
		RefreshExitButton();

		RefreshEnemyArea();
		RefreshHand();
	}

	private void RefreshExitButton()
	{
		bool over = _state != null && _state.CombatOver;
		bool victory = _state != null && _state.Victory;
		if (!over)
			_exitButton.Text = "🏳 Бежать";
		else if (victory)
			_exitButton.Text = "✅ На карту →";
		else
			_exitButton.Text = "💀 Выйти из подземелья";
	}

	// Динамически перестраиваем кнопки зелий: показываем все типы, что
	// сейчас лежат в инвентаре. Новый тип из лута → кнопка появляется сама.
	private void RefreshPotionsRow()
	{
		foreach (Node c in _potionsRow.GetChildren())
		{
			_potionsRow.RemoveChild(c);
			c.QueueFree();
		}
		var p = GameData.Instance.Character;
		if (p == null) return;
		foreach (var potion in PotionsDB.All())
		{
			int count = p.Inventory.CountOf(potion.Id);
			if (count <= 0) continue;
			var btn = new Button { Text = $"{potion.Icon}×{count}" };
			UIStyle.StyleButton(btn);
			btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			btn.Disabled = _state != null && _state.CombatOver;
			btn.TooltipText = $"{potion.Name}\n{potion.Description}";
			string id = potion.Id;
			btn.Pressed += () => OnUsePotion(id);
			_potionsRow.AddChild(btn);
		}
	}

	private void RefreshEquipSlots()
	{
		ClearRow(_equipArmorRow);
		ClearRow(_equipJewelryRow);
		var p = GameData.Instance.Character;
		if (p == null) return;
		AddEquipSlotIcon(_equipArmorRow,   "⚔",  p.Weapon?.Name);
		AddEquipSlotIcon(_equipArmorRow,   "👕", p.Chest?.Name);
		AddEquipSlotIcon(_equipArmorRow,   "⛑",  p.Helmet?.Name);
		AddEquipSlotIcon(_equipArmorRow,   "🧤", p.Gloves?.Name);
		AddEquipSlotIcon(_equipArmorRow,   "👢", p.Boots?.Name);
		AddEquipSlotIcon(_equipJewelryRow, "📿", p.Amulet?.Name);
		AddEquipSlotIcon(_equipJewelryRow, "💍", p.Ring1?.Name);
		AddEquipSlotIcon(_equipJewelryRow, "💍", p.Ring2?.Name);
	}

	private static void ClearRow(HBoxContainer row)
	{
		foreach (Node c in row.GetChildren())
		{
			row.RemoveChild(c);
			c.QueueFree();
		}
	}

	private void AddEquipSlotIcon(HBoxContainer row, string emoji, string itemName)
	{
		var slot = new PanelContainer { CustomMinimumSize = new Vector2(40, 40) };
		var sb = UIStyle.MiniPanelStyle();
		bool empty = string.IsNullOrEmpty(itemName);
		sb.BorderWidthLeft = 2; sb.BorderWidthRight = 2;
		sb.BorderWidthTop = 2; sb.BorderWidthBottom = 2;
		sb.BorderColor = empty ? UIStyle.GoldDark * 0.5f : UIStyle.GoldMid;
		sb.BgColor = empty ? new Color(0.10f, 0.09f, 0.13f) : UIStyle.PanelBgLight;
		sb.ContentMarginLeft = 2; sb.ContentMarginRight = 2;
		sb.ContentMarginTop = 2; sb.ContentMarginBottom = 2;
		slot.AddThemeStyleboxOverride("panel", sb);
		slot.TooltipText = empty ? "(пусто)" : itemName;
		slot.MouseFilter = MouseFilterEnum.Stop;

		var label = UIStyle.MakeLabel(emoji, 20, UIStyle.GoldBright);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		if (empty) label.Modulate = new Color(0.5f, 0.5f, 0.5f);
		slot.AddChild(label);

		row.AddChild(slot);
	}

	private void RefreshEnemyArea()
	{
		if (_state == null) return;
		bool targetingActive = _selectedHandIndex >= 0;
		var enemies = _state.Enemies;

		// Если состав encounter изменился — пересобираем. Иначе обновляем
		// существующие view "на месте", чтобы не прерывать анимации (Flash).
		bool needRebuild = _enemyArea.GetChildCount() != enemies.Count;
		if (!needRebuild)
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				if (_enemyArea.GetChild(i) is not EnemyView ev || ev.Enemy != enemies[i])
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
			foreach (var enemy in enemies)
			{
				var view = new EnemyView();
				_enemyArea.AddChild(view);
				view.SetEnemy(enemy, targetingActive && enemy.CurrentHp > 0, _state.Player);
				view.EnemyClicked += OnEnemyTargeted;
			}
		}
		else
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				if (_enemyArea.GetChild(i) is EnemyView ev)
					ev.SetEnemy(enemies[i], targetingActive && enemies[i].CurrentHp > 0, _state.Player);
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
		if (_state == null) return;
		var p = GameData.Instance.Character;
		var firstAlive = _state.Enemies.FirstOrDefault(e => e.CurrentHp > 0);
		bool over = _state.CombatOver;
		int chain = _state.SpellsCastThisTurn;
		for (int i = 0; i < _state.Hand.Count; i++)
		{
			var cardId = _state.Hand[i];
			var view = new CardView();
			_handContainer.AddChild(view);
			view.SetCard(cardId, p, firstAlive, _state.Hand, chain);
			var card = CardsDB.GetCard(cardId);
			int actualCost = CardsDB.ComputeManaCost(card, p, chain);
			view.SetPlayable(p.CurrentMp >= actualCost && !over);
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
				"phys_dmg_pct"   => $"Ярость +{(int)e.Amount}% ({e.Remaining})",
				_                => $"{e.Id} ({e.Remaining})",
			});
		}
		return string.Join(", ", parts);
	}

	private string DeckSummary()
	{
		var counts = new Dictionary<string, int>();
		if (_state != null)
			foreach (var id in _state.Deck)
				counts[id] = counts.GetValueOrDefault(id, 0) + 1;
		var parts = new List<string>();
		foreach (var kv in counts)
			parts.Add($"{CardsDB.GetCard(kv.Key).Name}×{kv.Value}");
		return string.Join(", ", parts);
	}
}
