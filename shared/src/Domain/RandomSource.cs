using System;
using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Instance-based RNG. Используется для детерминированных просчётов которые
// должны давать одинаковый результат на клиенте и на сервере при одном seed.
//
// Старый static-фасад `Rng` оставлен (см. Rng.cs) для legacy-кода, который
// использует "глобальный" недетерминированный RNG (UI-эффекты, генерация
// карты подземелья и т.п.). Боевой код мигрирует на RandomSource.
//
// На сервере: создаётся новый RandomSource(seed) на каждое начало боя,
// seed выдаётся клиенту в BattleStarted — клиент использует тот же seed.
// Дальше все Rng-операции (shuffle, intent roll, loot) дают идентичную
// последовательность на обеих сторонах — это база для CSP-сверки.
public class RandomSource
{
	private readonly Random _rand;

	public RandomSource() { _rand = new Random(); }
	public RandomSource(int seed) { _rand = new Random(seed); }

	// [0, maxExclusive)
	public int Next(int maxExclusive)
	{
		if (maxExclusive <= 0) return 0;
		return _rand.Next(maxExclusive);
	}

	// [minInclusive, maxExclusive)
	public int Range(int minInclusive, int maxExclusive)
	{
		if (maxExclusive <= minInclusive) return minInclusive;
		return _rand.Next(minInclusive, maxExclusive);
	}

	// [0.0, 1.0)
	public float NextFloat() => (float)_rand.NextDouble();

	public bool Chance(float probability) => NextFloat() < probability;

	public T Pick<T>(IList<T> list)
	{
		if (list == null || list.Count == 0) return default;
		return list[_rand.Next(list.Count)];
	}
}
