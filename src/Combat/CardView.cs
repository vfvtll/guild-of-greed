using Godot;

// Визуальный контрол одной карты в руке.
// Стиль зависит от архетипа: воин — тёплые тона, маг — холодные.
// При наведении карта поднимается и слегка масштабируется.
// При выборе цели — жёлтая рамка + свечение.
public partial class CardView : PanelContainer
{
	[Signal]
	public delegate void CardClickedEventHandler(CardView view);

	public string CardId = "";
	public CardData CardData;
	public bool Playable = true;
	public bool Selected = false;

	private CharacterData _character;
	private EnemyData _enemy;

	private Label _nameLabel;
	private Label _costLabel;
	private Label _typeLabel;
	private Label _descLabel;
	private Label _infoLabel;
	private Label _iconLabel;

	private Tween _scaleTween;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(155, 220);
		MouseFilter = MouseFilterEnum.Stop;
		GuiInput += OnGuiInput;
		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
		Resized += () => PivotOffset = Size / 2f;

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 4);
		v.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(v);

		// === Header: название | ⓘ | стоимость ===
		var header = new HBoxContainer();
		header.MouseFilter = MouseFilterEnum.Ignore;
		header.AddThemeConstantOverride("separation", 4);
		v.AddChild(header);

		_nameLabel = new Label();
		_nameLabel.AddThemeFontSizeOverride("font_size", 14);
		_nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_nameLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.85f));
		_nameLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		_nameLabel.AddThemeConstantOverride("outline_size", 3);
		header.AddChild(_nameLabel);

		_infoLabel = new Label { Text = "ⓘ" };
		_infoLabel.MouseFilter = MouseFilterEnum.Stop;
		_infoLabel.AddThemeFontSizeOverride("font_size", 14);
		_infoLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.75f, 1.0f));
		header.AddChild(_infoLabel);

		_costLabel = new Label();
		_costLabel.AddThemeFontSizeOverride("font_size", 16);
		_costLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 1.0f));
		_costLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		_costLabel.AddThemeConstantOverride("outline_size", 3);
		header.AddChild(_costLabel);

		// === Подпись типа ===
		_typeLabel = new Label();
		_typeLabel.AddThemeFontSizeOverride("font_size", 11);
		_typeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_typeLabel);

		v.AddChild(new HSeparator());

		// === Большая иконка по центру ===
		_iconLabel = new Label();
		_iconLabel.AddThemeFontSizeOverride("font_size", 40);
		_iconLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_iconLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
		_iconLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_iconLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		_iconLabel.AddThemeConstantOverride("outline_size", 6);
		v.AddChild(_iconLabel);

		v.AddChild(new HSeparator());

		// === Конкретное число эффекта ===
		_descLabel = new Label();
		_descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_descLabel.AddThemeFontSizeOverride("font_size", 12);
		_descLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_descLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.8f));
		_descLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		_descLabel.AddThemeConstantOverride("outline_size", 2);
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
		if (IsInsideTree())
		{
			Refresh();
			ApplyStyle(false);
		}
	}

	public void SetPlayable(bool p)
	{
		Playable = p;
		if (IsInsideTree()) ApplyStyle(false);
	}

	public void SetSelected(bool s)
	{
		Selected = s;
		if (IsInsideTree())
		{
			ApplyStyle(false);
			AnimateScale(s ? 1.04f : 1.0f);
		}
	}

	private void Refresh()
	{
		if (CardData == null) return;
		_nameLabel.Text = CardData.Name;
		_costLabel.Text = $"{CardData.Cost}MP";
		_iconLabel.Text = CardData.Icon ?? "?";
		_iconLabel.AddThemeColorOverride("font_color", ArchetypeAccentColor());

		if (CardData.Type == "physical")
		{
			_typeLabel.Text = "Физическая";
			_typeLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.7f, 0.4f));
		}
		else
		{
			_typeLabel.Text = "Магическая";
			_typeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.6f, 1.0f));
		}

		_descLabel.Text = CardsDB.DescribeCurrent(CardData, _character, _enemy);
		_infoLabel.TooltipText = CardsDB.DescribeFormula(CardData);
	}

	private Color ArchetypeAccentColor() => CardData?.Archetype switch
	{
		"warrior" => new Color(1.0f, 0.70f, 0.35f),
		"mage"    => new Color(0.75f, 0.55f, 1.0f),
		_         => new Color(0.85f, 0.78f, 0.55f),
	};

	private (Color bg, Color border) ArchetypePalette() => CardData?.Archetype switch
	{
		"warrior" => (new Color(0.20f, 0.10f, 0.10f), new Color(0.65f, 0.35f, 0.20f)),
		"mage"    => (new Color(0.10f, 0.10f, 0.22f), new Color(0.40f, 0.35f, 0.70f)),
		_         => (new Color(0.15f, 0.13f, 0.18f), new Color(0.45f, 0.38f, 0.28f)),
	};

	private void ApplyStyle(bool hovered)
	{
		var sb = new StyleBoxFlat();
		var (bgBase, borderBase) = ArchetypePalette();
		var accent = ArchetypeAccentColor();

		if (!Playable)
		{
			sb.BgColor = bgBase * 0.7f;
			sb.BorderColor = borderBase * 0.6f;
			Modulate = new Color(0.55f, 0.55f, 0.55f);
		}
		else if (Selected)
		{
			sb.BgColor = bgBase.Lerp(accent, 0.18f);
			sb.BorderColor = new Color(1.0f, 0.85f, 0.30f);
			Modulate = new Color(1.05f, 1.05f, 1.05f);
		}
		else if (hovered)
		{
			sb.BgColor = bgBase.Lerp(accent, 0.12f);
			sb.BorderColor = accent;
			Modulate = new Color(1, 1, 1);
		}
		else
		{
			sb.BgColor = bgBase;
			sb.BorderColor = borderBase;
			Modulate = new Color(1, 1, 1);
		}

		int borderW = Selected ? 4 : (hovered ? 3 : 2);
		sb.BorderWidthLeft = borderW;
		sb.BorderWidthRight = borderW;
		sb.BorderWidthTop = borderW;
		sb.BorderWidthBottom = borderW;
		sb.CornerRadiusTopLeft = 10;
		sb.CornerRadiusTopRight = 10;
		sb.CornerRadiusBottomLeft = 10;
		sb.CornerRadiusBottomRight = 10;
		sb.ContentMarginLeft = 10;
		sb.ContentMarginRight = 10;
		sb.ContentMarginTop = 10;
		sb.ContentMarginBottom = 10;
		// Тень для глубины
		sb.ShadowColor = new Color(0, 0, 0, 0.5f);
		sb.ShadowSize = hovered ? 8 : 5;
		sb.ShadowOffset = new Vector2(2, 4);

		AddThemeStyleboxOverride("panel", sb);
	}

	private void AnimateScale(float target)
	{
		_scaleTween?.Kill();
		_scaleTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_scaleTween.TweenProperty(this, "scale", new Vector2(target, target), 0.12f);
	}

	private void OnGuiInput(InputEvent ev)
	{
		if (!Playable) return;
		if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			EmitSignal(SignalName.CardClicked, this);
	}

	private void OnMouseEntered()
	{
		if (!Playable) return;
		ApplyStyle(true);
		ZIndex = 10;
		AnimateScale(1.07f);
	}

	private void OnMouseExited()
	{
		ApplyStyle(false);
		ZIndex = 0;
		AnimateScale(Selected ? 1.04f : 1.0f);
	}
}
