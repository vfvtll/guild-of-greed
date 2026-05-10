using Godot;

// Единая палитра + фабрики стилей для UI боя.
// Используется в Combat.cs и EnemyView.cs.
public static class UIStyle
{
	// =====================================================================
	// Палитра
	// =====================================================================
	public static readonly Color BgDeep        = new(0.07f, 0.06f, 0.10f);
	public static readonly Color PanelBg       = new(0.13f, 0.11f, 0.18f);
	public static readonly Color PanelBgLight  = new(0.17f, 0.15f, 0.22f);
	public static readonly Color PanelBgDeep   = new(0.09f, 0.08f, 0.13f);

	public static readonly Color GoldDark      = new(0.40f, 0.32f, 0.20f);
	public static readonly Color GoldMid       = new(0.65f, 0.50f, 0.28f);
	public static readonly Color GoldBright    = new(0.95f, 0.82f, 0.45f);

	public static readonly Color TextPrimary   = new(0.95f, 0.92f, 0.85f);
	public static readonly Color TextDim       = new(0.62f, 0.58f, 0.55f);
	public static readonly Color TextSecondary = new(0.85f, 0.78f, 0.65f);

	public static readonly Color HpFill        = new(0.85f, 0.22f, 0.30f);
	public static readonly Color HpEmpty       = new(0.22f, 0.08f, 0.10f);
	public static readonly Color MpFill        = new(0.30f, 0.55f, 0.95f);
	public static readonly Color MpEmpty       = new(0.07f, 0.12f, 0.22f);

	public static readonly Color BlockCyan     = new(0.55f, 0.85f, 1.00f);
	public static readonly Color HealGreen     = new(0.55f, 1.00f, 0.65f);
	public static readonly Color WarnAmber     = new(1.00f, 0.85f, 0.40f);
	public static readonly Color DangerRed     = new(1.00f, 0.45f, 0.45f);
	public static readonly Color OutlineBlack  = new(0, 0, 0);

	// =====================================================================
	// Панели
	// =====================================================================
	public static StyleBoxFlat PanelStyle(Color? bg = null, Color? border = null)
	{
		return new StyleBoxFlat
		{
			BgColor = bg ?? PanelBg,
			BorderColor = border ?? GoldDark,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			BorderWidthTop = 2,  BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			ContentMarginLeft = 14, ContentMarginRight = 14,
			ContentMarginTop = 12, ContentMarginBottom = 12,
			ShadowColor = new Color(0, 0, 0, 0.55f),
			ShadowSize = 6,
			ShadowOffset = new Vector2(0, 4),
		};
	}

	public static StyleBoxFlat MiniPanelStyle(Color? bg = null, Color? border = null)
	{
		return new StyleBoxFlat
		{
			BgColor = bg ?? PanelBgDeep,
			BorderColor = border ?? GoldDark,
			BorderWidthLeft = 1, BorderWidthRight = 1,
			BorderWidthTop = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 10, ContentMarginRight = 10,
			ContentMarginTop = 5, ContentMarginBottom = 5,
		};
	}

