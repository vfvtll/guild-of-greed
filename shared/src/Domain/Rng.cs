using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Глобальный недетерминированный RNG — фасад над глобальным RandomSource.
//
// Используется только для UI/мира где детерминизм НЕ нужен:
//   - Анимации, цвет-шумы, прочие визуальные эффекты на клиенте
//   - Генерация карты подземелья (пока — позже мигрируем на seeded)
//
// Для боевого кода (CombatEngine) — используй явный RandomSource из BattleState.
// Это критично для CSP: оба, клиент и сервер, должны получать одинаковую
// последовательность чисел с одним seed.
public static class Rng
{
	private static RandomSource _shared = new RandomSource();

	public static void Seed(int seed) => _shared = new RandomSource(seed);
	public static void SeedFromTime() => _shared = new RandomSource();

	public static int Next(int maxExclusive) => _shared.Next(maxExclusive);
	public static int Range(int min, int maxExclusive) => _shared.Range(min, maxExclusive);
	public static float NextFloat() => _shared.NextFloat();
	public static bool Chance(float probability) => _shared.Chance(probability);
	public static T Pick<T>(IList<T> list) => _shared.Pick(list);
}
