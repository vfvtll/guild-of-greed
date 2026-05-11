using Godot;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Экран карты подземелья. Рисует узлы текущего RunMap и рёбра между ними,
// разрешает клик только по узлам из CurrentRun.AvailableNext().
//
// Сигналы наверх:
//   NodeSelected(int nodeId)        — игрок кликнул на доступный узел.
//   BackToLocationsRequested()       — вышел из подземелья (run прерывается).
//   ResetCharacterRequested()        — кнопка «Новый персонаж».
//
// Карта горизонтальная (под landscape): Row растёт вправо, Col — это Y.
// Слева "вход" (виртуальная точка отсчёта), справа — узел босса.
public partial class MapView : Control
{
	[Signal] public delegate void NodeSelectedEventHandler(int nodeId);
	[Signal] public delegate void BackToLocationsRequestedEventHandler();
	[Signal] public delegate void ResetCharacterRequestedEventHandler();

	private const int LeftMargin   = 80;
	private const int RightMargin  = 60;
	private const int CenterY      = 400;
	private const int StepY        = 80;
	private const int NodeSize     = 56;

	private Vector2 _entryPos;        // Координата виртуального "входа" слева.

	private readonly Dictionary<int, Vector2> _nodePositions = new();
	private InventoryOverlay _inventoryOverlay;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUI();
		BuildNodes();
	}

	private void BuildUI()
	{
		var bg = new ColorRect { Color = UIStyle.BgDeep };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		bg.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(bg);

		// === Top bar ===
		var top = new HBoxContainer { Position = new Vector2(20, 12) };
		top.AddThemeConstantOverride("separation", 10);
		AddChild(top);

		var backBtn = new Button { Text = "← Выйти из подземелья" };
		UIStyle.StyleButton(backBtn);
		backBtn.Pressed += () => EmitSignal(SignalName.BackToLocationsRequested);
		top.AddChild(backBtn);

		var inventoryBtn = new Button { Text = "🎒 Инвентарь" };
		UIStyle.StyleButton(inventoryBtn);
		inventoryBtn.Pressed += OnInventoryPressed;
		top.AddChild(inventoryBtn);

		var resetBtn = new Button { Text = "👤 Новый персонаж" };
		UIStyle.StyleButton(resetBtn);
		resetBtn.Pressed += () => EmitSignal(SignalName.ResetCharacterRequested);
		top.AddChild(resetBtn);

		// === Title ===
		var loc = GameData.Instance.CurrentLocationName();
		var title = UIStyle.MakeLabel($"🗺  {loc} — карта подземелья", 22, UIStyle.GoldBright);
		title.Position = new Vector2(420, 22);
		AddChild(title);

		// === Hint snippet ===
		var hintText = GameData.Instance.CurrentRun?.CurrentNodeId < 0
			? "Выберите первый узел"
			: "Выберите следующий узел";
		var hint = UIStyle.MakeLabel(hintText, 14, UIStyle.TextSecondary);
		hint.Position = new Vector2(420, 56);
		AddChild(hint);

		// === Player health snippet ===
		var p = GameData.Instance.Character;
		if (p != null)
		{
			var hp = UIStyle.MakeLabel(
				$"{p.CharacterName}   ❤ {p.CurrentHp}/{p.MaxHp()}   ✦ {p.CurrentMp}/{p.MaxMp()}",
				14, UIStyle.TextPrimary);
			hp.Position = new Vector2(900, 22);
			AddChild(hp);
		}
	}

	private void BuildNodes()
	{
		var run = GameData.Instance.CurrentRun;
		if (run == null) return;

		int totalRowsIncludingEntry = run.Rows + 1;       // +1 виртуальный ряд "вход"
		int viewWidth = (int)(GetViewportRect().Size.X - LeftMargin - RightMargin);
		int stepX = viewWidth / totalRowsIncludingEntry;

		_nodePositions.Clear();

		// Сначала вычислим максимум столбцов на любом ряду — для центрирования по Y.
		int maxCols = 1;
		foreach (var n in run.Nodes)
			if (n.Col + 1 > maxCols) maxCols = n.Col + 1;

		float midCol = (maxCols - 1) * 0.5f;

		foreach (var n in run.Nodes)
		{
			int x = LeftMargin + (n.Row + 1) * stepX;
			int y = (int)(CenterY + (n.Col - midCol) * StepY);
			_nodePositions[n.Id] = new Vector2(x, y);
		}

		_entryPos = new Vector2(LeftMargin + stepX * 0.5f, CenterY);

		// Подпись "Вход" — отдельным Label чтобы не зависеть от ThemeDB шрифта.
		var entryLabel = UIStyle.MakeLabel("🚪 Вход", 14, UIStyle.TextSecondary);
		entryLabel.Position = _entryPos + new Vector2(-26, 16);
		AddChild(entryLabel);

		// === Создание кнопок узлов ===
		foreach (var n in run.Nodes)
		{
			var btn = new MapNodeButton(n);
			var pos = _nodePositions[n.Id];
			btn.Position = pos - new Vector2(NodeSize / 2f, NodeSize / 2f);
			btn.CustomMinimumSize = new Vector2(NodeSize, NodeSize);
			btn.Size = new Vector2(NodeSize, NodeSize);
			btn.ApplyStatus(StatusFor(n, run));
			int capturedId = n.Id;
			btn.Pressed += () => OnNodeClicked(capturedId);
			AddChild(btn);
		}

		QueueRedraw();
	}

	// Рёбра рисуются здесь, под кнопками (Godot вызывает _Draw до отрисовки детей).
	public override void _Draw()
	{
		var run = GameData.Instance.CurrentRun;
		if (run == null) return;

		// === "Вход" слева — точка + рёбра ко всем узлам Row=1 ===
		DrawCircle(_entryPos, 10, UIStyle.GoldMid);

		foreach (var n in run.NodesInRow(1))
		{
			if (!_nodePositions.TryGetValue(n.Id, out var to)) continue;
			DrawEdge(_entryPos, to, IsAvailable(n, run), n.Visited);
		}

		// === Рёбра между узлами ===
		foreach (var n in run.Nodes)
		{
			if (!_nodePositions.TryGetValue(n.Id, out var fromPos)) continue;
			foreach (var toId in n.EdgesTo)
			{
				if (!_nodePositions.TryGetValue(toId, out var toPos)) continue;
				var toNode = run.GetNode(toId);
				bool active = n.Visited && IsAvailable(toNode, run);
				bool walked = n.Visited && toNode != null && toNode.Visited;
				DrawEdge(fromPos, toPos, active, walked);
			}
		}
	}

	private void DrawEdge(Vector2 from, Vector2 to, bool active, bool walked)
	{
		Color color;
		float width;
		if (walked)        { color = UIStyle.GoldBright * 0.7f;  width = 4f; }
		else if (active)   { color = UIStyle.GoldBright;         width = 4f; }
		else               { color = UIStyle.GoldDark * 0.6f;    width = 2f; }
		DrawLine(from, to, color, width, true);
	}

	private static bool IsAvailable(MapNode node, RunMap run)
	{
		if (node == null) return false;
		foreach (var avail in run.AvailableNext())
			if (avail.Id == node.Id) return true;
		return false;
	}

	private static MapNodeButton.NodeStatus StatusFor(MapNode node, RunMap run)
	{
		if (node.Id == run.CurrentNodeId) return MapNodeButton.NodeStatus.Current;
		if (node.Visited) return MapNodeButton.NodeStatus.Visited;
		if (IsAvailable(node, run)) return MapNodeButton.NodeStatus.Available;
		return MapNodeButton.NodeStatus.Locked;
	}

	private void OnNodeClicked(int nodeId)
	{
		var run = GameData.Instance.CurrentRun;
		if (run == null) return;
		if (!run.CanAdvanceTo(nodeId)) return;
		EmitSignal(SignalName.NodeSelected, nodeId);
	}

	// Инвентарь вне боя — полный доступ (можно менять экипировку и пить зелья).
	private void OnInventoryPressed()
	{
		if (_inventoryOverlay != null) return;
		_inventoryOverlay = new InventoryOverlay { ReadOnly = false };
		_inventoryOverlay.Closed += OnInventoryClosed;
		AddChild(_inventoryOverlay);
	}

	private void OnInventoryClosed()
	{
		if (_inventoryOverlay == null) return;
		RemoveChild(_inventoryOverlay);
		_inventoryOverlay.QueueFree();
		_inventoryOverlay = null;
	}
}
