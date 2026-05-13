using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Генератор карт подземелья (Slay-the-Spire-style).
//
// Алгоритм:
//   1. Для каждого "пути" (paths) ставим стартовый узел в случайной колонке Row 1.
//   2. На каждом следующем ряду путь сдвигается на -1/0/+1 (clamp в [0, cols-1]).
//   3. Если в (row, col) уже есть узел — переиспользуем его (пути сливаются),
//      добавляя только ребро от текущего предыдущего узла.
//   4. Последний ряд — единственный Boss-узел; все узлы предпоследнего ряда
//      ведут к нему.
//   5. Тип каждого промежуточного узла назначается AssignType — пока только
//      Battle (Elite/Rest/Chest/Event добавятся в следующих инкрементах).
//
// Параметры локаций — в Specs (по индексу совпадают с GameData.LocationNames).

public static class MapGenerator
{
	public class LocationSpec
	{
		public int Rows;     // Всего рядов, включая ряд босса (последний).
		public int Paths;    // Сколько путей строится снизу вверх.
		public int Cols;     // Ширина сетки (макс. позиций в ряду).
	}

	private static readonly LocationSpec[] Specs =
	{
		new() { Rows = 4, Paths = 2, Cols = 4 }, // Подземелье — короткое.
		new() { Rows = 5, Paths = 3, Cols = 5 }, // Тёмный лес — среднее.
		new() { Rows = 6, Paths = 3, Cols = 5 }, // Логово босса — длинное.
		new() { Rows = 8, Paths = 3, Cols = 5 }, // Заброшенные катакомбы — длинное (lvl≥5).
		new() { Rows = 10, Paths = 4, Cols = 6 }, // Развалины старого замка — самое длинное (lvl≥5).
	};

	// Главный API — детерминированная генерация из seed. Клиент и сервер
	// получают seed из StartRunResponse и оба строят идентичную карту.
	public static RunMap Generate(int locationIndex, int seed)
	{
		var rng = new RandomSource(seed);
		var spec = SpecFor(locationIndex);
		var map = new RunMap
		{
			LocationIndex = locationIndex,
			Seed = seed,
			Rows = spec.Rows,
		};

		// (row, col) → Id узла. Для дедупликации при пересечении путей.
		var grid = new Dictionary<(int row, int col), int>();

		// === 1. Прокладываем пути от Row 1 до предпоследнего ряда ===
		int lastNormalRow = spec.Rows - 1;
		for (int p = 0; p < spec.Paths; p++)
		{
			int col = rng.Next(spec.Cols);
			int prevId = -1;

			for (int row = 1; row <= lastNormalRow; row++)
			{
				if (row > 1)
				{
					int delta = rng.Range(-1, 2); // -1, 0, +1
					col = Clamp(col + delta, 0, spec.Cols - 1);
				}
				int id = EnsureNode(map, grid, row, col, MapNodeType.Battle);
				if (prevId >= 0) AddEdgeOnce(map.Nodes[prevId], id);
				prevId = id;
			}
		}

		// === 2. Boss row: единственный узел, к нему ведут все из lastNormalRow ===
		int bossCol = spec.Cols / 2;
		int bossId = EnsureNode(map, grid, spec.Rows, bossCol, MapNodeType.Boss);
		foreach (var n in map.Nodes)
			if (n.Row == lastNormalRow) AddEdgeOnce(n, bossId);

		// === 3. Тип узлов (на данный момент все промежуточные — Battle) ===
		AssignTypes(map);

		return map;
	}

	// Берёт узел из (row, col) или создаёт новый. Возвращает Id.
	private static int EnsureNode(RunMap map, Dictionary<(int, int), int> grid,
		int row, int col, MapNodeType type)
	{
		if (grid.TryGetValue((row, col), out int existing)) return existing;
		var node = new MapNode
		{
			Id = map.Nodes.Count,
			Row = row,
			Col = col,
			Type = type,
		};
		map.Nodes.Add(node);
		grid[(row, col)] = node.Id;
		return node.Id;
	}

	private static void AddEdgeOnce(MapNode from, int toId)
	{
		if (!from.EdgesTo.Contains(toId)) from.EdgesTo.Add(toId);
	}

	// На MVP-инкременте: всё что не Boss — Battle.
	// Расширим в следующем инкременте: вкрапления Rest/Chest/Elite/Event с весами.
	private static void AssignTypes(RunMap map)
	{
		// Пока что узлы создаются с правильными типами в Generate.
		// Hook оставлен под будущее: тут будем перетасовывать промежуточные.
	}

	private static LocationSpec SpecFor(int locationIndex)
	{
		if (locationIndex < 0 || locationIndex >= Specs.Length) return Specs[0];
		return Specs[locationIndex];
	}

	private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
