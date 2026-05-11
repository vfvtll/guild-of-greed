using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Data;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Combat;

// Pure-function движок боя. ВСЕ переходы состояния = чистые методы от
// (BattleState, BattleAction|тик) → List<BattleEvent>. Stateful только сам
// BattleState; engine классов не содержит полей.
//
// Контракт CSP:
//   - Клиент и сервер имеют BattleState с одинаковым Seed.
//   - Клиент вызывает Apply на свой state → получает events → визуализирует.
//   - Параллельно шлёт BattleAction на сервер.
//   - Сервер вызывает Apply на свой state → получает свои events.
//   - Сервер сравнивает: ожидаемые от клиента события (или просто хеши) с
//     серверными. При совпадении — confirms. При расхождении — sends rollback.
//
// На И5в.1 реализованы только StartBattle и EndTurn — простейшие действия
// без выбора цели карты. PlayCard/UsePotion/Flee — следующий шаг.
public static class CombatEngine
{
	// === Старт боя =========================================================

	// Создаёт state, генерит начальную колоду, тасует, тянет руку, ролит intent.
	// Возвращает state + events стартового хода.
	public static (BattleState State, List<BattleEvent> Events) StartBattle(
		CharacterData player,
		List<EnemyData> enemies,
		List<string> deckSource,
		int seed)
	{
		var state = new BattleState
		{
			Player = player,
			Enemies = enemies,
			Seed = seed,
			Rng = new RandomSource(seed),
		};

		// Сброс боевого state игрока (Hp/Mp/Block/Effects/AttacksSinceLastCrit).
		state.Player.ResetForCombat();

		var events = new List<BattleEvent> { new() { Type = BattleEventType.BattleStarted } };

		// Колода + тасовка.
		state.Deck = new List<string>(deckSource);
		Shuffle(state.Deck, state.Rng);
		events.Add(new BattleEvent
		{
			Type = BattleEventType.DeckShuffled,
			CardOrder = new List<string>(state.Deck),
		});

		// Намерения врагов.
		for (int i = 0; i < state.Enemies.Count; i++)
		{
			var enemy = state.Enemies[i];
			enemy.RollIntent(state.Rng);
			events.Add(new BattleEvent
			{
				Type = BattleEventType.EnemyIntentRolled,
				EnemyIndex = i,
				IntentName = enemy.NextIntent?.Name,
			});
		}

		// Стартовый ход игрока (без реген MP — он восстанавливается с ResetForCombat).
		BeginPlayerTurn(state, events, regenMp: false);

		return (state, events);
	}

	// === EndTurn ===========================================================

	// Сброс руки → тик эффектов → ход врагов → новый ход игрока.
	public static List<BattleEvent> EndTurn(BattleState state)
	{
		if (state.CombatOver) return new List<BattleEvent>();
		var events = new List<BattleEvent>();

		// 1. Сброс руки в discard.
		foreach (var cardId in state.Hand)
		{
			state.Discard.Add(cardId);
			events.Add(new BattleEvent
			{
				Type = BattleEventType.CardDiscarded,
				CardId = cardId,
			});
		}
		state.Hand.Clear();

		// 2. Тик эффектов игрока.
		TickPlayerEffects(state, events);

		// 3. Все живые враги бьют.
		EnemyTurn(state, events);
		if (state.CombatOver) return events;

		// 4. Новые intent'ы.
		for (int i = 0; i < state.Enemies.Count; i++)
		{
			var enemy = state.Enemies[i];
			if (enemy.CurrentHp <= 0) continue;
			enemy.RollIntent(state.Rng);
			events.Add(new BattleEvent
			{
				Type = BattleEventType.EnemyIntentRolled,
				EnemyIndex = i,
				IntentName = enemy.NextIntent?.Name,
			});
		}

		// 5. Старт следующего хода игрока.
		BeginPlayerTurn(state, events, regenMp: true);

		return events;
	}

	// === Internal pieces ===================================================

