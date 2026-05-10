using Godot;
using System.Collections.Generic;

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

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(125, 200);
		MouseFilter = MouseFilterEnum.Stop;
		GuiInput += OnGuiInput;
		MouseEntered += () => ApplyStyle(true);
		MouseExited  += () => ApplyStyle(false);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 4);
		v.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(v);

		_nameLabel = new Label();
		_nameLabel.AddThemeFontSizeOverride("font_size", 13);
		_nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(_nameLabel);

		_hpBar = new ProgressBar
		{
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 14),
			Modulate = new Color(0.85f, 0.25f, 0.30f),
		};
		v.AddChild(_hpBar);

		_hpLabel = new Label();
		_hpLabel.AddThemeFontSizeOverride("font_size", 11);
		v.AddChild(_hpLabel);

		_intentLabel = new Label();
		_intentLabel.AddThemeFontSizeOverride("font_size", 12);
		_intentLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.4f));
		_intentLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(_intentLabel);

		_blockLabel = new Label();
		_blockLabel.AddThemeFontSizeOverride("font_size", 11);
		v.AddChild(_blockLabel);

		_effectsLabel = new Label();
		_effectsLabel.AddThemeFontSizeOverride("font_size", 10);
		_effectsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_effectsLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.55f, 0.55f));
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
		_effectsLabel.Text = DescribeEffects(Enemy.Effects);
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

	private static string DescribeEffects(List<StatusEffect> effects)
	{
		if (effects.Count == 0) return "";
		var parts = new List<string>();
		foreach (var e in effects)
		{
			parts.Add(e.Type switch
			{
				"phys_taken_pct" => $"AB+{(int)e.Amount}% ({e.Remaining})",
				_                => $"{e.Id} ({e.Remaining})",
			});
		}
		return string.Join(", ", parts);
	}
}
