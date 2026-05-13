using Godot;
using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

// Окно выбора эффекта подземелья после победы. Показывает 3 варианта,
// игрок кликает — эмитит Chosen(effectId), оверлей закрывается. После этого
// Combat включает кнопку "Переход на карту".
//
// Эффекты — это RunEffect (см. shared/Domain/RunEffect.cs); содержат
// имя/описание/Kind/Magnitude. Здесь только UI; resolved выбор Combat
// добавляет в RunMap.ActiveEffects.
public partial class RunEffectChoiceOverlay : Control
{
	[Signal] public delegate void ChosenEventHandler(string effectId);

	private readonly List<RunEffect> _choices;
	private PanelContainer _panel;
	private ColorRect _dim;

	public RunEffectChoiceOverlay(List<RunEffect> choices)
	{
		_choices = choices ?? new List<RunEffect>();
	}

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
		PlayOpenAnimation();
	}

	private void BuildUI()
	{
		_dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		_dim.SetAnchorsPreset(LayoutPreset.FullRect);
		_dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(_dim);

		_panel = new PanelContainer
		{
			Position = new Vector2(140, 100),
			Size = new Vector2(1000, 480),
			CustomMinimumSize = new Vector2(1000, 480),
		};
		_panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(_panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 16);
		_panel.AddChild(v);

		var title = UIStyle.MakeLabel("⚗ Эффект подземелья", 26, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(title);

		var sub = UIStyle.MakeLabel(
			"Выберите один эффект — он будет действовать до конца забега.",
			14, UIStyle.TextSecondary);
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(sub);

		v.AddChild(new HSeparator());

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.SizeFlagsVertical = SizeFlags.ExpandFill;
		v.AddChild(row);

		foreach (var eff in _choices)
			row.AddChild(MakeEffectCard(eff));
	}

	private PanelContainer MakeEffectCard(RunEffect eff)
	{
		var card = new PanelContainer
		{
			CustomMinimumSize = new Vector2(300, 340),
		};
		card.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 10);
		card.AddChild(col);

		col.AddChild(UIStyle.MakeSectionTitle(eff.Name));

		var desc = UIStyle.MakeLabel(eff.Description, 13, UIStyle.TextPrimary);
		desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		desc.CustomMinimumSize = new Vector2(260, 0);
		col.AddChild(desc);

		var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
		col.AddChild(spacer);

		var btn = new Button { Text = "Выбрать" };
		UIStyle.StyleButton(btn, primary: true);
		btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		string capturedId = eff.Id;
		btn.Pressed += () => EmitSignal(SignalName.Chosen, capturedId);
		col.AddChild(btn);

		return card;
	}

	private void PlayOpenAnimation()
	{
		_panel.Modulate = new Color(1, 1, 1, 0);
		_dim.Modulate = new Color(1, 1, 1, 0);
		var t = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(_panel, "modulate:a", 1f, 0.22f);
		t.TweenProperty(_dim, "modulate:a", 1f, 0.22f);
	}
}
