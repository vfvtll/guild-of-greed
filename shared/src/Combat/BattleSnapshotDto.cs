using System.Text.Json;
using System.Text.Json.Serialization;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Combat;

// Serializable shape активного боя для server persistence (active_battles
// SQLite table) и client-side resume. Тот же JSON-формат на обеих сторонах
// — сервер пишет, отдаёт через SelectCharacterResponse.ActiveBattleJson,
// клиент десериализует и продолжает бой.
//
// RandomSource не сериализуется напрямую (Calls — get-only); храним
// State.Seed внутри State + RngCalls отдельно. RestoreRng() пересоздаёт
// RandomSource(seed) и проматывает AdvanceTo(Calls).
public class BattleSnapshotDto
{
	[JsonInclude] public BattleState State;
	[JsonInclude] public MapNodeType NodeType;
	[JsonInclude] public int LocationIndex;
	[JsonInclude] public int RngCalls;

	// Per-battle поля CharacterData помечены [JsonIgnore] (см. CharacterData.cs)
	// — иначе бы они портили обычный character_json. Здесь дублируем явно,
	// чтобы при resume не потерять кровотечение/баффы/счётчик крита/блок.
	[JsonInclude] public int PlayerCurrentBlock;
	[JsonInclude] public System.Collections.Generic.List<StatusEffect> PlayerEffects;
	[JsonInclude] public int PlayerAttacksSinceLastCrit;
	[JsonInclude] public int PlayerBleedStack;

	private static readonly JsonSerializerOptions Opts = new()
	{
		IncludeFields = true,
		PropertyNameCaseInsensitive = true,
	};

	public static string ToJson(BattleSnapshotDto snap) => JsonSerializer.Serialize(snap, Opts);
	public static BattleSnapshotDto FromJson(string json) => JsonSerializer.Deserialize<BattleSnapshotDto>(json, Opts);

	// Снимает все per-battle поля игрока в snapshot перед сериализацией.
	// Вызывает сторона, конструирующая dto (server BattleSnapshot.Serialize).
	public void CaptureFromPlayer()
	{
		if (State?.Player == null) return;
		PlayerCurrentBlock = State.Player.CurrentBlock;
		PlayerEffects = new System.Collections.Generic.List<StatusEffect>(State.Player.Effects);
		PlayerAttacksSinceLastCrit = State.Player.AttacksSinceLastCrit;
		PlayerBleedStack = State.Player.BleedStack;
	}

	// Перезаписывает per-battle поля игрока после десериализации.
	// Вызывается вместе с RestoreRng при resume.
	public void RestoreToPlayer()
	{
		if (State?.Player == null) return;
		State.Player.CurrentBlock = PlayerCurrentBlock;
		State.Player.Effects = PlayerEffects ?? new System.Collections.Generic.List<StatusEffect>();
		State.Player.AttacksSinceLastCrit = PlayerAttacksSinceLastCrit;
		State.Player.BleedStack = PlayerBleedStack;
	}

	// Восстанавливает RandomSource на State из (Seed, RngCalls). После
	// этого State готов к дальнейшим ApplyAction — CSP-стрим сошёлся.
	public void RestoreRng()
	{
		if (State == null) return;
		State.Rng = new RandomSource(State.Seed);
		State.Rng.AdvanceTo(RngCalls);
	}
}
