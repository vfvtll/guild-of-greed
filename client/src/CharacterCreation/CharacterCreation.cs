using Godot;
using System;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Сцена создания персонажа.
// Логика:
//   1. Кидаем 6 базовых статов случайно в диапазоне 35..45
//   2. Игрок вводит имя (валидируется real-time через CharacterNameValidator)
//      и распределяет 10 очков на любые статы
//   3. Кнопка "Начать игру" доступна когда: 10 очков потрачены, имя валидно,
//      нет уже летящего CreateCharacter-запроса (_creating=false)
//   4. По клику — ConfirmDialog. После подтверждения — event Confirmed(CharacterData)
//      → Main отправляет CreateCharacter на сервер. На время запроса форма
//      блокируется (SetCreating(true)).
//   5. Если сервер вернул ошибку — Main зовёт ShowServerError, форма
//      разблокируется, под кнопкой показывается сообщение.
//
// Подписка из C# (event Action<T>, не Godot signal): CharacterData — POCO,
// не GodotObject, через Variant передавать нельзя.
public partial class CharacterCreation : Control
{
	public event Action<CharacterData> Confirmed;
	public event Action BackRequested;

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
	private bool _creating;          // true пока летит запрос на сервер
	private CharNameStatus _nameStatus = CharNameStatus.TooShort;

	private LineEdit _nameInput;
	private Label _nameHintLabel;    // real-time валидация имени
	private Label _pointsLabel;
	private Label _errorLabel;       // ошибки сервера
	private Button _confirmButton;
	private Button _backButton;
	private readonly Label[] _baseLabels = new Label[StatCount];
	private readonly Label[] _addedLabels = new Label[StatCount];
	private readonly Label[] _totalLabels = new Label[StatCount];

	// Превью производных характеристик (HP/MP/реген/крит) — обновляется на каждое +/-.
	private Label _previewHp, _previewMp, _previewRegen, _previewCrit, _previewBlock, _previewBonus;

	public override void _Ready()
	{
		UIStyle.FillParent(this);
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
		if (_creating) return;
		if (_pointsRemaining > 0) return;
		if (_nameStatus != CharNameStatus.Ok) return;

		// Показываем confirm-диалог перед отправкой на сервер: имя в кнопке
		// «Начать игру» необратимо — создаст персонажа в БД, потратит слот.
		ShowConfirmDialog();
	}

	private void ShowConfirmDialog()
	{
		var name = _nameInput.Text.Trim();
		var dialog = new ConfirmDialog
		{
			Title = "Создать персонажа?",
			Body =
				$"Имя: {name}\n\n" +
				$"STR {Total(0)}  INT {Total(1)}  CON {Total(2)}\n" +
				$"WIT {Total(3)}  MEN {Total(4)}  DEX {Total(5)}\n\n" +
				"Статы можно будет перераспределить за плату в Гильдии.",
			ConfirmText = "Создать",
			CancelText = "Отмена",
		};
		dialog.Confirmed += DoCreate;
		AddChild(dialog);
	}

	private void DoCreate()
	{
		var ch = new CharacterData
		{
			Str = Total(0),
			Int = Total(1),
			Con = Total(2),
			Wit = Total(3),
			Men = Total(4),
			Dex = Total(5),
			BaseStr = _baseStats[0],
			BaseInt = _baseStats[1],
			BaseCon = _baseStats[2],
			BaseWit = _baseStats[3],
			BaseMen = _baseStats[4],
			BaseDex = _baseStats[5],
			CharacterName = _nameInput.Text.Trim(),
		};
		SetCreating(true);
		Confirmed?.Invoke(ch);
	}

	// Вызывается из Main, когда сервер вернул ошибку — например, имя уже
	// занято или не прошло profanity. Возвращает форму в редактируемое
	// состояние и показывает сообщение под кнопкой.
	public void ShowServerError(string errorCode)
	{
		SetCreating(false);
		_errorLabel.Text = ServerErrorMessage(errorCode);
		_errorLabel.Visible = true;
	}

