using Godot;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Мини-панель одного врага в локации.
// Кликабельна когда Targetable=true (режим выбора цели).
// Подсветка наведения помогает понять кого выберешь.
public partial class EnemyView : PanelContainer
{
	[Signal]
	public delegate void EnemyClickedEventHandler(EnemyView view);

	public EnemyData Enemy;
	public bool Targetable;

	private Label _nameLabel;
	private ProgressBar _hpBar;
	private Label _hpLabel;
	private Label _intentLabel;
	private Label _blockLabel;
	private Label _effectsLabel;

	private Tween _flashTween;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(125, 200);
		MouseFilter = MouseFilterEnum.Stop;
		GuiInput += OnGuiInput;
		MouseEntered += () => ApplyStyle(true);
		MouseExited  += () => ApplyStyle(false);
		Resized += () => PivotOffset = Size / 2f;

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 4);
		v.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(v);

		_nameLabel = UIStyle.MakeLabel("", 13, UIStyle.TextPrimary);
		_nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(_nameLabel);

		_hpBar = new ProgressBar
		{
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 16),
		};
		UIStyle.StyleProgressBar(_hpBar, UIStyle.HpFill, UIStyle.HpEmpty);
		v.AddChild(_hpBar);

		_hpLabel = UIStyle.MakeLabel("", 11, UIStyle.TextPrimary);
		v.AddChild(_hpLabel);

		_intentLabel = UIStyle.MakeLabel("", 12, UIStyle.WarnAmber);
		_intentLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(_intentLabel);

		_blockLabel = UIStyle.MakeLabel("", 11, UIStyle.BlockCyan);
		v.AddChild(_blockLabel);

		_effectsLabel = UIStyle.MakeLabel("", 10, UIStyle.DangerRed);
		_effectsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(_effectsLabel);

		ApplyStyle(false);
	}

	public void SetEnemy(EnemyData e, bool targetable)
	{
		Enemy = e;
		Targetable = targetable;
		if (IsInsideTree())
		{
			Refresh();
			ApplyStyle(false);
		}
	}

	// Красная вспышка при попадании.
	public void Flash()
	{
		_flashTween?.Kill();
		_flashTween = CreateTween();
		_flashTween.TweenProperty(this, "modulate", new Color(1.6f, 0.5f, 0.5f), 0.06f);
		_flashTween.TweenProperty(this, "modulate", Modulate, 0.20f);
		_flashTween.Chain().TweenCallback(Callable.From(Refresh));
		// Лёгкий "удар" — масштаб
		var t2 = CreateTween().SetTrans(Tween.TransitionType.Cubic);
		t2.TweenProperty(this, "scale", new Vector2(1.08f, 0.94f), 0.07f);
		t2.TweenProperty(this, "scale", new Vector2(1.0f, 1.0f), 0.18f);
	}

	public void Refresh()
	{
		if (Enemy == null) return;
		bool dead = Enemy.CurrentHp <= 0;
		Modulate = dead ? new Color(0.4f, 0.4f, 0.4f, 0.6f) : new Color(1, 1, 1);

		_nameLabel.Text = dead ? $"{Enemy.EnemyName} 💀" : Enemy.EnemyName;
		_hpBar.MaxValue = Enemy.MaxHp;
		_hpBar.Value = Enemy.CurrentHp;
		_hpLabel.Text = $"ХП: {Enemy.CurrentHp}/{Enemy.MaxHp}";
		_intentLabel.Text = dead ? "—" : Enemy.DescribeIntent();
		_blockLabel.Text = Enemy.CurrentBlock > 0 ? $"🛡 {Enemy.CurrentBlock}" : "";
		_effectsLabel.Text = DescribeEffects(Enemy.Effects, Enemy.BleedStack);
	}

	private void ApplyStyle(bool hovered)
	{
		var sb = new StyleBoxFlat();
		bool dead = Enemy != null && Enemy.CurrentHp <= 0;

		if (dead)
		{
			sb.BgColor     = new Color(0.10f, 0.08f, 0.10f);
			sb.BorderColor = new Color(0.25f, 0.20f, 0.20f);
		}
		else if (Targetable && hovered)
		{
			sb.BgColor     = new Color(0.30f, 0.18f, 0.18f);
			sb.BorderColor = new Color(1.0f, 0.45f, 0.45f);
		}
		else if (Targetable)
		{
			sb.BgColor     = new Color(0.20f, 0.15f, 0.15f);
			sb.BorderColor = new Color(0.95f, 0.55f, 0.40f);
		}
		else
		{
			sb.BgColor     = new Color(0.16f, 0.12f, 0.16f);
			sb.BorderColor = new Color(0.40f, 0.32f, 0.30f);
		}
		sb.BorderWidthLeft = 2;
		sb.BorderWidthRight = 2;
		sb.BorderWidthTop = 2;
		sb.BorderWidthBottom = 2;
		sb.CornerRadiusTopLeft = 6;
		sb.CornerRadiusTopRight = 6;
		sb.CornerRadiusBottomLeft = 6;
		sb.CornerRadiusBottomRight = 6;
		sb.ContentMarginLeft = 8;
		sb.ContentMarginRight = 8;
		sb.ContentMarginTop = 8;
		sb.ContentMarginBottom = 8;
		AddThemeStyleboxOverride("panel", sb);
	}

	private void OnGuiInput(InputEvent ev)
	{
		if (Enemy == null || Enemy.CurrentHp <= 0 || !Targetable) return;
		if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			EmitSignal(SignalName.EnemyClicked, this);
	}

	private static string DescribeEffects(List<StatusEffect> effects, int bleedStack)
	{
		var parts = new List<string>();
		if (bleedStack > 0) parts.Add($"🩸 {bleedStack}");
		foreach (var e in effects)
		{
			parts.Add(e.Type switch
			{
				"phys_taken_pct" => $"AB+{(int)e.Amount}% ({e.Remaining})",
				_                => $"{e.Id} ({e.Remaining})",
			});
		}
		return parts.Count == 0 ? "" : string.Join(", ", parts);
	}
}
