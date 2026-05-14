using Godot;
using System;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Кузница. Три операции над instance-предметами инвентаря:
//   🔨 Распылить — превратить в магическую эссенцию (бесплатно).
//   ⚒ Улучшить  — поднять rarity на 1 ступень (если grade позволяет).
//   🎲 Реролл    — перекатать аффиксы (та же rarity).
//
// Зелья и стакаемые предметы игнорируются (нет rarity-апгрейда).
public partial class ForgeOverlay : Control
{
	[Signal] public delegate void ClosedEventHandler();

	private PanelContainer _panel;
	private ColorRect _dim;
	private Label _essenceLabel;
	private VBoxContainer _itemList;
	private Label _statusLabel;
	private Label _capacityLabel;

	public override void _Ready()
	{
		// FillParent вместо голого SetAnchorsPreset — гарантирует, что оверлей
		// растянут на весь рут, а не схлопывается под нулевой стартовый rect.
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

		// Адаптивный размер: панель всегда на весь экран с отступом, даже если
		// контента мало (например, в инвентаре нет снаряжения).
		_panel = new PanelContainer();
		_panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(_panel);
		UIStyle.FillParent(_panel, marginX: 40, marginY: 30);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 10);
		_panel.AddChild(v);

		// === Title ===
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		v.AddChild(titleRow);

		var leftSpacer = new Control { CustomMinimumSize = new Vector2(44, 0) };
		titleRow.AddChild(leftSpacer);

		var title = UIStyle.MakeLabel("⚒ Кузница", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		titleRow.AddChild(title);

		var xBtn = new Button { Text = "✕" };
		UIStyle.StyleButton(xBtn);
		xBtn.CustomMinimumSize = new Vector2(44, 44);
		xBtn.Pressed += Close;
		titleRow.AddChild(xBtn);

		_essenceLabel = UIStyle.MakeLabel("", 16, UIStyle.GoldBright);
		_essenceLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_essenceLabel);

		var hint = UIStyle.MakeLabel(
			"🔨 Распылить → эссенция · ⚒ Улучшить → +1 редкость · 🎲 Реролл → новые аффиксы",
			12, UIStyle.TextDim);
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(hint);

		v.AddChild(new HSeparator());

		_capacityLabel = UIStyle.MakeSectionTitle("Инвентарь");
		v.AddChild(_capacityLabel);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		v.AddChild(scroll);

		_itemList = new VBoxContainer();
		_itemList.AddThemeConstantOverride("separation", 6);
		_itemList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(_itemList);

