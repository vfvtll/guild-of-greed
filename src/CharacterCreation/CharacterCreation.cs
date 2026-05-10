using Godot;
using System;

// Сцена создания персонажа.
// Логика:
//   1. Кидаем 5 базовых статов случайно в диапазоне 35..45
//   2. Игрок имя вводит и распределяет 10 очков на любые статы
//   3. Кнопка "Начать игру" доступна только когда все 10 очков потрачены
//   4. По конфирму — испускаем event Confirmed(CharacterData), Main сохраняет
//
// Подписка из C# (event Action<T>, не Godot signal): CharacterData — POCO,
// не GodotObject, через Variant передавать нельзя.
public partial class CharacterCreation : Control
{
	public event Action<CharacterData> Confirmed;

	private const int StatCount = 6;
	private const int PointsTotal = 10;

	private static readonly string[] StatLabels = { "STR", "INT", "CON", "WIT", "MEN", "DEX" };
	private static readonly string[] StatDescriptions =
	{
		"Физическая атака",
		"Магическая атака",
		"Здоровье (ХП)",
		"Реген маны",
		"Максимум маны",
		"Блок, шанс и сила крита",
	};

	private readonly int[] _baseStats = new int[StatCount];
	private readonly int[] _addedPoints = new int[StatCount];
	private int _pointsRemaining = PointsTotal;

	private LineEdit _nameInput;
	private Label _pointsLabel;
	private Button _confirmButton;
	private readonly Label[] _baseLabels = new Label[StatCount];
	private readonly Label[] _addedLabels = new Label[StatCount];
	private readonly Label[] _totalLabels = new Label[StatCount];