	public static StyleBoxFlat BannerStyle(Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.20f, 0.14f, 0.08f),
			BorderColor = border,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			BorderWidthTop = 2, BorderWidthBottom = 2,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			ContentMarginLeft = 16, ContentMarginRight = 16,
			ContentMarginTop = 8, ContentMarginBottom = 8,
			ShadowColor = new Color(0, 0, 0, 0.6f),
			ShadowSize = 8,
			ShadowOffset = new Vector2(0, 2),
		};
	}

	// =====================================================================
	// Кнопки
	// =====================================================================
	public static void StyleButton(Button btn, bool primary = false)
	{
		Color bgN, bgH, bgP, borderN, borderH;
		if (primary)
		{
			bgN = new Color(0.32f, 0.20f, 0.08f);
			bgH = new Color(0.52f, 0.32f, 0.12f);
			bgP = new Color(0.20f, 0.12f, 0.05f);
			borderN = GoldMid;
			borderH = GoldBright;
		}
		else
		{
			bgN = new Color(0.17f, 0.14f, 0.20f);
			bgH = new Color(0.25f, 0.20f, 0.30f);
			bgP = new Color(0.10f, 0.08f, 0.13f);
			borderN = GoldDark;
			borderH = GoldMid;
		}
		var bgD = new Color(0.10f, 0.09f, 0.13f);

		btn.AddThemeStyleboxOverride("normal",   BtnStyle(bgN, borderN));
		btn.AddThemeStyleboxOverride("hover",    BtnStyle(bgH, borderH));
		btn.AddThemeStyleboxOverride("pressed",  BtnStyle(bgP, borderN));
		btn.AddThemeStyleboxOverride("disabled", BtnStyle(bgD, GoldDark * 0.5f, 1));
		btn.AddThemeStyleboxOverride("focus",    BtnStyle(bgN, GoldBright));
		btn.AddThemeColorOverride("font_color",          TextPrimary);
		btn.AddThemeColorOverride("font_hover_color",    GoldBright);
		btn.AddThemeColorOverride("font_pressed_color",  TextDim);
		btn.AddThemeColorOverride("font_disabled_color", TextDim * 0.7f);
		btn.AddThemeColorOverride("font_focus_color",    GoldBright);
		btn.AddThemeColorOverride("font_outline_color",  OutlineBlack);
		btn.AddThemeConstantOverride("outline_size", 2);
		btn.AddThemeFontSizeOverride("font_size", primary ? 16 : 13);
	}

	private static StyleBoxFlat BtnStyle(Color bg, Color border, int borderW = 2)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = borderW, BorderWidthRight = borderW,
			BorderWidthTop = borderW, BorderWidthBottom = borderW,
			CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
			ContentMarginLeft = 14, ContentMarginRight = 14,
			ContentMarginTop = 7, ContentMarginBottom = 7,
		};
	}

	// =====================================================================
	// Прогресс-бары (HP / MP / etc.)
	// =====================================================================
	public static void StyleProgressBar(ProgressBar bar, Color fill, Color empty)
	{
		var bgStyle = new StyleBoxFlat
		{
			BgColor = empty,
			BorderColor = empty.Darkened(0.3f),
			BorderWidthLeft = 1, BorderWidthRight = 1,
			BorderWidthTop = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
		};
		var fillStyle = new StyleBoxFlat
		{
			BgColor = fill,
			BorderColor = fill.Lightened(0.25f),
			BorderWidthLeft = 1, BorderWidthRight = 1,
			BorderWidthTop = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
		};
		bar.AddThemeStyleboxOverride("background", bgStyle);
		bar.AddThemeStyleboxOverride("fill", fillStyle);
		bar.Modulate = new Color(1, 1, 1);
	}

	// =====================================================================
	// Лейблы
	// =====================================================================
	public static Label MakeSectionTitle(string text)
	{
		var l = new Label { Text = text };
		l.AddThemeFontSizeOverride("font_size", 17);
		l.AddThemeColorOverride("font_color", GoldBright);
		l.AddThemeColorOverride("font_outline_color", OutlineBlack);
		l.AddThemeConstantOverride("outline_size", 3);
		return l;
	}

	public static Label MakeLabel(string text, int fontSize, Color color)
	{
		var l = new Label { Text = text };
		l.AddThemeFontSizeOverride("font_size", fontSize);
		l.AddThemeColorOverride("font_color", color);
		l.AddThemeColorOverride("font_outline_color", OutlineBlack);
		l.AddThemeConstantOverride("outline_size", 2);
		return l;
	}

	public static void ApplyOutline(Label l, int size = 2)
	{
		l.AddThemeColorOverride("font_outline_color", OutlineBlack);
		l.AddThemeConstantOverride("outline_size", size);
	}
}
