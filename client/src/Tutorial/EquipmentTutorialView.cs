using System;
using Godot;

// Простой пост-бой туториал по экипировке. Показывается один раз после
// первого стартового боя (CreateTrainingDummy) и объясняет как открыть
// инвентарь и одеть выпавший меч и броню.
//
// Сигналы:
//   OpenInventoryRequested — игрок нажал "Открыть инвентарь".
//   SkipRequested          — игрок нажал "Пропустить".
//
// Main подписывается, открывает InventoryOverlay (или сразу LocationSelect
// если Skip), и при закрытии инвентаря переходит в LocationSelect.
public partial class EquipmentTutorialView : Control
{
	[Signal] public delegate void OpenInventoryRequestedEventHandler();
	[Signal] public delegate void SkipRequestedEventHandler();

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
	}

	private void BuildUI()
	{
		var bg = new ColorRect { Color = UIStyle.BgDeep };
		UIStyle.FillParent(bg);
		bg.MouseFilter = MouseFilterEnum.Stop;
		AddChild(bg);

		var panel = new PanelContainer
		{
			Position = new Vector2(290, 100),
			CustomMinimumSize = new Vector2(700, 480),
		};
		panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 14);
		panel.AddChild(v);

		var title = UIStyle.MakeLabel("🎉 Победа! Первая добыча", 26, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(title);

		var sub = UIStyle.MakeLabel(
			"С волка выпал короткий меч и комплект кожаной брони.",
			14, UIStyle.TextSecondary);
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(sub);

		v.AddChild(new HSeparator());

		var rich = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			CustomMinimumSize = new Vector2(640, 260),
			Text =
				"Откройте инвентарь и наденьте новые вещи:\n\n" +
				"• [b]Меч[/b] — даёт +ФизАтк и +1 карту в начале хода.\n" +
				"  Без оружия крит не срабатывает.\n\n" +
				"• [b]Кожаная броня[/b] (грудь / шлем / перчатки / сапоги) —\n" +
				"  даёт ФизЗащ и небольшие бонусы к ХП и атаке.\n\n" +
				"• [b]Амулет[/b] — дополнительный слот, можно занять\n" +
				"  без свапа основной брони.\n\n" +
				"Клик по предмету в инвентаре — надеть.\n" +
				"Клик по уже надетому слоту слева — снять обратно.",
		};
		rich.AddThemeColorOverride("default_color", UIStyle.TextPrimary);
		v.AddChild(rich);

		v.AddChild(new HSeparator());

		var btnBar = new HBoxContainer();
		btnBar.AddThemeConstantOverride("separation", 16);
		btnBar.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(btnBar);

		var skipBtn = new Button { Text = "Пропустить" };
		UIStyle.StyleButton(skipBtn);
		skipBtn.Pressed += () => EmitSignal(SignalName.SkipRequested);
		btnBar.AddChild(skipBtn);

		var openBtn = new Button { Text = "🎒 Открыть инвентарь" };
		UIStyle.StyleButton(openBtn, primary: true);
		openBtn.Pressed += () => EmitSignal(SignalName.OpenInventoryRequested);
		btnBar.AddChild(openBtn);
	}
}
