using Godot;
using System.Collections.Generic;
using System.Linq;

// Combat — построение и обновление UI боя.
public partial class Combat
{
	// === UI узлы ===
	private Button _loadoutButton, _armorButton, _restartButton, _endTurnButton;
	private Label _playerNameLabel, _hpLabel, _mpLabel, _statsLabel, _equipLabel, _blockLabel, _buffsLabel;
	private ProgressBar _hpBar, _mpBar;
	private HBoxContainer _enemyArea;
	private Label _deckCountLabel, _discardCountLabel;
	private HBoxContainer _handContainer;
	private RichTextLabel _logText;
	private Label _targetingHint;
	private PanelContainer _targetingBanner;

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

		_armorButton = new Button { Text = Lang.T("ui.combat.armor") };
		UIStyle.StyleButton(_armorButton);
		_armorButton.Pressed += OnArmorPressed;
		top.AddChild(_armorButton);

		var locationButton = new Button { Text = Lang.T("ui.combat.location") };
		UIStyle.StyleButton(locationButton);
		locationButton.Pressed += OnLocationPressed;
		top.AddChild(locationButton);

		_restartButton = new Button { Text = Lang.T("ui.combat.restart") };
		UIStyle.StyleButton(_restartButton);
		_restartButton.Pressed += OnRestartPressed;
		top.AddChild(_restartButton);

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

		_equipLabel = UIStyle.MakeLabel("", 11, UIStyle.TextSecondary);
		_equipLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pv.AddChild(_equipLabel);

		_blockLabel = UIStyle.MakeLabel("", 13, UIStyle.BlockCyan);
		pv.AddChild(_blockLabel);

		_buffsLabel = UIStyle.MakeLabel("", 11, UIStyle.BlockCyan);
		_buffsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pv.AddChild(_buffsLabel);

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

		_deckCountLabel.Text = $"{Lang.T("ui.combat.deck")}: {_deck.Count}";
		_discardCountLabel.Text = $"{Lang.T("ui.combat.discard")}: {_discard.Count}";

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
}
