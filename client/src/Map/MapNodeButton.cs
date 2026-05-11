using Godot;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Кнопка одного узла на карте подземелья.
// Визуал зависит от состояния: Visited (тусклый), Current (золотая рамка),
// Available (можно кликнуть, акцентная рамка), Locked (затемнён, не кликается).
public partial class MapNodeButton : Button
{
	public MapNode Node { get; }

	public enum NodeStatus
	{
		Locked,     // Не доступен, виден тусклым.
		Available,  // Можно перейти отсюда.
		Current,    // Текущая позиция игрока (только что завершённый узел).
		Visited,    // Пройден ранее.
	}

	public MapNodeButton(MapNode node)
	{
		Node = node;
		Text = IconFor(node.Type);
		AddThemeFontSizeOverride("font_size", 26);
		AddThemeColorOverride("font_outline_color", UIStyle.OutlineBlack);
		AddThemeConstantOverride("outline_size", 3);
		MouseFilter = MouseFilterEnum.Stop;
		TooltipText = TooltipFor(node.Type);
	}

	public void ApplyStatus(NodeStatus status)
	{
		Disabled = status != NodeStatus.Available;

		Color border, bg, font;
		switch (status)
		{
			case NodeStatus.Available:
				border = UIStyle.GoldBright;
				bg     = new Color(0.18f, 0.14f, 0.09f);
				font   = UIStyle.GoldBright;
				break;
			case NodeStatus.Current:
				border = UIStyle.HealGreen;
				bg     = new Color(0.10f, 0.20f, 0.10f);
				font   = UIStyle.HealGreen;
				break;
			case NodeStatus.Visited:
				border = UIStyle.GoldDark;
				bg     = new Color(0.10f, 0.09f, 0.13f);
				font   = UIStyle.TextDim;
				break;
			default: // Locked
				border = UIStyle.GoldDark * 0.4f;
				bg     = new Color(0.07f, 0.06f, 0.10f);
				font   = UIStyle.TextDim * 0.7f;
				break;
		}

		AddThemeStyleboxOverride("normal",   NodeStyle(bg, border));
		AddThemeStyleboxOverride("hover",    NodeStyle(bg.Lightened(0.1f), border));
		AddThemeStyleboxOverride("pressed",  NodeStyle(bg.Darkened(0.2f), border));
		AddThemeStyleboxOverride("disabled", NodeStyle(bg, border));
		AddThemeStyleboxOverride("focus",    NodeStyle(bg, UIStyle.GoldBright));
		AddThemeColorOverride("font_color",          font);
		AddThemeColorOverride("font_hover_color",    UIStyle.GoldBright);
		AddThemeColorOverride("font_disabled_color", font);
	}

	private static StyleBoxFlat NodeStyle(Color bg, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 3, BorderWidthRight = 3,
			BorderWidthTop = 3, BorderWidthBottom = 3,
			CornerRadiusTopLeft = 28, CornerRadiusTopRight = 28,
			CornerRadiusBottomLeft = 28, CornerRadiusBottomRight = 28,
			ContentMarginLeft = 4, ContentMarginRight = 4,
			ContentMarginTop = 4, ContentMarginBottom = 4,
		};
	}

	private static string IconFor(MapNodeType type) => type switch
	{
		MapNodeType.Battle => "⚔",
		MapNodeType.Elite  => "☠",
		MapNodeType.Rest   => "🔥",
		MapNodeType.Chest  => "📦",
		MapNodeType.Event  => "❓",
		MapNodeType.Boss   => "👑",
		_                  => "?",
	};

	private static string TooltipFor(MapNodeType type) => type switch
	{
		MapNodeType.Battle => "Бой с врагами",
		MapNodeType.Elite  => "Элитный противник — больше лута",
		MapNodeType.Rest   => "Отдых: восстановит часть ХП",
		MapNodeType.Chest  => "Сундук с гарантированным лутом",
		MapNodeType.Event  => "Случайное событие",
		MapNodeType.Boss   => "Босс подземелья",
		_                  => "",
	};
}