	// Превью производных характеристик (HP/MP/реген/крит) — обновляется на каждое +/-.
	private Label _previewHp, _previewMp, _previewRegen, _previewCrit, _previewBlock, _previewBonus;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUI();
		Reroll();
	}

	private void Reroll()
	{
		for (int i = 0; i < StatCount; i++)
		{
			_baseStats[i] = Rng.Range(35, 46);
			_addedPoints[i] = 0;
		}
		_pointsRemaining = PointsTotal;
		Refresh();
	}

	private int Total(int i) => _baseStats[i] + _addedPoints[i];

	private void OnAdd(int i)
	{
		if (_pointsRemaining <= 0) return;
		_addedPoints[i]++;
		_pointsRemaining--;
		Refresh();
	}

	private void OnRemove(int i)
	{
		if (_addedPoints[i] <= 0) return;
		_addedPoints[i]--;
		_pointsRemaining++;
		Refresh();
	}

	private void OnConfirm()
	{
		if (_pointsRemaining > 0) return;
		var ch = new CharacterData
		{
			Str = Total(0),
			Int = Total(1),
			Con = Total(2),
			Wit = Total(3),
			Men = Total(4),
			Dex = Total(5),
		};
		var name = _nameInput.Text?.Trim();
		ch.CharacterName = string.IsNullOrEmpty(name) ? "Авантюрист" : name;
		Confirmed?.Invoke(ch);
	}

	private void Refresh()
	{
		for (int i = 0; i < StatCount; i++)
		{
			_baseLabels[i].Text = _baseStats[i].ToString();
			_addedLabels[i].Text = _addedPoints[i] > 0 ? $"+{_addedPoints[i]}" : "—";
			_addedLabels[i].AddThemeColorOverride("font_color",
				_addedPoints[i] > 0 ? UIStyle.HealGreen : UIStyle.TextDim);
			_totalLabels[i].Text = Total(i).ToString();
		}
		_pointsLabel.Text = $"Осталось очков: {_pointsRemaining}";
		_pointsLabel.AddThemeColorOverride("font_color",
			_pointsRemaining == 0 ? UIStyle.HealGreen : UIStyle.WarnAmber);
		_confirmButton.Disabled = _pointsRemaining > 0;

		RefreshPreview();
	}

	// Обновляет превью производных параметров с учётом текущего распределения.
	// Без оружия и брони — чистая база, чтобы игрок видел что даст голый персонаж.
	private void RefreshPreview()
	{
		int str = Total(0), inT = Total(1), con = Total(2);
		int wit = Total(3), men = Total(4), dex = Total(5);
		int hp = 40 + con * 2;
		int mp = 30 + men;
		int regen = wit / 3;
		float critMult = 1.5f + dex / 100f;
		int blockBonus = dex / 4;
		_previewHp.Text     = $"❤ ХП: {hp}";
		_previewMp.Text     = $"💧 МП: {mp}";
		_previewRegen.Text  = $"🔄 Реген: {regen}/ход";
		_previewCrit.Text   = $"🎯 Крит ×{critMult:F2}";
		_previewBlock.Text  = $"🛡 +{blockBonus} к блоку";
		_previewBonus.Text  = $"⚔ +{str / 3}  🔮 +{inT / 3} к урону";
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

		var panel = new PanelContainer
		{
			Position = new Vector2(290, 20),
			Size = new Vector2(700, 680),
			CustomMinimumSize = new Vector2(700, 680),
		};
		panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 14);
		panel.AddChild(v);

		var title = UIStyle.MakeLabel("⚜ Создание персонажа", 24, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(title);

		var hint = UIStyle.MakeLabel(
			"Базовые статы выкатились случайно (35..45). Распределите 10 очков\n" +
			"по своему усмотрению — никаких ограничений по максимуму нет.",
			13, UIStyle.TextSecondary);
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(hint);

		// === Поле имени ===
		var nameRow = new HBoxContainer();
		nameRow.AddThemeConstantOverride("separation", 10);
		v.AddChild(nameRow);
		nameRow.AddChild(UIStyle.MakeLabel("Имя:", 14, UIStyle.TextPrimary));
		_nameInput = new LineEdit
		{
			Text = "Авантюрист",
			PlaceholderText = "Введите имя",
		};
		_nameInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_nameInput.AddThemeColorOverride("font_color", UIStyle.TextPrimary);
		nameRow.AddChild(_nameInput);

		v.AddChild(new HSeparator());

		// === Сетка статов ===
		var grid = new GridContainer { Columns = 5 };
		grid.AddThemeConstantOverride("h_separation", 14);
		grid.AddThemeConstantOverride("v_separation", 10);
		v.AddChild(grid);

		// Заголовки
		grid.AddChild(UIStyle.MakeLabel("Стат", 13, UIStyle.GoldMid));
		grid.AddChild(UIStyle.MakeLabel("База", 13, UIStyle.GoldMid));
		grid.AddChild(UIStyle.MakeLabel("Бонус", 13, UIStyle.GoldMid));
		grid.AddChild(UIStyle.MakeLabel("Итого", 13, UIStyle.GoldMid));
		grid.AddChild(new Control());

		for (int i = 0; i < StatCount; i++)
		{
			int idx = i;

			var nameLabel = UIStyle.MakeLabel($"{StatLabels[i]} — {StatDescriptions[i]}", 13, UIStyle.TextPrimary);
			nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			grid.AddChild(nameLabel);

			_baseLabels[i] = UIStyle.MakeLabel("0", 14, UIStyle.TextDim);
			_baseLabels[i].HorizontalAlignment = HorizontalAlignment.Center;
			_baseLabels[i].CustomMinimumSize = new Vector2(40, 0);
			grid.AddChild(_baseLabels[i]);

			_addedLabels[i] = UIStyle.MakeLabel("—", 14, UIStyle.TextDim);
			_addedLabels[i].HorizontalAlignment = HorizontalAlignment.Center;
			_addedLabels[i].CustomMinimumSize = new Vector2(50, 0);
			grid.AddChild(_addedLabels[i]);

			_totalLabels[i] = UIStyle.MakeLabel("0", 18, UIStyle.GoldBright);
			_totalLabels[i].HorizontalAlignment = HorizontalAlignment.Center;
			_totalLabels[i].CustomMinimumSize = new Vector2(50, 0);
			grid.AddChild(_totalLabels[i]);

			var btnRow = new HBoxContainer();
			btnRow.AddThemeConstantOverride("separation", 4);

			var minusBtn = new Button { Text = "−" };
			UIStyle.StyleButton(minusBtn);
			minusBtn.CustomMinimumSize = new Vector2(36, 0);
			minusBtn.Pressed += () => OnRemove(idx);
			btnRow.AddChild(minusBtn);

			var plusBtn = new Button { Text = "+" };
			UIStyle.StyleButton(plusBtn);
			plusBtn.CustomMinimumSize = new Vector2(36, 0);
			plusBtn.Pressed += () => OnAdd(idx);
			btnRow.AddChild(plusBtn);
			grid.AddChild(btnRow);
		}

		v.AddChild(new HSeparator());

		_pointsLabel = UIStyle.MakeLabel("", 18, UIStyle.WarnAmber);
		_pointsLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_pointsLabel);

		// === Превью производных параметров ===
		var previewPanel = new PanelContainer();
		previewPanel.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		v.AddChild(previewPanel);

		var previewGrid = new GridContainer { Columns = 3 };
		previewGrid.AddThemeConstantOverride("h_separation", 18);
		previewGrid.AddThemeConstantOverride("v_separation", 4);
		previewPanel.AddChild(previewGrid);

		_previewHp     = UIStyle.MakeLabel("", 13, UIStyle.HealGreen);
		_previewMp     = UIStyle.MakeLabel("", 13, UIStyle.MpFill);
		_previewRegen  = UIStyle.MakeLabel("", 13, UIStyle.MpFill);
		_previewCrit   = UIStyle.MakeLabel("", 13, UIStyle.WarnAmber);
		_previewBlock  = UIStyle.MakeLabel("", 13, UIStyle.BlockCyan);
		_previewBonus  = UIStyle.MakeLabel("", 13, UIStyle.GoldBright);
		previewGrid.AddChild(_previewHp);
		previewGrid.AddChild(_previewMp);
		previewGrid.AddChild(_previewRegen);
		previewGrid.AddChild(_previewCrit);
		previewGrid.AddChild(_previewBlock);
		previewGrid.AddChild(_previewBonus);

		// === Нижние кнопки ===
		var btnBar = new HBoxContainer();
		btnBar.AddThemeConstantOverride("separation", 18);
		btnBar.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(btnBar);

		var rerollBtn = new Button { Text = "🎲 Перекинуть базу" };
		UIStyle.StyleButton(rerollBtn);
		rerollBtn.Pressed += Reroll;
		btnBar.AddChild(rerollBtn);

		_confirmButton = new Button { Text = "▶ Начать игру" };
		UIStyle.StyleButton(_confirmButton, primary: true);
		_confirmButton.Pressed += OnConfirm;
		btnBar.AddChild(_confirmButton);
	}
}
