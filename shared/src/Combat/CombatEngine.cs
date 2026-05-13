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
// И5в.2: добавлены PlayCard / UsePotion / Flee + dispatcher Apply.
// Дроп лута живёт в engine: на смерть врага рандомно прокатывается LootTable
// и итог кладётся в state.Player.Inventory здесь же — обе стороны (клиент и
// сервер) пополняют инвентарь идентично при одном RNG.
public static class CombatEngine
{
	private const int PotionMaxStack = 9;

	// =======================================================================
	// Dispatcher
	// =======================================================================

	public static List<BattleEvent> Apply(BattleState state, BattleAction action)
	{
		if (state.CombatOver) return new List<BattleEvent>();
		return action.Type switch
		{
			BattleActionType.PlayCard  => PlayCardAction(state, action),
			BattleActionType.UsePotion => UsePotionAction(state, action),
			BattleActionType.EndTurn   => EndTurn(state),
			BattleActionType.Flee      => FleeAction(state),
			_                          => new List<BattleEvent>(),
		};
	}

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

		// Подготовка к бою: HP/MP переносятся из прошлого боя (persist между
		// узлами одного забега), но Block / Effects / счётчик крита сбрасываются.
		state.Player.PrepareForBattle();

		// Снэпшот уровней ДО боя — нужен ResolveLevelUps для эмитa LevelUp-событий
		// только при реальном повышении. Сохраняем уровень всех типов оружия,
		// по которым у игрока уже есть XP, плюс текущий equipped-type на случай
		// если он ещё не в словаре (0 XP).
		state.PreBattleCharacterLevel = state.Player.Level;
		state.PreBattleWeaponLevels.Clear();
		if (state.Player.WeaponXp != null)
			foreach (var kv in state.Player.WeaponXp)
				state.PreBattleWeaponLevels[kv.Key] = state.Player.GetWeaponLevel(kv.Key);
		if (state.Player.Weapon != null)
		{
			var t = state.Player.Weapon.Type;
			if (!string.IsNullOrEmpty(t) && !state.PreBattleWeaponLevels.ContainsKey(t))
				state.PreBattleWeaponLevels[t] = 0;
		}

		var events = new List<BattleEvent> { new() { Type = BattleEventType.BattleStarted } };

		// Колода + тасовка. Стартовый shuffle event не эмитим — клиент видит
		// итоговый порядок прямо в state.Deck. DeckShuffled понадобится только
		// при reshuffle сброса во время боя (DrawToHand ниже).
		state.Deck = new List<string>(deckSource);
		Shuffle(state.Deck, state.Rng);

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

		// Стартовый ход игрока — С регеном MP. ResetForCombat зовётся только
		// при StartRun (вход в новое подземелье) и при смерти; между узлами
		// одного забега HP/MP переносятся как есть, поэтому без этого тика
		// игрок второго и последующих боёв не получал бы регена вообще.
		// На первом бою регена → no-op (MP уже на максимуме).
		BeginPlayerTurn(state, events, regenMp: true);

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
		// Reset цепочки маг.заклинаний (пассив посоха) на каждый новый ход.
		state.SpellsCastThisTurn = 0;

		if (regenMp)
		{
			int amount = state.Player.MpRegen();
			int oldMp = state.Player.CurrentMp;
			state.Player.CurrentMp = Math.Min(state.Player.MaxMp(), state.Player.CurrentMp + amount);
			int delta = state.Player.CurrentMp - oldMp;
			if (delta > 0)
				events.Add(new BattleEvent { Type = BattleEventType.MpRegenerated, Amount = delta });
		}

		// HpRegen (И6.2) — от аффиксов/сетов. У персонажа базы нет; если игрок
		// ничего "регенерирующего" не надел, amount=0 и event не эмитится.
		int hpRegen = state.Player.HpRegen();
		if (hpRegen > 0)
		{
			int oldHp = state.Player.CurrentHp;
			state.Player.CurrentHp = Math.Min(state.Player.MaxHp(), state.Player.CurrentHp + hpRegen);
			int delta = state.Player.CurrentHp - oldHp;
			if (delta > 0)
				events.Add(new BattleEvent { Type = BattleEventType.HpHealed, Amount = delta });
		}

		// Блок не переносится между ходами игрока.
		state.Player.CurrentBlock = 0;