		_statusLabel = UIStyle.MakeLabel("", 13, UIStyle.WarnAmber);
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_statusLabel);

		var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
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

		_essenceLabel.Text = $"✨ Магическая эссенция: {ch.Inventory.Essence}";
		_capacityLabel.Text = $"Инвентарь ({ch.Inventory.Slots.Count}/{Inventory.Capacity}) — кузнец работает только с экипировкой";

		ClearChildren(_itemList);
		int forgeable = 0;
		for (int i = 0; i < ch.Inventory.Slots.Count; i++)
		{
			var stack = ch.Inventory.Slots[i];
			// Кузница работает только с instance-предметами (weapon/armor/shield).
			if (stack.WeaponInstance == null && stack.ArmorInstance == null && stack.ShieldInstance == null)
				continue;
			_itemList.AddChild(MakeItemRow(stack, i));
			forgeable++;
		}

		// Пустое состояние: явная подсказка, чтобы игрок не думал, что кузнец
		// сломан. Все надетые предметы или зелья не годятся — нужно положить
		// инстансы оружия/брони/щита в инвентарь.
		if (forgeable == 0)
			_itemList.AddChild(MakeEmptyState());
	}

	private Control MakeEmptyState()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 8);
		panel.AddChild(v);

		var spacerTop = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
		v.AddChild(spacerTop);

		var icon = UIStyle.MakeLabel("⚒", 64, UIStyle.GoldDark);
		icon.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(icon);

		var msg = UIStyle.MakeLabel(
			"В инвентаре нет снаряжения, которое можно обработать.",
			15, UIStyle.TextSecondary);
		msg.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(msg);

		var hint = UIStyle.MakeLabel(
			"Кузнец работает только с оружием, бронёй и щитами в инвентаре.\n" +
			"Снимите ненужное с персонажа или достаньте из стэша, потом возвращайтесь.",
			12, UIStyle.TextDim);
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(hint);

		var spacerBot = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
		v.AddChild(spacerBot);

		return panel;
	}

	// === Item row ===

	private Control MakeItemRow(InventoryStack stack, int slotIndex)
	{
		string name, grade, rank;
		ItemRarity rarity;
		bool canRerollAffixes;
		if (stack.WeaponInstance != null)
		{
			var w = stack.WeaponInstance;
			name = w.Name; grade = w.Grade; rank = w.Tier; rarity = w.Rarity;
			canRerollAffixes = true;
		}
		else if (stack.ArmorInstance != null)
		{
			var a = stack.ArmorInstance;
			name = a.Name; grade = a.Grade; rank = a.Tier; rarity = a.Rarity;
			canRerollAffixes = true;
		}
		else
		{
			var s = stack.ShieldInstance;
			name = s.Name; grade = s.Grade; rank = s.Tier; rarity = s.Rarity;
			canRerollAffixes = false;  // у щитов сейчас нет ItemGenerator-роллера
		}

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 4);
		panel.AddChild(col);

		// Шапка: имя + grade/rank/rarity.
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		col.AddChild(header);

		var nameL = UIStyle.MakeLabel(name, 14, UIStyle.RarityColor(rarity));
		nameL.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(nameL);

		var tagL = UIStyle.MakeLabel($"{grade}-{rank} · {rarity}", 11, UIStyle.TextDim);
		header.AddChild(tagL);

		// Кнопки.
		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 6);
		col.AddChild(actions);

		long dismantleYield = ForgeDB.DismantleEssence(grade, rank, rarity);
		var distillBtn = new Button { Text = $"🔨 Распылить (+{dismantleYield})" };
		UIStyle.StyleButton(distillBtn);
		distillBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		distillBtn.Pressed += () => OnDistill(slotIndex);
		actions.AddChild(distillBtn);

		long upgradeCost = ForgeDB.UpgradeCost(grade, rank, rarity);
		// UpgradeCost: -1 = нельзя апать (потолок rarity для grade), иначе >=1.
		// Раньше тут было > 0, и для E-low Common кнопка отключалась из-за того,
		// что 1*20/100 = 0 в int-делении. Чинено и здесь, и в ForgeDB.
		bool canUp = upgradeCost >= 0;
		var upgradeBtn = new Button
		{
			Text = canUp
				? $"⚒ Улучшить ({upgradeCost}) → {ForgeDB.NextRarity(rarity)}"
				: $"⚒ Макс. {ForgeDB.MaxRarityFor(grade)}",
			Disabled = !canUp,
		};
		UIStyle.StyleButton(upgradeBtn);
		upgradeBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (canUp) upgradeBtn.Pressed += () => OnUpgrade(slotIndex);
		actions.AddChild(upgradeBtn);

		long rerollCost = ForgeDB.RerollCost(grade, rank);
		var rerollBtn = new Button
		{
			Text = canRerollAffixes ? $"🎲 Реролл ({rerollCost})" : "🎲 Нет аффиксов",
			Disabled = !canRerollAffixes,
		};
		UIStyle.StyleButton(rerollBtn);
		rerollBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (canRerollAffixes) rerollBtn.Pressed += () => OnReroll(slotIndex);
		actions.AddChild(rerollBtn);

		return panel;
	}

	// === Handlers ===

	private void OnDistill(int slotIndex)
	{
		long got = GameData.Instance.ForgeDismantle(slotIndex);
		if (got <= 0)
		{
			SetStatus("Не удалось распылить.", error: true);
			return;
		}
		SetStatus($"Получено эссенции: +{got}.", error: false);
		Refresh();
	}

	private void OnUpgrade(int slotIndex)
	{
		var (ok, reason) = GameData.Instance.ForgeUpgrade(slotIndex);
		if (!ok)
		{
			string msg = reason switch
			{
				"no_essence"     => "Не хватает эссенции.",
				"cant_upgrade"   => "Этот предмет уже на максимальной редкости для своего grade.",
				_                => "Не удалось улучшить.",
			};
			SetStatus(msg, error: true);
			return;
		}
		SetStatus("Редкость повышена.", error: false);
		Refresh();
	}

	private void OnReroll(int slotIndex)
	{
		var (ok, reason) = GameData.Instance.ForgeReroll(slotIndex);
		if (!ok)
		{
			string msg = reason switch
			{
				"no_essence"      => "Не хватает эссенции.",
				"not_rerollable"  => "У этого предмета нельзя перекатать аффиксы.",
				_                 => "Не удалось перекатать.",
			};
			SetStatus(msg, error: true);
			return;
		}
		SetStatus("Аффиксы перекатаны.", error: false);
		Refresh();
	}

	private void SetStatus(string text, bool error)
	{
		_statusLabel.Text = text;
		_statusLabel.AddThemeColorOverride("font_color",
			error ? UIStyle.DangerRed : UIStyle.HealGreen);
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
