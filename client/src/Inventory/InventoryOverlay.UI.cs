using Godot;
using System;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// InventoryOverlay — построение UI и фабрики карточек.
public partial class InventoryOverlay
{
	private void BuildUI()
	{
		// Затемнение позади панели — закрывает клики по бою.
		_dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		_dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(_dim);
		UIStyle.FillParent(_dim);

		// Панель на весь экран с отступом. FillParent прибивает offsets к нулю.
		_panel = new PanelContainer();
		_panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(_panel);
		UIStyle.FillParent(_panel, marginX: 40, marginY: 30);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 10);
		_panel.AddChild(v);

		// === Шапка: 🎒 название | кошель | 📊 персонаж | ✕ ===
		// Кошель — RichText в центре, чтобы три номинала читались с одного взгляда.
		// Кнопка 📊 открывает модалку со сводкой персонажа (статы / бой / пассивы /
		// сеты) — раньше всё это висело inline под title row и захламляло экран.
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 10);
		v.AddChild(titleRow);

		var title = UIStyle.MakeLabel("🎒 Инвентарь", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Left;
		titleRow.AddChild(title);

		_currencyLabel = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			CustomMinimumSize = new Vector2(0, 24),
		};
		_currencyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_currencyLabel.AddThemeFontSizeOverride("normal_font_size", 14);
		_currencyLabel.AddThemeColorOverride("default_color", UIStyle.TextPrimary);
		titleRow.AddChild(_currencyLabel);

		var infoBtn = new Button { Text = "📊" };
		UIStyle.StyleButton(infoBtn);
		infoBtn.CustomMinimumSize = new Vector2(44, 44);
		infoBtn.TooltipText = "Сводка персонажа: статы, бой, пассивы и сеты";
		infoBtn.Pressed += OpenCharacterSheet;
		titleRow.AddChild(infoBtn);

		var xBtn = new Button { Text = "✕" };
		UIStyle.StyleButton(xBtn);
		xBtn.CustomMinimumSize = new Vector2(44, 44);
		xBtn.TooltipText = "Закрыть инвентарь";
		xBtn.Pressed += Close;
		titleRow.AddChild(xBtn);

		// Плашка показа ReadOnly — видна только во время боя.
		_readOnlyHint = UIStyle.MakeLabel(
			"🔒 Бой идёт — изменения недоступны (зелья пьются через панель игрока)",
			13, UIStyle.WarnAmber);
		_readOnlyHint.HorizontalAlignment = HorizontalAlignment.Center;
		_readOnlyHint.Visible = ReadOnly;
		v.AddChild(_readOnlyHint);

		var sep = new HSeparator();
		var sepStyle = new StyleBoxFlat
		{
			BgColor = UIStyle.GoldDark,
			ContentMarginTop = 1, ContentMarginBottom = 1,
		};
		sep.AddThemeStyleboxOverride("separator", sepStyle);
		v.AddChild(sep);

		// === Распределение очков статов ===
		// Сетка 3×2 вместо HBox на 6 кнопок: 6×140 = 840px не помещается
		// на 1280-ширине после margin'ов. 3×2 ≈ 420×каждая по 100px широки.
		_statSpendPanel = new PanelContainer();
		_statSpendPanel.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		v.AddChild(_statSpendPanel);

		var spendV = new VBoxContainer();
		spendV.AddThemeConstantOverride("separation", 6);
		_statSpendPanel.AddChild(spendV);

		_statPointsAvailLabel = UIStyle.MakeLabel("", 14, UIStyle.GoldBright);
		spendV.AddChild(_statPointsAvailLabel);

		var spendGrid = new GridContainer { Columns = 3 };
		spendGrid.AddThemeConstantOverride("h_separation", 6);
		spendGrid.AddThemeConstantOverride("v_separation", 4);
		spendV.AddChild(spendGrid);

		for (int i = 0; i < 6; i++)
		{
			var btn = new Button { Text = "" };
			UIStyle.StyleButton(btn);
			btn.CustomMinimumSize = new Vector2(120, 0);
			btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			string capturedStat = StatIds[i];
			btn.TooltipText = StatTooltip(capturedStat);
			btn.Pressed += () => OnSpendStatPressed(capturedStat);
			spendGrid.AddChild(btn);
			_statSpendButtons[i] = btn;
		}

		// Две колонки: надето слева, инвентарь справа. ExpandFill даёт им
		// доступную высоту целиком — оба внутренних скролла растягиваются.
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 20);
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		columns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		v.AddChild(columns);

		// === Левая колонка: надето ===
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
		left.AddThemeConstantOverride("separation", 6);
		columns.AddChild(left);

		left.AddChild(UIStyle.MakeSectionTitle("Надето"));

		var equipScroll = new ScrollContainer();
		equipScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		equipScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		left.AddChild(equipScroll);

		_equipmentList = new VBoxContainer();
		_equipmentList.AddThemeConstantOverride("separation", 5);
		_equipmentList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		equipScroll.AddChild(_equipmentList);

		var hint = UIStyle.MakeLabel("Клик — снять в инвентарь", 11, UIStyle.TextDim);
		left.AddChild(hint);

		// === Правая колонка: инвентарь ===
		var right = new VBoxContainer();
		right.AddThemeConstantOverride("separation", 6);
		right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		right.SizeFlagsVertical   = SizeFlags.ExpandFill;
		columns.AddChild(right);

		_capacityLabel = UIStyle.MakeSectionTitle("Содержимое");
		right.AddChild(_capacityLabel);

		// 40 ячеек × 50px = 400px + отступы — при 8 рядах легко выходит за
		// экран. ScrollContainer держит сетку всегда внутри границ панели.
		var invScroll = new ScrollContainer();
		invScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		invScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		invScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		right.AddChild(invScroll);

		_inventoryGrid = new GridContainer { Columns = GridColumns };
		_inventoryGrid.AddThemeConstantOverride("h_separation", 6);
		_inventoryGrid.AddThemeConstantOverride("v_separation", 6);
		invScroll.AddChild(_inventoryGrid);

		var hint2 = UIStyle.MakeLabel("Клик — надеть / выпить зелье", 11, UIStyle.TextDim);
		right.AddChild(hint2);

		// === Низ: статус + закрыть ===
		_statusLabel = UIStyle.MakeLabel("", 13, UIStyle.WarnAmber);
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_statusLabel);

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(btnRow);

		var closeBtn = new Button { Text = "Закрыть" };
		UIStyle.StyleButton(closeBtn, primary: true);
		closeBtn.CustomMinimumSize = new Vector2(180, 40);
		closeBtn.Pressed += Close;
		btnRow.AddChild(closeBtn);
	}

	// Подсказка для каждой кнопки распределения очков.
	private static string StatTooltip(string stat) => stat switch
	{
		"STR" => "Физическая атака",
		"INT" => "Магическая атака",
		"CON" => "Здоровье (ХП)",
		"WIT" => "Регенерация маны",
		"MEN" => "Максимум маны",
		"DEX" => "Блок, шанс и сила крита",
		_     => "",
	};

	// Фабрики карточек слотов и предметов вынесены в InventoryOverlay.Cards.cs
	// (UI.cs упёрся бы в 500-строчный cap из CODING_STANDARDS.md).
}
