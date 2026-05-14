using Godot;
using System;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Перенос предметов между Inventory и Stash.
//
// UX:
//   - Левая колонка — инвентарь (20 ячеек). Клик по слоту → переложить в стэш.
//   - Правая колонка — стэш (50 ячеек). Клик по слоту → переложить в инвентарь.
//   - Перенос — слот целиком (включая стак зелий). Для деления стака пока нет UI.
//
// Все изменения локальные (см. GameData.DepositToStash/WithdrawFromStash).
// Персистенс на сервер — при следующем сохранении персонажа (после боя).
public partial class StashOverlay : Control
{
	[Signal] public delegate void ClosedEventHandler();

	private PanelContainer _panel;
	private ColorRect _dim;
	private GridContainer _invGrid;
	private GridContainer _stashGrid;
	private Label _invTitle;
	private Label _stashTitle;
	private Label _statusLabel;

	private const int GridColumns = 4;

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
		Refresh();
		PlayOpenAnimation();
	}

	public void Close()
	{
		var t = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		t.TweenProperty(_panel, "modulate:a", 0f, 0.18f);
		t.TweenProperty(_dim, "modulate:a", 0f, 0.18f);
		t.Chain().TweenCallback(Callable.From(() => EmitSignal(SignalName.Closed)));
	}

	private void PlayOpenAnimation()
	{
		_panel.Modulate = new Color(1, 1, 1, 0);
		_dim.Modulate = new Color(1, 1, 1, 0);

		var t = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(_panel, "modulate:a", 1f, 0.22f);
		t.TweenProperty(_dim, "modulate:a", 1f, 0.22f);
	}

	private void BuildUI()
	{
		_dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		_dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(_dim);
		UIStyle.FillParent(_dim);

		// Адаптивный фулл-рект с отступами — гарантированно на весь экран.
		_panel = new PanelContainer();
		_panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(_panel);
		UIStyle.FillParent(_panel, marginX: 40, marginY: 30);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 12);
		_panel.AddChild(v);

		// === Title row ===
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		v.AddChild(titleRow);

		var leftSpacer = new Control { CustomMinimumSize = new Vector2(44, 0) };
		titleRow.AddChild(leftSpacer);

		var title = UIStyle.MakeLabel("🏦 Стэш", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		titleRow.AddChild(title);

		var xBtn = new Button { Text = "✕" };
		UIStyle.StyleButton(xBtn);
		xBtn.CustomMinimumSize = new Vector2(44, 44);
		xBtn.Pressed += Close;
		titleRow.AddChild(xBtn);

		// === Hint ===
		var hint = UIStyle.MakeLabel(
			"Клик по слоту переносит весь стак на противоположную сторону.",
			12, UIStyle.TextDim);
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(hint);

		v.AddChild(new HSeparator());

		// === Две колонки ===
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 24);
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		v.AddChild(columns);

		var left = new VBoxContainer();
		left.AddThemeConstantOverride("separation", 8);
		left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(left);

		_invTitle = UIStyle.MakeSectionTitle("Инвентарь");
		left.AddChild(_invTitle);

		var leftHint = UIStyle.MakeLabel("→ Клик переносит в стэш", 11, UIStyle.TextDim);
		left.AddChild(leftHint);

		var leftScroll = new ScrollContainer();
		leftScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		leftScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		left.AddChild(leftScroll);

		_invGrid = new GridContainer { Columns = GridColumns };
		_invGrid.AddThemeConstantOverride("h_separation", 8);
		_invGrid.AddThemeConstantOverride("v_separation", 8);
		leftScroll.AddChild(_invGrid);

		var right = new VBoxContainer();
		right.AddThemeConstantOverride("separation", 8);
		right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(right);

		_stashTitle = UIStyle.MakeSectionTitle("Стэш");
		right.AddChild(_stashTitle);

		var rightHint = UIStyle.MakeLabel("← Клик переносит в инвентарь", 11, UIStyle.TextDim);
		right.AddChild(rightHint);

		var rightScroll = new ScrollContainer();
		rightScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		rightScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		right.AddChild(rightScroll);

		_stashGrid = new GridContainer { Columns = GridColumns };
		_stashGrid.AddThemeConstantOverride("h_separation", 8);
		_stashGrid.AddThemeConstantOverride("v_separation", 8);
		rightScroll.AddChild(_stashGrid);

		// === Status + close ===
		_statusLabel = UIStyle.MakeLabel("", 13, UIStyle.WarnAmber);
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_statusLabel);

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(btnRow);

		var closeBtn = new Button { Text = "Закрыть" };
		UIStyle.StyleButton(closeBtn, primary: true);
		closeBtn.CustomMinimumSize = new Vector2(180, 44);
		closeBtn.Pressed += Close;
		btnRow.AddChild(closeBtn);
	}

	private void Refresh()
	{
		var ch = GameData.Instance.Character;
		if (ch == null) return;

		_invTitle.Text = $"Инвентарь ({ch.Inventory.Slots.Count}/{Inventory.Capacity})";
		_stashTitle.Text = $"Стэш ({ch.Stash.Slots.Count}/{Stash.Capacity})";

		ClearChildren(_invGrid);
		for (int i = 0; i < Inventory.Capacity; i++)
		{
			if (i < ch.Inventory.Slots.Count)
			{
				int idx = i;
				_invGrid.AddChild(MakeSlotCard(ch.Inventory.Slots[i], () => OnInvSlotClicked(idx)));
			}
			else _invGrid.AddChild(MakeEmptyCard());
		}

		ClearChildren(_stashGrid);
		for (int i = 0; i < Stash.Capacity; i++)
		{
			if (i < ch.Stash.Slots.Count)
			{
				int idx = i;
				_stashGrid.AddChild(MakeSlotCard(ch.Stash.Slots[i], () => OnStashSlotClicked(idx)));
			}
			else _stashGrid.AddChild(MakeEmptyCard());
		}
	}

	private void OnInvSlotClicked(int idx)
	{
		if (!GameData.Instance.DepositToStash(idx))
		{
			SetStatus("Стэш полон.", error: true);
			return;
		}
		SetStatus("Перенесено в стэш.", error: false);
		Refresh();
	}

	private void OnStashSlotClicked(int idx)
	{
		if (!GameData.Instance.WithdrawFromStash(idx))
		{
			SetStatus("Инвентарь полон.", error: true);
			return;
		}
		SetStatus("Перенесено в инвентарь.", error: false);
		Refresh();
	}

	private void SetStatus(string text, bool error)
	{
		_statusLabel.Text = text;
		_statusLabel.AddThemeColorOverride("font_color",
			error ? UIStyle.DangerRed : UIStyle.HealGreen);
	}

	// ===== Slot cards =====

	private Control MakeSlotCard(InventoryStack stack, Action onClick)
	{
		string name; string icon; ItemRarity rarity;
		(name, icon, rarity) = DescribeSlot(stack);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(190, 56) };
		var hovered = false;
		ApplyCardStyle(panel, hovered, rarity);
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.TooltipText = name;
		panel.MouseEntered += () => { hovered = true; ApplyCardStyle(panel, hovered, rarity); };
		panel.MouseExited  += () => { hovered = false; ApplyCardStyle(panel, hovered, rarity); };
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

		var nameL = UIStyle.MakeLabel(name, 11, UIStyle.RarityColor(rarity));
		nameL.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameL.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		h.AddChild(nameL);

		if (stack.Count > 1)
			h.AddChild(UIStyle.MakeLabel($"×{stack.Count}", 14, UIStyle.WarnAmber));

		return panel;
	}

	private static Control MakeEmptyCard()
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

	private static void ApplyCardStyle(PanelContainer p, bool hovered, ItemRarity rarity)
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
			sb.BorderColor = UIStyle.RarityColor(rarity);
		}
		p.AddThemeStyleboxOverride("panel", sb);
	}

	// (name, icon, rarity) для произвольного стака — instance или стакаемый baseId.
	public static (string name, string icon, ItemRarity rarity) DescribeSlot(InventoryStack s)
	{
		if (s.WeaponInstance != null)
			return (s.WeaponInstance.Name, "⚔", s.WeaponInstance.Rarity);
		if (s.ArmorInstance != null)
			return (s.ArmorInstance.Name, SlotIcon(s.ArmorInstance.Slot), s.ArmorInstance.Rarity);
		if (s.ShieldInstance != null)
			return (s.ShieldInstance.Name, "🛡", s.ShieldInstance.Rarity);

		var p = PotionsDB.Get(s.ItemId);
		if (p != null) return (p.Name, p.Icon ?? "🧪", p.Rarity);
		var w = ItemsDB.GetWeapon(s.ItemId);
		if (w != null) return (w.Name, "⚔", w.Rarity);
		var a = ItemsDB.GetArmor(s.ItemId);
		if (a != null) return (a.Name, SlotIcon(a.Slot), a.Rarity);
		var sh = ShieldsDB.Get(s.ItemId);
		if (sh != null) return (sh.Name, "🛡", sh.Rarity);
		return (s.ItemId, "?", ItemRarity.Common);
	}

	public static string SlotIcon(ArmorSlot slot) => slot switch
	{
		ArmorSlot.Chest  => "👕",
		ArmorSlot.Helmet => "⛑",
		ArmorSlot.Gloves => "🧤",
		ArmorSlot.Boots  => "👢",
		ArmorSlot.Amulet => "📿",
		ArmorSlot.Ring1  => "💍",
		ArmorSlot.Ring2  => "💍",
		_                 => "🧱",
	};

	private static void ClearChildren(Node container)
	{
		foreach (Node child in container.GetChildren())
		{
			container.RemoveChild(child);
			child.QueueFree();
		}
	}
}
