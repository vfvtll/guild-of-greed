using Godot;
using System;

// InventoryOverlay — построение UI и фабрики карточек.
public partial class InventoryOverlay
{
	private void BuildUI()
	{
		// Затемнение позади панели — закрывает клики по бою.
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(dim);

		var panel = new PanelContainer
		{
			Position = new Vector2(70, 60),
			Size = new Vector2(1140, 600),
			CustomMinimumSize = new Vector2(1140, 600),
		};
		panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 14);
		panel.AddChild(v);

		var title = UIStyle.MakeLabel("🎒 Инвентарь", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(title);

		var sep = new HSeparator();
		var sepStyle = new StyleBoxFlat
		{
			BgColor = UIStyle.GoldDark,
			ContentMarginTop = 1, ContentMarginBottom = 1,
		};
		sep.AddThemeStyleboxOverride("separator", sepStyle);
		v.AddChild(sep);

		// === Сводка по эффективным параметрам ===
		var summary = new PanelContainer();
		summary.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		v.AddChild(summary);

		var summaryV = new VBoxContainer();
		summaryV.AddThemeConstantOverride("separation", 2);
		summary.AddChild(summaryV);
		_summaryAtk  = UIStyle.MakeLabel("", 13, UIStyle.TextPrimary);
		_summaryDef  = UIStyle.MakeLabel("", 13, UIStyle.TextPrimary);
		_summaryCrit = UIStyle.MakeLabel("", 13, UIStyle.WarnAmber);
		summaryV.AddChild(_summaryAtk);
		summaryV.AddChild(_summaryDef);
		summaryV.AddChild(_summaryCrit);

		// Две колонки: надето слева, инвентарь справа.
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 24);
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		v.AddChild(columns);

		// === Левая колонка: надето ===
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
		left.AddThemeConstantOverride("separation", 8);
		columns.AddChild(left);

		left.AddChild(UIStyle.MakeSectionTitle("Надето"));
		_equipmentList = new VBoxContainer();
		_equipmentList.AddThemeConstantOverride("separation", 6);
		left.AddChild(_equipmentList);

		var hint = UIStyle.MakeLabel("Клик — снять в инвентарь", 11, UIStyle.TextDim);
		left.AddChild(hint);

		// === Правая колонка: инвентарь ===
		var right = new VBoxContainer();
		right.AddThemeConstantOverride("separation", 8);
		right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(right);

		_capacityLabel = UIStyle.MakeSectionTitle("Содержимое");
		right.AddChild(_capacityLabel);

