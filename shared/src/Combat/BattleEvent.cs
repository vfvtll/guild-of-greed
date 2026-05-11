using System.Collections.Generic;

namespace GuildOfGreed.Shared.Combat;

// Атомарные изменения, которые BatleEngine генерирует в ответ на BattleAction
// (или системные тики). Список BattleEvent — это и есть "дельта", которой
// обменивается клиент/сервер в CSP: они эмитят одну и ту же последовательность
// при одной BattleAction и одном RNG seed.
//
// Стейт меняется через CombatEngine.Apply — оно прокатывает каждый event
// и мутирует BattleState. View триггерится по event'ам (флэш, всплывающий
// урон, анимация смерти).
public enum BattleEventType
{
	BattleStarted,
	TurnStarted,
	TurnEnded,
	DeckShuffled,
	CardDrawn,
	CardPlayed,
	CardDiscarded,
	MpSpent,
	MpRegenerated,
	HpHealed,
	BlockGained,
	DamageDealtToEnemy,
	DamageDealtToPlayer,
	EnemyDied,
	PlayerDied,
	EffectApplied,
	EffectTicked,
	EnemyIntentRolled,
	EnemyAction,
	BattleEnded,
	LootDropped,
}

public class BattleEvent
{
	public BattleEventType Type;

	// Универсальные поля — заполнены только для соответствующих Type.
	public int EnemyIndex = -1;
	public int HandIndex = -1;
	public int Amount;
	public bool IsCrit;
	public bool IsPhys;       // для DamageDealtTo* — выбор цвета всплывающего текста.
	public bool Victory;
	public string CardId;
	public string PotionId;
	public string EffectType;
	public int EffectDuration;
	public string IntentName;
	public List<string> CardOrder;       // для DeckShuffled: новый порядок колоды.
	public List<string> DroppedItems;    // для LootDropped: ID-ы выпавших предметов.
}
