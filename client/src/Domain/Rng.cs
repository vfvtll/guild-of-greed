using System;

// Портативный RNG: без зависимости от Godot.
// Используется во всех Domain/Data вместо GD.Randi/Randf.
//
// На клиенте (Godot) сидируется в Core/GameData._Ready через Rng.Seed(GD.Randi()).
// На сервере будет сидироваться от собственного источника (timestamp, crypto rng).
// При необходимости в детерминизме (replays, тесты) — Rng.Seed(fixed) даёт воспроизводимую последовательность.
public static class Rng
{
	private static Random _rand = new Random();

	public static void Seed(int seed) => _rand = new Random(seed);

	public static void SeedFromTime() => _rand = new Random();

	// [0, maxExclusive)
	public static int Next(int maxExclusive)
	{
		if (maxExclusive <= 0) return 0;
		return _rand.Next(maxExclusive);
	}

	// [minInclusive, maxExclusive)
	public static int Range(int minInclusive, int maxExclusive)
	{
		if (maxExclusive <= minInclusive) return minInclusive;
		return _rand.Next(minInclusive, maxExclusive);
	}

	// [0.0, 1.0)
	public static float NextFloat() => (float)_rand.NextDouble();

	public static bool Chance(float probability) => NextFloat() < probability;

	public static T Pick<T>(System.Collections.Generic.IList<T> list)
	{
		if (list == null || list.Count == 0) return default;
		return list[_rand.Next(list.Count)];
	}
}
