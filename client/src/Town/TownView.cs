using Godot;
using GuildOfGreed.Shared.Domain;

// Городской хаб. Открывается из LocationSelectView. Содержит:
//   - Стэш (хранилище предметов между забегами)
//   - Лавку (купить зелья / продать предметы)
//   - Кнопку возврата к выбору подземелья
//
// Все городские действия — клиентские. Сервер увидит изменения при
// следующем сохранении персонажа (после боя). См. GameData комментарий.
public partial class TownView : Control
{
	[Signal] public delegate void LeaveTownRequestedEventHandler();

	private StashOverlay _stashOverlay;
	private ShopOverlay _shopOverlay;
	private Label _kosheLabel;

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

		var backBtn = new Button { Text = "⚔ В подземелье" };
		UIStyle.StyleButton(backBtn);
		backBtn.Pressed += () => EmitSignal(SignalName.LeaveTownRequested);
		top.AddChild(backBtn);

		// === Player snippet (HP/MP/Кошель) ===
		var p = GameData.Instance.Character;
		if (p != null)
		{
			var hp = UIStyle.MakeLabel(
				$"{p.CharacterName}   ❤ {p.CurrentHp}/{p.MaxHp()}   ✦ {p.CurrentMp}/{p.MaxMp()}",
				14, UIStyle.TextPrimary);
			hp.Position = new Vector2(420, 22);
			AddChild(hp);
		}

		// Кошель — отдельной строкой, обновляем при возврате из оверлеев.
		_kosheLabel = UIStyle.MakeLabel("", 14, UIStyle.GoldBright);
		_kosheLabel.Position = new Vector2(420, 44);
		AddChild(_kosheLabel);
		RefreshKoshel();

		// === Title ===
		var title = UIStyle.MakeLabel("🏛  Город", 32, UIStyle.GoldBright);
		title.Position = new Vector2(560, 80);
		AddChild(title);

		var sub = UIStyle.MakeLabel(
			"Спокойное место между забегами. Сюда можно убрать лишний лут и закупиться зельями.",
			14, UIStyle.TextSecondary);
		sub.Position = new Vector2(420, 130);
		AddChild(sub);

		// === Карточки заведений ===
		var cardsRow = new HBoxContainer
		{
			Position = new Vector2(200, 220),
			Size = new Vector2(880, 360),
		};
		cardsRow.AddThemeConstantOverride("separation", 32);
		cardsRow.Alignment = BoxContainer.AlignmentMode.Center;
		AddChild(cardsRow);

		cardsRow.AddChild(MakeBuildingCard(
			"🏦", "Стэш",
			"Хранилище предметов между забегами.\n50 ячеек. Бесплатно.",
			OnStashPressed));

		cardsRow.AddChild(MakeBuildingCard(
			"🛒", "Лавка",
			"Продай лишнее, купи зелья.\nЦена выкупа — 40% от полной.",
			OnShopPressed));
	}

	private PanelContainer MakeBuildingCard(string icon, string name, string hint, System.Action onEnter)
	{
		var card = new PanelContainer { CustomMinimumSize = new Vector2(360, 340) };
		card.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 12);
		card.AddChild(v);

		var iconL = UIStyle.MakeLabel(icon, 56, UIStyle.GoldBright);
		iconL.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(iconL);

		v.AddChild(UIStyle.MakeSectionTitle(name));

		var hintL = UIStyle.MakeLabel(hint, 13, UIStyle.TextSecondary);
		hintL.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		hintL.CustomMinimumSize = new Vector2(320, 0);
		v.AddChild(hintL);

		var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
		v.AddChild(spacer);

		var btn = new Button { Text = $"Войти в {name.ToLower()} →" };
		UIStyle.StyleButton(btn, primary: true);
		btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		btn.Pressed += () => onEnter();
		v.AddChild(btn);

		return card;
	}

	private void OnStashPressed()
	{
		if (_stashOverlay != null) return;
		_stashOverlay = new StashOverlay();
		_stashOverlay.Closed += OnStashClosed;
		AddChild(_stashOverlay);
	}

	private void OnStashClosed()
	{
		if (_stashOverlay == null) return;
		RemoveChild(_stashOverlay);
		_stashOverlay.QueueFree();
		_stashOverlay = null;
		RefreshKoshel();
	}

	private void OnShopPressed()
	{
		if (_shopOverlay != null) return;
		_shopOverlay = new ShopOverlay();
		_shopOverlay.Closed += OnShopClosed;
		AddChild(_shopOverlay);
	}

	private void OnShopClosed()
	{
		if (_shopOverlay == null) return;
		RemoveChild(_shopOverlay);
		_shopOverlay.QueueFree();
		_shopOverlay = null;
		RefreshKoshel();
	}

	private void RefreshKoshel()
	{
		var ch = GameData.Instance.Character;
		long money = ch?.Inventory?.Money ?? 0;
		_kosheLabel.Text = $"🪙 Кошель:  {Currency.FormatShort(money)}";
	}
}
