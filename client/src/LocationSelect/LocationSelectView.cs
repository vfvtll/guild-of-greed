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
	[Signal] public delegate void TownRequestedEventHandler();

	private InventoryOverlay _inventoryOverlay;

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		BuildUI();
	}

	private void BuildUI()
	{
		var bg = new ColorRect { Color = UIStyle.BgDeep };
		UIStyle.FillParent(bg);
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

		var townBtn = new Button { Text = "🏛 Город" };
		UIStyle.StyleButton(townBtn);
		townBtn.Pressed += () => EmitSignal(SignalName.TownRequested);
		top.AddChild(townBtn);

		var resetBtn = new Button { Text = "👤 Новый персонаж" };
		UIStyle.StyleButton(resetBtn);
		resetBtn.Pressed += () => EmitSignal(SignalName.ResetCharacterRequested);
		top.AddChild(resetBtn);

		// === Player snippet ===
		var p = GameData.Instance.Character;
		if (p != null)
		{
			var hp = UIStyle.MakeLabel(
				$"{p.CharacterName}   ❤ {p.CurrentHp}/{p.MaxHp()}   ✦ {p.CurrentMp}/{p.MaxMp()}   ⚔ {p.Weapon?.Name ?? "—"}",
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
		// 5 локаций не помещаются в один ряд — раскладываем сеткой 3 в ряд.
		// HFlowContainer переносит карточки на следующую строку автоматически.
		var cardsRow = new HFlowContainer
		{
			Position = new Vector2(60, 200),
			Size = new Vector2(1160, 760),
		};
		cardsRow.AddThemeConstantOverride("h_separation", 18);
		cardsRow.AddThemeConstantOverride("v_separation", 18);
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
			CustomMinimumSize = new Vector2(360, 220),
		};
		card.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 8);
		card.AddChild(v);

		v.AddChild(UIStyle.MakeSectionTitle(GameData.LocationNames[index]));

		// Требование уровня — показываем только если оно есть (>1).
		// Если игрок не дорос, подсвечиваем красным и блокируем кнопку.
		var ch = GameData.Instance.Character;
		int requiredLevel = index < GameData.LocationRequiredLevel.Length
			? GameData.LocationRequiredLevel[index] : 1;
		bool locked = ch != null && requiredLevel > 1 && ch.Level < requiredLevel;
		if (requiredLevel > 1)
		{
			var color = locked ? UIStyle.DangerRed : UIStyle.WarnAmber;
			var req = UIStyle.MakeLabel($"Требуется уровень: {requiredLevel}", 13, color);
			v.AddChild(req);
		}

		var hint = UIStyle.MakeLabel(GameData.LocationHints[index], 13, UIStyle.TextSecondary);
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		hint.CustomMinimumSize = new Vector2(330, 0);
		v.AddChild(hint);

		// Заполняющий пробел чтобы кнопка прижалась к низу.
		var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
		v.AddChild(spacer);

		var btn = new Button { Text = locked ? $"🔒 Уровень < {requiredLevel}" : "Войти →" };
		UIStyle.StyleButton(btn, primary: !locked);
		btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		btn.Disabled = locked;
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
