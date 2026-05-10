using Godot;
using System.Collections.Generic;
using System.Linq;

// Combat — построение и обновление UI боя.
public partial class Combat
{
	// === UI узлы ===
	private Button _loadoutButton, _chestButton, _restartButton, _endTurnButton;
	private Label _playerNameLabel, _hpLabel, _mpLabel, _statsLabel, _blockLabel, _buffsLabel;
	private ProgressBar _hpBar, _mpBar;
	private HBoxContainer _enemyArea;
	private Label _deckCountLabel, _discardCountLabel;
	private HBoxContainer _handContainer;
	private RichTextLabel _logText;
	private Label _targetingHint;
	private PanelContainer _targetingBanner;
	private Button _potionHpBtn, _potionMpBtn;
	private HBoxContainer _equipArmorRow, _equipJewelryRow;

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

		_loadoutButton = new Button { Text = Lang.T("ui.combat.weapon") };
		UIStyle.StyleButton(_loadoutButton);
		_loadoutButton.Pressed += OnLoadoutPressed;
		top.AddChild(_loadoutButton);

		_chestButton = new Button { Text = "🛡 Нагрудник" };
		UIStyle.StyleButton(_chestButton);
		_chestButton.Pressed += OnChestPressed;
		top.AddChild(_chestButton);

		var locationButton = new Button { Text = Lang.T("ui.combat.location") };
		UIStyle.StyleButton(locationButton);
		locationButton.Pressed += OnLocationPressed;
		top.AddChild(locationButton);

		_restartButton = new Button { Text = Lang.T("ui.combat.restart") };
		UIStyle.StyleButton(_restartButton);
		_restartButton.Pressed += OnRestartPressed;
		top.AddChild(_restartButton);

		var inventoryBtn = new Button { Text = "🎒 Инвентарь" };
		UIStyle.StyleButton(inventoryBtn);
		inventoryBtn.Pressed += OnInventoryPressed;
		top.AddChild(inventoryBtn);

		var resetCharBtn = new Button { Text = "👤 Новый персонаж" };
		UIStyle.StyleButton(resetCharBtn);
		resetCharBtn.Pressed += OnResetCharacterPressed;
		top.AddChild(resetCharBtn);

		// === Player Panel ===
		var (pp, pv) = MakeTitledPanel(Lang.T("ui.combat.player_panel"), new Vector2(20, 60), new Vector2(260, 320));
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

		// === Зелья (используются в течение боя) ===
		var potionsRow = new HBoxContainer();
		potionsRow.AddThemeConstantOverride("separation", 6);
		pv.AddChild(potionsRow);

		_potionHpBtn = new Button();
		UIStyle.StyleButton(_potionHpBtn);
		_potionHpBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_potionHpBtn.Pressed += () => OnUsePotion("potion_hp_small");
		potionsRow.AddChild(_potionHpBtn);

		_potionMpBtn = new Button();
		UIStyle.StyleButton(_potionMpBtn);
		_potionMpBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_potionMpBtn.Pressed += () => OnUsePotion("potion_mp_small");
		potionsRow.AddChild(_potionMpBtn);

		// === Enemy Area ===
		var (ep, ev) = MakeTitledPanel(Lang.T("ui.combat.enemies_panel"), new Vector2(290, 60), new Vector2(670, 320));
		AddChild(ep);
		_enemyArea = new HBoxContainer();
		_enemyArea.AddThemeConstantOverride("separation", 8);
		_enemyArea.SizeFlagsVertical = SizeFlags.ExpandFill;
		_enemyArea.Alignment = BoxContainer.AlignmentMode.Center;
		ev.AddChild(_enemyArea);

		// === Log Panel ===
		var (lp, lv) = MakeTitledPanel(Lang.T("ui.combat.log_panel"), new Vector2(970, 60), new Vector2(290, 320));
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

		_deckCountLabel.Text = $"{Lang.T("ui.combat.deck")}: {_deck.Count}";
		_discardCountLabel.Text = $"{Lang.T("ui.combat.discard")}: {_discard.Count}";

		RefreshPotionButton(_potionHpBtn, "potion_hp_small");
		RefreshPotionButton(_potionMpBtn, "potion_mp_small");

		RefreshEnemyArea();
		RefreshHand();
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
		var slot = new PanelContainer { CustomMinimumSize = new Vector2(44, 44) };
		var sb = UIStyle.MiniPanelStyle();
		bool empty = string.IsNullOrEmpty(itemName);
		sb.BorderWidthLeft = 2; sb.BorderWidthRight = 2;
		sb.BorderWidthTop = 2; sb.BorderWidthBottom = 2;
		sb.BorderColor = empty ? UIStyle.GoldDark * 0.5f : UIStyle.GoldMid;
		sb.BgColor = empty ? new Color(0.10f, 0.09f, 0.13f) : UIStyle.PanelBgLight;
		sb.ContentMarginLeft = 4; sb.ContentMarginRight = 4;
		sb.ContentMarginTop = 4; sb.ContentMarginBottom = 4;
		slot.AddThemeStyleboxOverride("panel", sb);
		slot.TooltipText = empty ? "(пусто)" : itemName;
		slot.MouseFilter = MouseFilterEnum.Stop;

		var label = UIStyle.MakeLabel(emoji, 22, UIStyle.GoldBright);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		if (empty) label.Modulate = new Color(0.5f, 0.5f, 0.5f);
		slot.AddChild(label);

		row.AddChild(slot);
	}

	private void RefreshPotionButton(Button btn, string itemId)
	{
		var p = GameData.Instance.Character;
		var potion = PotionsDB.Get(itemId);
		if (p == null || potion == null)
		{
			btn.Text = "—";
			btn.Disabled = true;
			return;
		}
		int count = p.Inventory.CountOf(itemId);
		btn.Text = $"{potion.Icon} ×{count}";
		btn.Disabled = count <= 0 || _combatOver;
		btn.TooltipText = $"{potion.Name}\n{potion.Description}";
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
}