		_inventoryGrid = new GridContainer { Columns = GridColumns };
		_inventoryGrid.AddThemeConstantOverride("h_separation", 8);
		_inventoryGrid.AddThemeConstantOverride("v_separation", 8);
		right.AddChild(_inventoryGrid);

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
		closeBtn.CustomMinimumSize = new Vector2(180, 44);
		closeBtn.Pressed += () => EmitSignal(SignalName.Closed);
		btnRow.AddChild(closeBtn);
	}

	// =====================================================================
	// Фабрики карточек слотов
	// =====================================================================

	private Control MakeSlotRow(string icon, string slotName, string itemId, Action onClick)
	{
		string itemName = "—";
		string detail = "(пусто)";
		if (!string.IsNullOrEmpty(itemId))
		{
			var (name, det, _) = DescribeItem(itemId);
			itemName = name;
			detail = det;
		}

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(280, 56) };
		var hovered = false;
		ApplySlotStyle(panel, hovered, isEmpty: string.IsNullOrEmpty(itemId));
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.TooltipText = $"{slotName}: {itemName}\n{detail}";
		panel.MouseEntered += () => { hovered = true; ApplySlotStyle(panel, hovered, string.IsNullOrEmpty(itemId)); };
		panel.MouseExited  += () => { hovered = false; ApplySlotStyle(panel, hovered, string.IsNullOrEmpty(itemId)); };
		panel.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				onClick();
		};

		var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		h.AddThemeConstantOverride("separation", 8);
		panel.AddChild(h);

		var iconL = UIStyle.MakeLabel(icon, 22, UIStyle.GoldBright);
		iconL.CustomMinimumSize = new Vector2(36, 0);
		iconL.HorizontalAlignment = HorizontalAlignment.Center;
		h.AddChild(iconL);

		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		h.AddChild(col);

		var slotL = UIStyle.MakeLabel(slotName, 10, UIStyle.TextDim);
		col.AddChild(slotL);

		var nameL = UIStyle.MakeLabel(itemName, 12, UIStyle.TextPrimary);
		nameL.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		col.AddChild(nameL);

		return panel;
	}

	private Control MakeItemCard(string itemId, int count, Action onClick)
	{
		var (name, detail, icon) = DescribeItem(itemId);
		var ch = GameData.Instance.Character;

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(190, 56) };
		var hovered = false;
		ApplyItemStyle(panel, hovered);
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.TooltipText = BuildItemTooltip(itemId, name, detail, ch);
		panel.MouseEntered += () => { hovered = true; ApplyItemStyle(panel, hovered); };
		panel.MouseExited  += () => { hovered = false; ApplyItemStyle(panel, hovered); };
		panel.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				onClick();
		};

		var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		h.AddThemeConstantOverride("separation", 6);
		panel.AddChild(h);

		var iconL = UIStyle.MakeLabel(icon, 22, UIStyle.GoldBright);
		iconL.CustomMinimumSize = new Vector2(28, 0);
		iconL.HorizontalAlignment = HorizontalAlignment.Center;
		h.AddChild(iconL);

		var nameL = UIStyle.MakeLabel(name, 11, UIStyle.TextPrimary);
		nameL.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameL.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		h.AddChild(nameL);

		if (count > 1)
		{
			var countL = UIStyle.MakeLabel($"×{count}", 14, UIStyle.WarnAmber);
			h.AddChild(countL);
		}

		return panel;
	}

	private Control MakeEmptyCard()
	{
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(190, 56) };
		panel.MouseFilter = MouseFilterEnum.Ignore;
		var sb = UIStyle.MiniPanelStyle();
		sb.BorderColor = UIStyle.GoldDark * 0.4f;
		sb.BgColor = new Color(0.08f, 0.07f, 0.10f);
		panel.AddThemeStyleboxOverride("panel", sb);

		var l = UIStyle.MakeLabel("—", 14, UIStyle.TextDim);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		panel.AddChild(l);
		return panel;
	}

	private static void ApplySlotStyle(PanelContainer p, bool hovered, bool isEmpty)
	{
		var sb = UIStyle.MiniPanelStyle();
		sb.BorderWidthLeft = 2; sb.BorderWidthRight = 2;
		sb.BorderWidthTop = 2; sb.BorderWidthBottom = 2;
		if (isEmpty)
		{
			sb.BgColor = new Color(0.10f, 0.09f, 0.13f);
			sb.BorderColor = UIStyle.GoldDark * 0.5f;
		}
		else if (hovered)
		{
			sb.BgColor = new Color(0.20f, 0.16f, 0.10f);
			sb.BorderColor = UIStyle.GoldBright;
		}
		else
		{
			sb.BgColor = UIStyle.PanelBgLight;
			sb.BorderColor = UIStyle.GoldMid;
		}
		p.AddThemeStyleboxOverride("panel", sb);
	}

	private static void ApplyItemStyle(PanelContainer p, bool hovered)
	{
		var sb = UIStyle.MiniPanelStyle();
		sb.BorderWidthLeft = 2; sb.BorderWidthRight = 2;
		sb.BorderWidthTop = 2; sb.BorderWidthBottom = 2;
		if (hovered)
		{
			sb.BgColor = new Color(0.20f, 0.18f, 0.26f);
			sb.BorderColor = UIStyle.GoldBright;
		}
		else
		{
			sb.BgColor = UIStyle.PanelBgLight;
			sb.BorderColor = UIStyle.GoldMid;
		}
		p.AddThemeStyleboxOverride("panel", sb);
	}
}
