using Godot;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Хаб между забегами. Игрок выбирает локацию для следующего run'а или
// открывает инвентарь чтобы перераспределить экипировку.
//
// Сигналы:
//   LocationChosen(int index)     — игрок выбрал локацию.
//   ResetCharacterRequested()      — кнопка «Новый персонаж».
//
// На прототипе — простой список карточек. В будущем здесь же появится
// доступ к Stash, лавке, крафту, кораблю на новые континенты и т.п.
public partial class LocationSelectView : Control
{
	[Signal] public delegate void LocationChosenEventHandler(int index);
	[Signal] public delegate void ResetCharacterRequestedEventHandler();

	private InventoryOverlay _inventoryOverlay;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUI();
	}

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

		var inventoryBtn = new Button { Text = "🎒 Инвентарь" };
		UIStyle.StyleButton(inventoryBtn);
		inventoryBtn.Pressed += OnInventoryPressed;
		top.AddChild(inventoryBtn);

		var resetBtn = new Button { Text = "👤 Новый персонаж" };
		UIStyle.StyleButton(resetBtn);
		resetBtn.Pressed += () => EmitSignal(SignalName.ResetCharacterRequested);
		top.AddChild(resetBtn);

		// === Player snippet ===
		var p = GameData.Instance.Character;
		if (p != null)
		{
			var hp = UIStyle.MakeLabel(
				$"{p.CharacterName}   ❤ {p.MaxHp()}   ✦ {p.MaxMp()}   ⚔ {p.Weapon?.Name ?? "—"}",
				14, UIStyle.TextPrimary);
			hp.Position = new Vector2(440, 22);
			AddChild(hp);
		}

		// === Title ===
		var title = UIStyle.MakeLabel("🗡  Выбор подземелья", 28, UIStyle.GoldBright);
		title.Position = new Vector2(440, 90);
		AddChild(title);

		var sub = UIStyle.MakeLabel(
			"Каждое подземелье — карта с врагами, отдыхом, сокровищами и боссом в конце.",
			14, UIStyle.TextSecondary);
		sub.Position = new Vector2(440, 130);
		AddChild(sub);

		// === Карточки локаций ===
		var cardsRow = new HBoxContainer
		{
			Position = new Vector2(60, 200),
			Size = new Vector2(1160, 380),
		};
		cardsRow.AddThemeConstantOverride("separation", 24);
		cardsRow.Alignment = BoxContainer.AlignmentMode.Center;
		AddChild(cardsRow);

		for (int i = 0; i < GameData.LocationNames.Length; i++)
		{
			cardsRow.AddChild(MakeLocationCard(i));
		}
	}

	private PanelContainer MakeLocationCard(int index)
	{
		var card = new PanelContainer
		{
			CustomMinimumSize = new Vector2(330, 360),
		};
		card.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 10);
		card.AddChild(v);

		v.AddChild(UIStyle.MakeSectionTitle(GameData.LocationNames[index]));

		var hint = UIStyle.MakeLabel(GameData.LocationHints[index], 13, UIStyle.TextSecondary);
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		hint.CustomMinimumSize = new Vector2(290, 0);
		v.AddChild(hint);

		// Заполняющий пробел чтобы кнопка прижалась к низу.
		var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
		v.AddChild(spacer);

		var btn = new Button { Text = "Войти →" };
		UIStyle.StyleButton(btn, primary: true);
		btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		int captured = index;
		btn.Pressed += () => EmitSignal(SignalName.LocationChosen, captured);
		v.AddChild(btn);

		return card;
	}

	private void OnInventoryPressed()
	{
		if (_inventoryOverlay != null) return;
		_inventoryOverlay = new InventoryOverlay { ReadOnly = false };
		_inventoryOverlay.Closed += OnInventoryClosed;
		AddChild(_inventoryOverlay);
	}

	private void OnInventoryClosed()
	{
		if (_inventoryOverlay == null) return;
		RemoveChild(_inventoryOverlay);
		_inventoryOverlay.QueueFree();
		_inventoryOverlay = null;
	}
}
