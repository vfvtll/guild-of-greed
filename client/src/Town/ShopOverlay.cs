using Godot;
using System;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Лавка: купить базовые зелья (ShopDB.Stock) / продать любой слот из инвентаря.
//
// UX:
//   - Левая колонка — ассортимент лавки. Цена + кнопка "Купить" покупает 1 шт.
//   - Правая колонка — инвентарь. Клик по слоту → продать весь стак.
//   - Зелья из лавки доступны без ограничения по числу покупок (∞).
//
// Все операции — клиентские (см. GameData). На сервер уйдёт при следующем бое.
public partial class ShopOverlay : Control
{
	[Signal] public delegate void ClosedEventHandler();

	private PanelContainer _panel;
	private ColorRect _dim;
	private Vector2 _panelTargetPos;
	private RichTextLabel _moneyLabel;
	private VBoxContainer _shopList;
	private GridContainer _invGrid;
	private Label _invTitle;
	private Label _statusLabel;

	private const int GridColumns = 3;
	private const string HexGold   = "#f3d172";
	private const string HexSilver = "#cfd2d8";
	private const string HexCopper = "#d18a4d";

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
		Refresh();
		PlayOpenAnimation();
	}

	public void Close()
	{
		var t = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		t.TweenProperty(_panel, "position", _panelTargetPos + new Vector2(0, 30), 0.18f);
		t.TweenProperty(_panel, "modulate:a", 0f, 0.18f);
		t.TweenProperty(_dim, "modulate:a", 0f, 0.18f);
		t.Chain().TweenCallback(Callable.From(() => EmitSignal(SignalName.Closed)));
	}

	private void PlayOpenAnimation()
	{
		_panelTargetPos = _panel.Position;
		_panel.Position = _panelTargetPos + new Vector2(0, 30);
		_panel.Modulate = new Color(1, 1, 1, 0);
		_dim.Modulate = new Color(1, 1, 1, 0);

		var t = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(_panel, "position", _panelTargetPos, 0.22f);
		t.TweenProperty(_panel, "modulate:a", 1f, 0.22f);
		t.TweenProperty(_dim, "modulate:a", 1f, 0.22f);
	}

	private void BuildUI()
	{
		_dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		_dim.SetAnchorsPreset(LayoutPreset.FullRect);
		_dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(_dim);

		_panel = new PanelContainer
		{
			Position = new Vector2(60, 30),
			Size = new Vector2(1160, 660),
			CustomMinimumSize = new Vector2(1160, 660),
		};
		_panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(_panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 12);
		_panel.AddChild(v);

		// === Title row ===
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		v.AddChild(titleRow);

		var leftSpacer = new Control { CustomMinimumSize = new Vector2(44, 0) };
		titleRow.AddChild(leftSpacer);

		var title = UIStyle.MakeLabel("🛒 Лавка", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		titleRow.AddChild(title);

		var xBtn = new Button { Text = "✕" };
		UIStyle.StyleButton(xBtn);
		xBtn.CustomMinimumSize = new Vector2(44, 44);
		xBtn.Pressed += Close;
		titleRow.AddChild(xBtn);

		// === Кошель ===
		_moneyLabel = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			CustomMinimumSize = new Vector2(0, 22),
		};
		_moneyLabel.AddThemeFontSizeOverride("normal_font_size", 14);
		v.AddChild(_moneyLabel);

		v.AddChild(new HSeparator());

		// === Две колонки ===
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 24);
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		v.AddChild(columns);

		// === Левая колонка: ассортимент ===
		var left = new VBoxContainer();
		left.AddThemeConstantOverride("separation", 8);
		left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		left.CustomMinimumSize = new Vector2(420, 0);
		columns.AddChild(left);

		left.AddChild(UIStyle.MakeSectionTitle("Купить"));

		var leftScroll = new ScrollContainer();
		leftScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		leftScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		left.AddChild(leftScroll);

		_shopList = new VBoxContainer();
		_shopList.AddThemeConstantOverride("separation", 6);
		_shopList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		leftScroll.AddChild(_shopList);

		// === Правая колонка: продать ===
		var right = new VBoxContainer();
		right.AddThemeConstantOverride("separation", 8);
		right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(right);

		_invTitle = UIStyle.MakeSectionTitle("Продать (инвентарь)");
		right.AddChild(_invTitle);

		var rightHint = UIStyle.MakeLabel("Клик по слоту — продать весь стак (40% цены).",
			11, UIStyle.TextDim);
		right.AddChild(rightHint);

		var rightScroll = new ScrollContainer();
		rightScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		rightScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		right.AddChild(rightScroll);

		_invGrid = new GridContainer { Columns = GridColumns };
		_invGrid.AddThemeConstantOverride("h_separation", 8);
		_invGrid.AddThemeConstantOverride("v_separation", 8);
		rightScroll.AddChild(_invGrid);

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

		RefreshMoney(ch);
		_invTitle.Text = $"Продать (инвентарь {ch.Inventory.Slots.Count}/{Inventory.Capacity})";

		// === Ассортимент ===
		ClearChildren(_shopList);
		foreach (var itemId in ShopDB.Stock)
		{
			var price = ShopDB.BuyPrice(itemId);
			if (price == null) continue;
			_shopList.AddChild(MakeShopRow(itemId, price.Value));
		}

		// === Инвентарь ===
		ClearChildren(_invGrid);
		for (int i = 0; i < Inventory.Capacity; i++)
		{
			if (i < ch.Inventory.Slots.Count)
			{
				int idx = i;
				_invGrid.AddChild(MakeSellCard(ch.Inventory.Slots[i], () => OnSellClicked(idx)));
			}
			else _invGrid.AddChild(MakeEmptyCard());
		}
	}

	private void RefreshMoney(CharacterData ch)
	{
		var (g, s, c) = Currency.Split(ch.Inventory.Money);
		_moneyLabel.Text =
			$"🪙 Кошель:  " +
			$"[color={HexGold}]{g}з[/color]  " +
			$"[color={HexSilver}]{s}с[/color]  " +
			$"[color={HexCopper}]{c}м[/color]";
	}

	// ===== Buy =====

	private Control MakeShopRow(string itemId, long price)
	{
		var (name, icon, rarity) = ResolveBaseItem(itemId);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());

		var h = new HBoxContainer();
		h.AddThemeConstantOverride("separation", 8);
		panel.AddChild(h);

		var iconL = UIStyle.MakeLabel(icon, 22, UIStyle.GoldBright);
		iconL.CustomMinimumSize = new Vector2(28, 0);
		h.AddChild(iconL);

		var nameL = UIStyle.MakeLabel(name, 13, UIStyle.RarityColor(rarity));
		nameL.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		h.AddChild(nameL);

		var priceL = UIStyle.MakeLabel(Currency.FormatShort(price), 13, UIStyle.WarnAmber);
		priceL.CustomMinimumSize = new Vector2(110, 0);
		priceL.HorizontalAlignment = HorizontalAlignment.Right;
		h.AddChild(priceL);

		var buyBtn = new Button { Text = "Купить" };
		UIStyle.StyleButton(buyBtn);
		buyBtn.CustomMinimumSize = new Vector2(96, 0);
		buyBtn.Pressed += () => OnBuyClicked(itemId);
		h.AddChild(buyBtn);

		return panel;
	}

	private void OnBuyClicked(string itemId)
	{
		var (ok, reason) = GameData.Instance.BuyOne(itemId);
		if (!ok)
		{
			string msg = reason switch
			{
				"no_money"     => "Не хватает монет.",
				"no_space"     => "Инвентарь полон.",
				"not_for_sale" => "Лавка этого не продаёт.",
				_              => "Не удалось купить.",
			};
			SetStatus(msg, error: true);
			return;
		}
		var (name, _, _) = ResolveBaseItem(itemId);
		SetStatus($"Куплено: {name}.", error: false);
		Refresh();
	}

	// ===== Sell =====

	private Control MakeSellCard(InventoryStack stack, Action onClick)
	{
		var (name, icon, rarity) = StashOverlay.DescribeSlot(stack);
		long price = ShopDB.SellPriceForStack(stack);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(240, 70) };
		var hovered = false;
		ApplyCardStyle(panel, hovered, rarity);
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.TooltipText = $"{name}\nЦена продажи: {Currency.FormatShort(price)}";
		panel.MouseEntered += () => { hovered = true; ApplyCardStyle(panel, hovered, rarity); };
		panel.MouseExited  += () => { hovered = false; ApplyCardStyle(panel, hovered, rarity); };
		panel.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				onClick();
		};

		var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		v.AddThemeConstantOverride("separation", 2);
		panel.AddChild(v);

		var hRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		hRow.AddThemeConstantOverride("separation", 6);
		v.AddChild(hRow);

		var iconL = UIStyle.MakeLabel(icon, 20, UIStyle.GoldBright);
		iconL.CustomMinimumSize = new Vector2(24, 0);
		hRow.AddChild(iconL);

		var nameL = UIStyle.MakeLabel(name, 11, UIStyle.RarityColor(rarity));
		nameL.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameL.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		hRow.AddChild(nameL);

		if (stack.Count > 1)
			hRow.AddChild(UIStyle.MakeLabel($"×{stack.Count}", 13, UIStyle.WarnAmber));

		var priceL = UIStyle.MakeLabel($"→ {Currency.FormatShort(price)}", 11, UIStyle.HealGreen);
		priceL.HorizontalAlignment = HorizontalAlignment.Right;
		v.AddChild(priceL);

		return panel;
	}

	private void OnSellClicked(int slotIndex)
	{
		long gained = GameData.Instance.SellSlot(slotIndex);
		if (gained <= 0)
		{
			SetStatus("Не удалось продать.", error: true);
			return;
		}
		SetStatus($"Продано на {Currency.FormatShort(gained)}.", error: false);
		Refresh();
	}

	private static Control MakeEmptyCard()
	{
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(240, 70) };
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

	private void SetStatus(string text, bool error)
	{
		_statusLabel.Text = text;
		_statusLabel.AddThemeColorOverride("font_color",
			error ? UIStyle.DangerRed : UIStyle.HealGreen);
	}

	// Резолв базового предмета для отображения в ассортименте лавки.
	private static (string name, string icon, ItemRarity rarity) ResolveBaseItem(string itemId)
	{
		var p = PotionsDB.Get(itemId);
		if (p != null) return (p.Name, p.Icon ?? "🧪", p.Rarity);
		var w = ItemsDB.GetWeapon(itemId);
		if (w != null) return (w.Name, "⚔", w.Rarity);
		var a = ItemsDB.GetArmor(itemId);
		if (a != null) return (a.Name, StashOverlay.SlotIcon(a.Slot), a.Rarity);
		var s = ShieldsDB.Get(itemId);
		if (s != null) return (s.Name, "🛡", s.Rarity);
		return (itemId, "?", ItemRarity.Common);
	}

	private static void ClearChildren(Node container)
	{
		foreach (Node child in container.GetChildren())
		{
			container.RemoveChild(child);
			child.QueueFree();
		}
	}
}