	private void SetCreating(bool creating)
	{
		_creating = creating;
		_confirmButton.Disabled = creating || _pointsRemaining > 0 || _nameStatus != CharNameStatus.Ok;
		_confirmButton.Text = creating ? "Создание..." : "▶ Начать игру";
		_backButton.Disabled = creating;
		_nameInput.Editable = !creating;
		if (creating) _errorLabel.Visible = false;
	}

	private static string ServerErrorMessage(string code) => code switch
	{
		"slots_full"     => "Слоты персонажей заняты — удалите одного на экране выбора.",
		"invalid_stats"  => "Статы вне допустимого диапазона. Попробуйте перекинуть базу.",
		"name_too_short" => $"Имя слишком короткое (минимум {CharacterNameValidator.MinLen}).",
		"name_too_long"  => $"Имя слишком длинное (максимум {CharacterNameValidator.MaxLen}).",
		"name_bad_chars" => "Имя содержит недопустимые символы.",
		"name_reserved"  => "Это имя зарезервировано системой.",
		"name_profanity" => "Имя содержит запрещённые слова.",
		_                => $"Ошибка сервера: {code}",
	};

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
		_confirmButton.Disabled = _creating || _pointsRemaining > 0 || _nameStatus != CharNameStatus.Ok;

		RefreshPreview();
	}

	// Real-time валидация имени. Дёргается при каждом изменении LineEdit.
	private void OnNameChanged(string newText)
	{
		_nameStatus = CharacterNameValidator.Validate(newText);
		if (_nameStatus == CharNameStatus.Ok)
		{
			_nameHintLabel.Text = "✓ Имя свободно для использования";
			_nameHintLabel.AddThemeColorOverride("font_color", UIStyle.HealGreen);
		}
		else
		{
			_nameHintLabel.Text = CharacterNameValidator.Hint(_nameStatus);
			_nameHintLabel.AddThemeColorOverride("font_color", UIStyle.DangerRed);
		}
		_confirmButton.Disabled = _creating || _pointsRemaining > 0 || _nameStatus != CharNameStatus.Ok;
		// Сервер мог дать ошибку — спрячем её, как только пользователь начал
		// править имя, чтобы старый текст не путал.
		if (_errorLabel != null) _errorLabel.Visible = false;
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
		UIStyle.FillParent(bg);
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
			Text = "",
			PlaceholderText = $"От {CharacterNameValidator.MinLen} до {CharacterNameValidator.MaxLen} символов",
			MaxLength = CharacterNameValidator.MaxLen,
		};
		_nameInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_nameInput.AddThemeColorOverride("font_color", UIStyle.TextPrimary);
		_nameInput.TextChanged += OnNameChanged;
		nameRow.AddChild(_nameInput);

		// Подсказка под полем имени — обновляется в OnNameChanged.
		_nameHintLabel = UIStyle.MakeLabel(
			CharacterNameValidator.Hint(CharNameStatus.TooShort), 12, UIStyle.DangerRed);
		v.AddChild(_nameHintLabel);

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

		// Сюда выводим ошибки от сервера (slots_full, name_reserved, ...).
		_errorLabel = UIStyle.MakeLabel("", 13, UIStyle.DangerRed);
		_errorLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_errorLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_errorLabel.Visible = false;
		v.AddChild(_errorLabel);

		// === Нижние кнопки ===
		var btnBar = new HBoxContainer();
		btnBar.AddThemeConstantOverride("separation", 18);
		btnBar.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(btnBar);

		_backButton = new Button { Text = "← Назад" };
		UIStyle.StyleButton(_backButton);
		_backButton.Pressed += () => BackRequested?.Invoke();
		btnBar.AddChild(_backButton);

		var rerollBtn = new Button { Text = "🎲 Перекинуть базу" };
		UIStyle.StyleButton(rerollBtn);
		rerollBtn.Pressed += Reroll;
		btnBar.AddChild(rerollBtn);

		_confirmButton = new Button { Text = "▶ Начать игру" };
		UIStyle.StyleButton(_confirmButton, primary: true);
		_confirmButton.Pressed += OnConfirm;
		// Стартовое состояние — disabled: имя пустое, _nameStatus = TooShort.
		_confirmButton.Disabled = true;
		btnBar.AddChild(_confirmButton);
	}
}
