using Godot;
using System;

// Корневой роутер. Состояния:
//   Нет сейва        → CharacterCreation → (создал) → LocationSelect
//   Есть сейв        → LocationSelect
//   LocationSelect   → (выбрал локацию)  → StartRun → MapView
//   MapView          → (клик доступного узла) → Combat (для Battle/Boss)
//   Combat завершён  → если победа над боссом или смерть → EndRun → LocationSelect
//                     если победа над обычным узлом     → MapView (с Advance)
//                     если бегство во время боя         → MapView (узел не отмечен)
//
// Также реагирует на запросы из любых экранов на удаление персонажа
// (возврат в CharacterCreation после Delete сейва).
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
				ShowLocationSelect();
				return;
			}
		}
		ShowCharacterCreation();
	}

	// =====================================================================
	// Экраны
	// =====================================================================

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
		ShowLocationSelect();
	}

	private void ShowLocationSelect()
	{
		GameData.Instance.EndRun();
		ClearContent();
		var view = new LocationSelectView();
		view.LocationChosen += OnLocationChosen;
		view.ResetCharacterRequested += OnResetCharacter;
		AddChild(view);
	}

	private void OnLocationChosen(int index)
	{
		GameData.Instance.StartRun(index);
		ShowMap();
	}

	private void ShowMap()
	{
		ClearContent();
		var map = new MapView();
		map.NodeSelected += OnMapNodeSelected;
		map.BackToLocationsRequested += ShowLocationSelect;
		map.ResetCharacterRequested += OnResetCharacter;
		AddChild(map);
	}

	private void OnMapNodeSelected(int nodeId)
	{
		var run = GameData.Instance.CurrentRun;
		if (run == null) return;
		var node = run.GetNode(nodeId);
		if (node == null) return;

		// На MVP-инкременте поддерживаем только Battle/Boss.
		// Остальные типы добавятся в следующих инкрементах (Elite/Rest/Chest/Event).
		switch (node.Type)
		{
			case MapNodeType.Battle:
			case MapNodeType.Elite:
			case MapNodeType.Boss:
				ShowCombatForNode(nodeId);
				break;
			default:
				// Заглушка для нереализованных типов: считаем "пройденным"
				// без эффекта. До добавления экранов эти узлы не должны
				// генерироваться, но безопаснее не зависнуть.
				GameData.Instance.AdvanceTo(nodeId);
				ShowMap();
				break;
		}
	}

	private void ShowCombatForNode(int nodeId)
	{
		// Узел становится "currentNodeId" сейчас, чтобы Combat.SpawnForCurrentNode
		// мог по нему определить тип боя. Если игрок сбежит — Visited не выставится,
		// но CurrentNodeId сместится. Это безопасно: AvailableNext() при возврате
		// будет считать что игрок уже на этом узле, и предложит соседей вперёд.
		// Чтобы избежать кривого UX (бегство = бесплатный пропуск), сохраняем
		// предыдущий CurrentNodeId и восстанавливаем его при бегстве.
		var run = GameData.Instance.CurrentRun;
		int prevNodeId = run.CurrentNodeId;
		run.Advance(nodeId);
		// Сразу откатываем Visited флаг — пометится только при победе.
		var n = run.GetNode(nodeId);
		if (n != null) n.Visited = false;

		ClearContent();
		var combat = new Combat();
		combat.ResetCharacterRequested += OnResetCharacter;
		combat.CombatExitRequested += (advance) => OnCombatExit(advance, nodeId, prevNodeId);
		AddChild(combat);
	}

	private void OnCombatExit(bool advance, int nodeId, int prevNodeId)
	{
		var run = GameData.Instance.CurrentRun;
		var node = run?.GetNode(nodeId);
		var character = GameData.Instance.Character;
		bool died = character != null && character.CurrentHp <= 0;

		if (died)
		{
			ShowLocationSelect();
			return;
		}

		if (advance && node != null)
		{
			// Победа: помечаем узел пройденным.
			node.Visited = true;
			if (node.Type == MapNodeType.Boss)
			{
				// Босс убит — подземелье завершено, возврат в хаб.
				ShowLocationSelect();
				return;
			}
			ShowMap();
			return;
		}

		// Бегство: откатываем CurrentNodeId на предыдущий, чтобы карта
		// показывала те же доступные узлы что и до боя.
		if (run != null) run.CurrentNodeId = prevNodeId;
		ShowMap();
	}

	private void OnResetCharacter()
	{
		SaveGame.Delete();
		GameData.Instance.EndRun();
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
