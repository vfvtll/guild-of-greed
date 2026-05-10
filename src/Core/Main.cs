using Godot;
using System;

// Корневой роутер. Решает что показать после старта:
//   - Есть сохранение → загружаем персонажа → Combat
//   - Нет сохранения → CharacterCreation → после конфирма сохраняем → Combat
//
// Также реагирует на запросы из Combat ("Новый персонаж") — удаляет сейв и
// возвращает игрока в CharacterCreation.
public partial class Main : Control
{
	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		StartFlow();
	}

	private void StartFlow()
	{
		if (SaveGame.HasSave())
		{
			var saved = SaveGame.Load();
			if (saved != null)
			{
				GameData.Instance.SetCharacter(saved);
				ShowCombat();
				return;
			}
		}
		ShowCharacterCreation();
	}

	private void ShowCharacterCreation()
	{
		ClearContent();
		var cc = new CharacterCreation();
		cc.Confirmed += OnCharacterConfirmed;
		AddChild(cc);
	}

	private void OnCharacterConfirmed(CharacterData character)
	{
		GameData.Instance.SetCharacter(character);
		SaveGame.Save(character);
		ShowCombat();
	}

	private void ShowCombat()
	{
		ClearContent();
		var combat = new Combat();
		combat.ResetCharacterRequested += OnResetCharacter;
		AddChild(combat);
	}

	private void OnResetCharacter()
	{
		SaveGame.Delete();
		GameData.Instance.SetCharacter(null);
		ShowCharacterCreation();
	}

	private void ClearContent()
	{
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}
	}
}
