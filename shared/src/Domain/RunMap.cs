using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Карта одного забега (run) — генерируется при входе в локацию,
// уничтожается при выходе. Slay-the-Spire-style: сетка из строк (Rows),
// в каждой строке — несколько узлов (Nodes), рёбра идут вверх.
//
// POCO без Godot: будущий сервер сможет авторитетно генерить карты с тем
// же сидом. Сейчас всё на клиенте, но контракт уже совместимый.
//
// Координаты:
//   Row = 0  — стартовая линия (виртуальный "вход", не настоящие узлы)
//   Row = 1..N-1 — обычные ряды
//   Row = N  — единственный узел босса.
//
// CurrentRow = -1 означает "ещё не зашли в первый ряд". Игрок может выбрать
// любой узел Row=1. Дальше ходит только по EdgesTo текущего узла.

public enum MapNodeType
{
	Battle,
	Elite,
	Rest,
	Chest,
	Event,
	Boss,
	// Стартовый бой нового персонажа: один тренировочный манекен, с которого
	// гарантированно падает стартовый меч и комплект кожаной брони. Не входит
	// в обычные RunMap — запускается напрямую из Main после CreateCharacter.
	Tutorial,
}

public class MapNode
{
	public int Id;                          // Уникальный индекс в RunMap.Nodes.
	public int Row;                         // 1..N-1 для обычных, N — для босса.
	public int Col;                         // Позиция в ряду (для отрисовки).
	public MapNodeType Type;
	public List<int> EdgesTo = new();       // Id узлов следующего ряда, доступных отсюда.
	public bool Visited;                    // Игрок завершил этот узел.
}

public class RunMap
{
	public int LocationIndex;               // Индекс в GameData.LocationNames.
	public int Seed;
	public int Rows;                        // Включая ряд босса (последний).
	public List<MapNode> Nodes = new();
	public int CurrentNodeId = -1;          // -1 = ещё не выбран первый узел.

	// Все узлы текущей строки (для отрисовки колонкой).
	public IEnumerable<MapNode> NodesInRow(int row)
	{
		foreach (var n in Nodes)
			if (n.Row == row) yield return n;
	}

	// Куда сейчас можно пойти. Если игрок ещё не зашёл — все узлы Row=1.
	// Иначе — EdgesTo текущего узла.
	public IEnumerable<MapNode> AvailableNext()
	{
		if (CurrentNodeId < 0)
		{
			foreach (var n in NodesInRow(1)) yield return n;
			yield break;
		}
		var current = GetNode(CurrentNodeId);
		if (current == null) yield break;
		foreach (var id in current.EdgesTo)
		{
			var n = GetNode(id);
			if (n != null) yield return n;
		}
	}

	public bool CanAdvanceTo(int nodeId)
	{
		foreach (var n in AvailableNext())
			if (n.Id == nodeId) return true;
		return false;
	}

	// Помечает текущий узел как пройденный и переключает курсор.
	// Не валидирует — это работа CanAdvanceTo на уровне UI.
	public void Advance(int nodeId)
	{
		var n = GetNode(nodeId);
		if (n == null) return;
		n.Visited = true;
		CurrentNodeId = nodeId;
	}

	// True когда игрок закончил босс-узел.
	public bool IsCompleted()
	{
		if (CurrentNodeId < 0) return false;
		var n = GetNode(CurrentNodeId);
		return n != null && n.Type == MapNodeType.Boss && n.Visited;
	}

	public MapNode GetNode(int id)
	{
		if (id < 0 || id >= Nodes.Count) return null;
		return Nodes[id];
	}

	public MapNode CurrentNode() => CurrentNodeId < 0 ? null : GetNode(CurrentNodeId);
}
