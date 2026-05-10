using Godot;

// Визуальный контрол одной карты в руке.
// Сама карта не знает про бой — только сообщает "меня кликнули".
// Показывает конкретное число (Урон/Блок/...) для текущих статов и врага.
// Иконка ⓘ в углу — при наведении показывает формулу карты.
public partial class CardView : PanelContainer
{
	[Signal]
	public delegate void CardClickedEventHandler(CardView view);

	public string CardId = "";
	public CardData CardData;
	public bool Playable = true;

	private CharacterData _character;
	private EnemyData _enemy;

	private Label _nameLabel;
	private Label _costLabel;
	private Label _typeLabel;
	private Label _descLabel;
	private Label _infoLabel;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(155, 220);
		MouseFilter = MouseFilterEnum.Stop;
		GuiInput += OnGuiInput;
		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 6);
		v.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(v);

		var header = new HBoxContainer();
		header.MouseFilter = MouseFilterEnum.Ignore;
		header.AddThemeConstantOverride("separation", 4);
		v.AddChild(header);

		_nameLabel = new Label();
		_nameLabel.AddThemeFontSizeOverride("font_size", 14);
		_nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		header.AddChild(_nameLabel);

		// Иконка информации — захватывает мышь, чтобы показать тултип
		// и не передавать клик карте.
		_infoLabel = new Label { Text = "ⓘ" };
		_infoLabel.MouseFilter = MouseFilterEnum.Stop;
		_infoLabel.AddThemeFontSizeOverride("font_size", 14);
		_infoLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.75f, 1.0f));
		header.AddChild(_infoLabel);

		_costLabel = new Label();
		_costLabel.AddThemeFontSizeOverride("font_size", 16);
		_costLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.78f, 1.0f));
		header.AddChild(_costLabel);

		_typeLabel = new Label();
		_typeLabel.AddThemeFontSizeOverride("font_size", 11);
		v.AddChild(_typeLabel);

		v.AddChild(new HSeparator());

		_descLabel = new Label();
		_descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_descLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
		_descLabel.AddThemeFontSizeOverride("font_size", 13);
		_descLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_descLabel);

		ApplyStyle(false);
		Refresh();
	}

	public void SetCard(string id, CharacterData character, EnemyData enemy = null)
	{
		CardId = id;
		CardData = CardsDB.GetCard(id);
		_character = character;
		_enemy = enemy;
		if (IsInsideTree()) Refresh();
	}

	public void SetPlayable(bool p)
	{
		Playable = p;
		if (IsInsideTree()) ApplyStyle(false);
	}

	private void Refresh()
	{
		if (CardData == null) return;
		_nameLabel.Text = CardData.Name;
		_costLabel.Text = $"{CardData.Cost} MP";
		if (CardData.Type == "physical")
		{
			_typeLabel.Text = "[Физическая]";
			_typeLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.7f, 0.4f));
		}
		else
		{
			_typeLabel.Text = "[Магическая]";
			_typeLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.5f, 1.0f));
		}
		_descLabel.Text = CardsDB.DescribeCurrent(CardData, _character, _enemy);
		_infoLabel.TooltipText = CardsDB.DescribeFormula(CardData);
	}

	private void ApplyStyle(bool hovered)
	{
		var sb = new StyleBoxFlat();
		if (!Playable)
		{
			sb.BgColor     = new Color(0.13f, 0.12f, 0.16f);
			sb.BorderColor = new Color(0.35f, 0.18f, 0.18f);
			Modulate = new Color(0.55f, 0.55f, 0.55f);
		}
		else if (hovered)
		{
			sb.BgColor     = new Color(0.22f, 0.20f, 0.28f);
			sb.BorderColor = new Color(0.95f, 0.78f, 0.40f);
			Modulate = new Color(1, 1, 1);
		}
		else
		{
			sb.BgColor     = new Color(0.17f, 0.15f, 0.22f);
			sb.BorderColor = new Color(0.50f, 0.42f, 0.25f);
			Modulate = new Color(1, 1, 1);
		}
		sb.BorderWidthLeft = 2;
		sb.BorderWidthRight = 2;
		sb.BorderWidthTop = 2;
		sb.BorderWidthBottom = 2;
		sb.CornerRadiusTopLeft = 8;
		sb.CornerRadiusTopRight = 8;
		sb.CornerRadiusBottomLeft = 8;
		sb.CornerRadiusBottomRight = 8;
		sb.ContentMarginLeft = 8;
		sb.ContentMarginRight = 8;
		sb.ContentMarginTop = 8;
		sb.ContentMarginBottom = 8;
		AddThemeStyleboxOverride("panel", sb);
	}

	private void OnGuiInput(InputEvent ev)
	{
		if (!Playable) return;
		if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			EmitSignal(SignalName.CardClicked, this);
	}

	private void OnMouseEntered()
	{
		if (Playable) ApplyStyle(true);
	}

	private void OnMouseExited() => ApplyStyle(false);
}