	private static void BeginPlayerTurn(BattleState state, List<BattleEvent> events, bool regenMp)
	{
		state.TurnCount++;

		if (regenMp)
		{
			int amount = state.Player.MpRegen();
			int oldMp = state.Player.CurrentMp;
			state.Player.CurrentMp = Math.Min(state.Player.MaxMp(), state.Player.CurrentMp + amount);
			int delta = state.Player.CurrentMp - oldMp;
			if (delta > 0)
				events.Add(new BattleEvent { Type = BattleEventType.MpRegenerated, Amount = delta });
		}

		// Блок не переносится между ходами игрока.
		state.Player.CurrentBlock = 0;

		// Добор руки до HandSize.
		DrawToHand(state, events, state.Player.HandSize());

		events.Add(new BattleEvent { Type = BattleEventType.TurnStarted, Amount = state.TurnCount });
	}

	private static void DrawToHand(BattleState state, List<BattleEvent> events, int target)
	{
		while (state.Hand.Count < target)
		{
			if (state.Deck.Count == 0)
			{
				if (state.Discard.Count == 0) return;
				state.Deck = new List<string>(state.Discard);
				state.Discard.Clear();
				Shuffle(state.Deck, state.Rng);
				events.Add(new BattleEvent
				{
					Type = BattleEventType.DeckShuffled,
					CardOrder = new List<string>(state.Deck),
				});
			}
			string top = state.Deck[^1];
			state.Deck.RemoveAt(state.Deck.Count - 1);
			state.Hand.Add(top);
			events.Add(new BattleEvent
			{
				Type = BattleEventType.CardDrawn,
				CardId = top,
				HandIndex = state.Hand.Count - 1,
			});
		}
	}

	private static void TickPlayerEffects(BattleState state, List<BattleEvent> events)
	{
		// Сделаем тик через цикл по копии, чтобы фиксировать тикнувшие/истёкшие.
		for (int i = state.Player.Effects.Count - 1; i >= 0; i--)
		{
			var e = state.Player.Effects[i];
			e.Remaining--;
			events.Add(new BattleEvent
			{
				Type = BattleEventType.EffectTicked,
				EffectType = e.Type,
				EffectDuration = e.Remaining,
			});
			if (e.Remaining <= 0) state.Player.Effects.RemoveAt(i);
		}
	}

	private static void EnemyTurn(BattleState state, List<BattleEvent> events)
	{
		foreach (var enemy in state.Enemies)
		{
			if (enemy.CurrentHp <= 0) continue;
			enemy.CurrentBlock = 0;
			var intent = enemy.NextIntent;
			if (intent == null) continue;

			switch (intent.Type)
			{
				case "attack":
					ApplyDamageToPlayer(state, events, intent.Amount, intent.Name);
					if (state.Player.CurrentHp <= 0)
					{
						state.CombatOver = true;
						state.Victory = false;
						events.Add(new BattleEvent { Type = BattleEventType.PlayerDied });
						events.Add(new BattleEvent { Type = BattleEventType.BattleEnded, Victory = false });
						return;
					}
					break;
				case "block":
					enemy.CurrentBlock += intent.Amount;
					events.Add(new BattleEvent
					{
						Type = BattleEventType.EnemyAction,
						EnemyIndex = state.Enemies.IndexOf(enemy),
						Amount = intent.Amount,
						IntentName = intent.Name,
					});
					break;
			}

			// Тик эффектов на враге.
			for (int i = enemy.Effects.Count - 1; i >= 0; i--)
			{
				var e = enemy.Effects[i];
				e.Remaining--;
				if (e.Remaining <= 0) enemy.Effects.RemoveAt(i);
			}
		}
	}

	private static void ApplyDamageToPlayer(BattleState state, List<BattleEvent> events,
		int rawDamage, string intentName)
	{
		int absorbed = Math.Min(state.Player.CurrentBlock, rawDamage);
		state.Player.CurrentBlock -= absorbed;
		int hpDamage = Math.Max(0, rawDamage - absorbed);
		// Бронированный физический mitigation идёт через PhysDef. Сейчас намерение
		// не разделяет phys/magic — копируем логику Combat.Cards: применяем PhysDef.
		hpDamage = Math.Max(0, hpDamage - state.Player.PhysDef());
		state.Player.CurrentHp = Math.Max(0, state.Player.CurrentHp - hpDamage);
		events.Add(new BattleEvent
		{
			Type = BattleEventType.DamageDealtToPlayer,
			Amount = hpDamage,
			IntentName = intentName,
		});
	}

	private static void Shuffle<T>(List<T> list, RandomSource rng)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