		// Physical shield (И6.4): в начале хода игрока даёт блок = MaxHp × Magnitude%.
		if (state.Player.Shield != null && state.Player.Shield.Type == ShieldType.Physical)
		{
			int blockGained = state.Player.MaxHp() * state.Player.Shield.EffectMagnitude / 100;
			if (blockGained > 0)
			{
				state.Player.CurrentBlock += blockGained;
				events.Add(new BattleEvent { Type = BattleEventType.BlockGained, Amount = blockGained });
			}
		}

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
		for (int idx = 0; idx < state.Enemies.Count; idx++)
		{
			var enemy = state.Enemies[idx];
			if (enemy.CurrentHp <= 0) continue;
			enemy.CurrentBlock = 0;
			var intent = enemy.NextIntent;
			if (intent == null) continue;

			switch (intent.Type)
			{
				// "attack" — физический. "attack_magic" зарезервирован для
				// будущих маг.врагов; magic_shield reflect и balanced
				// counter-buff активируются только на маг.уроне.
				case "attack":
				case "attack_magic":
				{
					bool isPhys = intent.Type != "attack_magic";
					ApplyDamageToPlayer(state, events, intent.Amount, intent.Name, isPhys, idx);
					if (state.Player.CurrentHp <= 0)
					{
						events.Add(new BattleEvent { Type = BattleEventType.PlayerDied });
						EndBattle(state, events, victory: false);
						return;
					}
					break;
				}
				case "block":
					enemy.CurrentBlock += intent.Amount;
					events.Add(new BattleEvent
					{
						Type = BattleEventType.EnemyAction,
						EnemyIndex = idx,
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

			// Кровотечение (И6.2-E). Регенерация снимает часть стака; остаток
			// наносит урон по HP (игнорирует Block/PhysDef). Стак не сбрасывается.
			TickBleed(state, events, enemy);
			if (state.Player.CurrentHp <= 0)
			{
				// Защита от parade-case: bleed не бьёт игрока, но если игрок
				// умер от удара врага выше — уже return'нули. Здесь ничего.
			}
		}

		// Если враг помер от bleed-тика — добиваем драматургию (event/loot).
		// AllEnemiesDead будет проверен на следующем шаге BeginPlayerTurn.
	}

	private static void TickBleed(BattleState state, List<BattleEvent> events, EnemyData enemy)
	{
		if (enemy.BleedStack <= 0) return;
		// Регенерация съедает часть стака. Остаток ляжет уроном.
		enemy.BleedStack = Math.Max(0, enemy.BleedStack - enemy.HpRegen);
		if (enemy.BleedStack <= 0) return;
		int dmg = Math.Min(enemy.BleedStack, enemy.CurrentHp);
		enemy.CurrentHp -= dmg;
		events.Add(new BattleEvent
		{
			Type = BattleEventType.BleedTicked,
			EnemyIndex = state.Enemies.IndexOf(enemy),
			Amount = dmg,
		});
		if (enemy.CurrentHp <= 0)
		{
			events.Add(new BattleEvent
			{
				Type = BattleEventType.EnemyDied,
				EnemyIndex = state.Enemies.IndexOf(enemy),
			});
			GrantKillXp(state, enemy, events);
			var dropped = DropLoot(state, enemy);
			if (dropped.Count > 0)
				events.Add(new BattleEvent { Type = BattleEventType.LootDropped, DroppedItems = dropped });
			if (AllEnemiesDead(state))
				EndBattle(state, events, victory: true);
		}
	}

	private static void ApplyDamageToPlayer(BattleState state, List<BattleEvent> events,
		int rawDamage, string intentName, bool isPhys = true, int sourceEnemyIndex = -1)
	{
		int absorbed = Math.Min(state.Player.CurrentBlock, rawDamage);
		state.Player.CurrentBlock -= absorbed;
		int hpDamage = Math.Max(0, rawDamage - absorbed);
		// Mitigation: PhysDef для физ.урона, MagDef для маг.
		int defense = isPhys ? state.Player.PhysDef() : state.Player.MagDef();
		hpDamage = Math.Max(0, hpDamage - defense);
		state.Player.CurrentHp = Math.Max(0, state.Player.CurrentHp - hpDamage);
		events.Add(new BattleEvent
		{
			Type = BattleEventType.DamageDealtToPlayer,
			Amount = hpDamage,
			IntentName = intentName,
			IsPhys = isPhys,
		});

		// === Эффекты щита (И6.4) ===
		var shield = state.Player.Shield;
		if (shield == null || hpDamage <= 0) return;

		// Magic shield: возврат N% от полученного маг.урона обратно источнику.
		if (shield.Type == ShieldType.Magic && !isPhys && sourceEnemyIndex >= 0)
		{
			int reflect = hpDamage * shield.EffectMagnitude / 100;
			if (reflect > 0 && sourceEnemyIndex < state.Enemies.Count)
			{
				var src = state.Enemies[sourceEnemyIndex];
				if (src.CurrentHp > 0)
				{
					int actual = Math.Min(reflect, src.CurrentHp);
					src.CurrentHp -= actual;
					events.Add(new BattleEvent
					{
						Type = BattleEventType.DamageDealtToEnemy,
						EnemyIndex = sourceEnemyIndex,
						Amount = actual,
						IsPhys = false,
					});
				}
			}
		}

		// Balanced shield: counter-buff. Физ.удар → +Magnitude% маг.урона
		// в следующем ходу; маг.удар → +Magnitude% физ.урона.
		if (shield.Type == ShieldType.Balanced)
		{
			string buffType = isPhys ? "magic_dmg_pct" : "phys_dmg_pct";
			state.Player.AddEffect("balanced_shield_buff", buffType,
				shield.EffectMagnitude, shield.CounterBuffDuration);
		}
	}

	private static void Shuffle<T>(List<T> list, RandomSource rng)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	// =======================================================================
	// PlayCard
	// =======================================================================

	private static List<BattleEvent> PlayCardAction(BattleState state, BattleAction action)
	{
		var events = new List<BattleEvent>();

		// Валидация: индекс в руке, существование карты, достаточно MP, цель жива.
		if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count) return events;
		string cardId = state.Hand[action.HandIndex];
		var card = CardsDB.GetCard(cardId);
		if (card == null) return events;
		// Реальная стоимость может быть выше card.Cost из-за пассива посоха.
		int actualCost = CardsDB.ComputeManaCost(card, state.Player, state.SpellsCastThisTurn);
		if (state.Player.CurrentMp < actualCost) return events;

		bool needsTarget = card.Effect is "damage_phys" or "damage_magic" or "debuff_phys";
		EnemyData target = null;
		if (needsTarget)
		{
			if (action.TargetEnemyIndex < 0 || action.TargetEnemyIndex >= state.Enemies.Count) return events;
			target = state.Enemies[action.TargetEnemyIndex];
			if (target.CurrentHp <= 0) return events;
		}

		// Списываем MP по актуальной стоимости.
		state.Player.CurrentMp -= actualCost;
		events.Add(new BattleEvent { Type = BattleEventType.MpSpent, Amount = actualCost });

		// XP оружия — за каждую сыгранную АТАКУЮЩУЮ карту (damage_phys / damage_magic).
		// Без оружия (Weapon=null) XP не идёт — голому персонажу нечего качать.
		// 5 XP за карту: первый уровень оружия (50 XP) = ~10 атак = 1-2 боя.
		if (card.IsAttack && state.Player.Weapon != null)
		{
			var wt = state.Player.Weapon.Type;
			if (!string.IsNullOrEmpty(wt))
			{
				state.Player.WeaponXp ??= new Dictionary<string, int>();
				state.Player.WeaponXp.TryGetValue(wt, out int cur);
				state.Player.WeaponXp[wt] = cur + 5;
				events.Add(new BattleEvent
				{
					Type = BattleEventType.WeaponXpGained,
					EffectType = wt,
					Amount = 5,
				});
			}
		}

		// Применяем эффект карты.
		switch (card.Effect)
		{
			case "damage_phys":
			{
				// Считаем non-attack карты ДО снятия played-карты из руки —
				// это даёт интуитивный bonus от текущего "защитного" набора.
				int dmg = CardsDB.ComputePhysDamage(card, state.Player, target, state.Hand);
				bool isCrit = state.Player.TryConsumeCrit();
				if (isCrit) dmg = (int)Math.Round(dmg * state.Player.CritMultiplier());
				ApplyDamageToEnemy(state, events, action.TargetEnemyIndex, dmg, isPhys: true, isCrit);
				break;
			}
			case "damage_magic":
			{
				int chain = state.SpellsCastThisTurn;
				int dmg = CardsDB.ComputeMagicDamage(card, state.Player, target, chain);
				bool isCrit = state.Player.TryConsumeCrit();
				if (isCrit) dmg = (int)Math.Round(dmg * state.Player.CritMultiplier());
				ApplyDamageToEnemy(state, events, action.TargetEnemyIndex, dmg, isPhys: false, isCrit);
				// Инкрементим счётчик ТОЛЬКО для атакующих маг.заклинаний.
				// Следующее заклинание в этом ходу получит +Magnitude% урона / +Magnitude2% маны.
				state.SpellsCastThisTurn++;
				break;
			}
			case "block":
			{
				int amount = CardsDB.ComputeBlock(card, state.Player);
				state.Player.CurrentBlock += amount;
				events.Add(new BattleEvent { Type = BattleEventType.BlockGained, Amount = amount });
				break;
			}
			case "heal":
			{
				int amount = CardsDB.ComputeHeal(card, state.Player);
				int before = state.Player.CurrentHp;
				state.Player.CurrentHp = Math.Min(state.Player.CurrentHp + amount, state.Player.MaxHp());
				int healed = state.Player.CurrentHp - before;
				events.Add(new BattleEvent { Type = BattleEventType.HpHealed, Amount = healed });
				break;
			}
			case "debuff_phys":
				target.AddEffect("armor_break", "phys_taken_pct", card.AmountPct, card.Duration);
				events.Add(new BattleEvent
				{
					Type = BattleEventType.EffectApplied,
					EnemyIndex = action.TargetEnemyIndex,
					EffectType = "phys_taken_pct",
					Amount = (int)card.AmountPct,
					EffectDuration = card.Duration,
				});
				break;
			case "buff_magic":
				state.Player.AddEffect("magic_focus", "magic_dmg_pct", card.AmountPct, card.Duration);
				events.Add(new BattleEvent
				{
					Type = BattleEventType.EffectApplied,
					EnemyIndex = -1,
					EffectType = "magic_dmg_pct",
					Amount = (int)card.AmountPct,
					EffectDuration = card.Duration,
				});
				break;
		}

		// Карта уходит в сброс.
		state.Hand.RemoveAt(action.HandIndex);
		state.Discard.Add(cardId);
		events.Add(new BattleEvent
		{
			Type = BattleEventType.CardDiscarded,
			CardId = cardId,
			HandIndex = action.HandIndex,
		});

		// Победа?
		if (AllEnemiesDead(state))
			EndBattle(state, events, victory: true);

		return events;
	}

	private static void ApplyDamageToEnemy(BattleState state, List<BattleEvent> events,
		int enemyIndex, int rawDamage, bool isPhys, bool isCrit)
	{
		var enemy = state.Enemies[enemyIndex];
		int defense = isPhys ? enemy.PhysDef : enemy.MagicDef;
		int dmg = Math.Max(1, rawDamage - defense);
		if (enemy.CurrentBlock > 0)
		{
			int absorbed = Math.Min(enemy.CurrentBlock, dmg);
			enemy.CurrentBlock -= absorbed;
			dmg -= absorbed;
		}
		int hpDamage = Math.Min(dmg, enemy.CurrentHp);
		enemy.CurrentHp = Math.Max(0, enemy.CurrentHp - dmg);
		events.Add(new BattleEvent
		{
			Type = BattleEventType.DamageDealtToEnemy,
			EnemyIndex = enemyIndex,
			Amount = dmg,
			IsCrit = isCrit,
			IsPhys = isPhys,
		});

		// Bleed-накопление (И6.3). Только физический урон по HP (после
		// защиты+блока) и только если оружие игрока имеет bleed_on_hit.
		//
		// Гиперболическая формула с насыщением:
		//   bleed_add = hpDamage² × Magnitude / (100 × (hpDamage + K))
		// При малом уроне (hpDamage << K) растёт почти квадратично — слабые
		// удары почти не bleed'ят. При большом (hpDamage >> K) выходит на
		// линейную асимптоту bleed → hpDamage × Magnitude/100 — bleed НИКОГДА
		// не превышает Magnitude% от удара, какой бы огромный удар ни был.
		// Один удар на 1000 даёт ~2.5× больше bleed, чем десять по 100.
		const int BleedSaturationK = 200;
		if (isPhys && hpDamage > 0 && state.Player.Weapon != null)
		{
			int mag = state.Player.Weapon.PassiveMagnitude(WeaponPassive.BleedOnHit);
			if (mag > 0)
			{
				long add = (long)hpDamage * hpDamage * mag / (100L * (hpDamage + BleedSaturationK));
				if (add > 0)
				{
					enemy.BleedStack += (int)add;
					events.Add(new BattleEvent
					{
						Type = BattleEventType.BleedStacked,
						EnemyIndex = enemyIndex,
						Amount = (int)add,
					});
				}
			}
		}

		if (enemy.CurrentHp <= 0)
		{
			events.Add(new BattleEvent
			{
				Type = BattleEventType.EnemyDied,
				EnemyIndex = enemyIndex,
			});
			GrantKillXp(state, enemy, events);
			var dropped = DropLoot(state, enemy);
			if (dropped.Count > 0)
			{
				events.Add(new BattleEvent
				{
					Type = BattleEventType.LootDropped,
					EnemyIndex = enemyIndex,
					DroppedItems = dropped,
				});
			}
		}
	}

	// XP персонажа за смерть врага. Базируется на MaxHp — чем толще враг,
	// тем больше опыта (training-dummy ~7, гоблин ~25, босс ~80). Накапливается
	// в Exp; повышение Level отложено в ResolveLevelUps на BattleEnded.
	private static void GrantKillXp(BattleState state, EnemyData enemy, List<BattleEvent> events)
	{
		if (state.Player == null) return;
		int xp = Math.Max(1, enemy.MaxHp / 4);
		state.Player.Exp += xp;
		events.Add(new BattleEvent { Type = BattleEventType.XpGained, Amount = xp });
	}

	// Прокатывает LootTable врага. Каждая запись — независимый ролл через
	// state.Rng (детерминированно). Результат кладётся в инвентарь игрока,
	// если место есть; если переполнен — предмет в списке пропускается.
	private static List<string> DropLoot(BattleState state, EnemyData enemy)
	{
		var dropped = new List<string>();
		if (enemy.LootTable == null) return dropped;
		foreach (var entry in enemy.LootTable)
		{
			if (!state.Rng.Chance(entry.Chance)) continue;

			// Affixed-режим: ролл через ItemGenerator с детерминированным rng
			// (для CSP). Каждая единица в counta — отдельный instance-слот.
			// PotionsDB-предметы игнорируют флаг — стакаемые не имеют instance.
			if (entry.Affixed && PotionsDB.Get(entry.ItemId) == null)
			{
				int countAffixed = state.Rng.Range(entry.MinCount, entry.MaxCount + 1);
				for (int i = 0; i < countAffixed; i++)
				{
					if (state.Player.Inventory.IsFull) break;
					var weapon = ItemGenerator.RollWeapon(entry.ItemId, state.Rng);
					if (weapon != null)
					{
						state.Player.Inventory.TryAddInstance(weapon);
						dropped.Add($"{entry.ItemId}*");
						continue;
					}
					var armor = ItemGenerator.RollArmor(entry.ItemId, state.Rng);
					if (armor != null)
					{
						state.Player.Inventory.TryAddInstance(armor);
						dropped.Add($"{entry.ItemId}*");
					}
				}
				continue;
			}

			int count = state.Rng.Range(entry.MinCount, entry.MaxCount + 1);
			int maxStack = PotionsDB.Get(entry.ItemId) != null ? PotionMaxStack : 1;
			if (state.Player.Inventory.TryAdd(entry.ItemId, count, maxStack))
			{
				dropped.Add($"{entry.ItemId}×{count}");
			}
			// Если инвентарь полон — лут теряется. Не эмитим отдельного event:
			// для CSP достаточно факта inventory state.
		}

		// Деньги. Катаются всегда (если MoneyMax>0), не зависят от LootTable.
		// Идут в общий медный кошель — переполнения слотов нет.
		if (enemy.MoneyMax > 0)
		{
			int money = state.Rng.Range(enemy.MoneyMin, enemy.MoneyMax + 1);
			if (money > 0)
			{
				state.Player.Inventory.Money += money;
				dropped.Add($"money:{money}");
			}
		}
		return dropped;
	}

	private static bool AllEnemiesDead(BattleState state)
	{
		foreach (var e in state.Enemies)
			if (e.CurrentHp > 0) return false;
		return true;
	}

	// =======================================================================
	// UsePotion
	// =======================================================================

	private static List<BattleEvent> UsePotionAction(BattleState state, BattleAction action)
	{
		var events = new List<BattleEvent>();
		if (string.IsNullOrEmpty(action.PotionId)) return events;
		if (!state.Player.Inventory.Has(action.PotionId)) return events;
		var potion = PotionsDB.Get(action.PotionId);
		if (potion == null) return events;

		state.Player.Inventory.Remove(action.PotionId, 1);

		if (potion.HpRestore > 0)
		{
			int before = state.Player.CurrentHp;
			state.Player.CurrentHp = Math.Min(state.Player.MaxHp(), state.Player.CurrentHp + potion.HpRestore);
			int delta = state.Player.CurrentHp - before;
			if (delta > 0)
				events.Add(new BattleEvent { Type = BattleEventType.HpHealed, Amount = delta });
		}
		if (potion.MpRestore > 0)
		{
			int before = state.Player.CurrentMp;
			state.Player.CurrentMp = Math.Min(state.Player.MaxMp(), state.Player.CurrentMp + potion.MpRestore);
			int delta = state.Player.CurrentMp - before;
			if (delta > 0)
				events.Add(new BattleEvent { Type = BattleEventType.MpRegenerated, Amount = delta });
		}
		if (!string.IsNullOrEmpty(potion.BuffType) && potion.BuffDuration > 0)
		{
			state.Player.AddEffect(potion.Id, potion.BuffType, potion.BuffAmount, potion.BuffDuration);
			events.Add(new BattleEvent
			{
				Type = BattleEventType.EffectApplied,
				EnemyIndex = -1,
				EffectType = potion.BuffType,
				Amount = (int)potion.BuffAmount,
				EffectDuration = potion.BuffDuration,
				PotionId = potion.Id,
			});
		}
		return events;
	}

	// =======================================================================
	// Flee
	// =======================================================================

	private static List<BattleEvent> FleeAction(BattleState state)
	{
		var events = new List<BattleEvent>();
		EndBattle(state, events, victory: false);
		return events;
	}

	// Финализация боя: устанавливает CombatOver/Victory, прокатывает повышения
	// уровней (через ResolveLevelUps) и эмитит BattleEnded. Вызывается из ВСЕХ
	// точек, где бой может закончиться — победа/смерть/бегство.
	private static void EndBattle(BattleState state, List<BattleEvent> events, bool victory)
	{
		state.CombatOver = true;
		state.Victory = victory;
		ResolveLevelUps(state, events);
		events.Add(new BattleEvent { Type = BattleEventType.BattleEnded, Victory = victory });
	}

	// Повышение уровней по накопленному за бой XP. Чистая функция от state —
	// клиент и сервер прокатывают идентично.
	private static void ResolveLevelUps(BattleState state, List<BattleEvent> events)
	{
		var p = state.Player;
		if (p == null) return;

		// Персонаж: пока Exp ≥ порога — повышаем уровень и распределяем статы.
		while (p.Exp >= p.XpForNextCharacterLevel())
		{
			p.LevelUpCharacter();
			events.Add(new BattleEvent
			{
				Type = BattleEventType.CharacterLevelUp,
				Amount = p.Level,
			});
		}

		// Оружие: уровень — производная от XP (xp/50). Сравниваем с pre-battle
		// снэпшотом чтобы не дублировать событие на стороннем оружии без XP.
		if (p.WeaponXp != null)
		{
			foreach (var kv in p.WeaponXp)
			{
				int oldLevel = state.PreBattleWeaponLevels.GetValueOrDefault(kv.Key, 0);
				int newLevel = p.GetWeaponLevel(kv.Key);
				if (newLevel > oldLevel)
				{
					events.Add(new BattleEvent
					{
						Type = BattleEventType.WeaponLevelUp,
						EffectType = kv.Key,
						Amount = newLevel,
					});
				}
			}
		}
	}
}
