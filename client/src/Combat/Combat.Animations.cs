using Godot;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Combat — анимации боя: всплывающий текст, вылет карты, поиск view врага.
public partial class Combat
{
	// Всплывающий текст (урон, исцеление, блок) — поднимается и тает.
	private void SpawnFloatingText(Vector2 globalPos, string text, Color color, int fontSize)
	{
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		label.AddThemeConstantOverride("outline_size", 5);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.MouseFilter = MouseFilterEnum.Ignore;
		label.ZIndex = 100;
		label.CustomMinimumSize = new Vector2(80, 0);
		AddChild(label);
		label.GlobalPosition = globalPos - new Vector2(40, 0);

		var t = CreateTween().SetParallel(true);
		t.TweenProperty(label, "position:y", label.Position.Y - 55, 0.75f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(label, "modulate:a", 0f, 0.75f).SetDelay(0.25f);
		t.Chain().TweenCallback(Callable.From(label.QueueFree));
	}

	// Карта вылетает из руки и тает.
	private void AnimateCardOut(CardView view)
	{
		if (view == null || !GodotObject.IsInstanceValid(view)) return;
		var globalPos = view.GlobalPosition;
		view.GetParent()?.RemoveChild(view);
		AddChild(view);
		view.GlobalPosition = globalPos;
		view.MouseFilter = MouseFilterEnum.Ignore;
		view.ZIndex = 50;

		var t = CreateTween().SetParallel(true);
		t.TweenProperty(view, "position:y", view.Position.Y - 60, 0.35f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(view, "modulate:a", 0.0f, 0.35f);
		t.TweenProperty(view, "scale", new Vector2(1.15f, 1.15f), 0.35f);
		t.Chain().TweenCallback(Callable.From(view.QueueFree));
	}

	private EnemyView FindEnemyView(EnemyData enemy)
	{
		foreach (Node child in _enemyArea.GetChildren())
			if (child is EnemyView ev && ev.Enemy == enemy) return ev;
		return null;
	}
}
