using Godot;
using System.Collections.Generic;
using System.Linq;
using GuildOfGreed.Shared.Combat;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Combat — обработка кликов по картам/врагам, выбор цели,
// view-рендеры BattleEvent. ВСЯ боевая математика — в shared CombatEngine.
public partial class Combat
{
	private void OnCardClicked(CardView view)
	{
		if (_state == null || _state.CombatOver || _busy) return;

		// Найти индекс кликнутой карты в руке (по позиции в HBox).
		int idx = -1;
		for (int i = 0; i < _handContainer.GetChildCount(); i++)
			if (_handContainer.GetChild(i) == view) { idx = i; break; }
		if (idx < 0 || idx >= _state.Hand.Count) return;

		var card = view.CardData;
		var p = _state.Player;

		int actualCost = CardsDB.ComputeManaCost(card, p, _state.SpellsCastThisTurn);
		if (p.CurrentMp < actualCost)
		{
			Log($"[color=#888]Недостаточно маны: {card.Name} стоит {actualCost} MP.[/color]");
			return;
		}

		bool needsTarget = card.Effect is "damage_phys" or "damage_magic" or "debuff_phys";

		if (!needsTarget)
		{
			PlayCardWithTarget(idx, -1);
			return;
		}

		var aliveIndices = new List<int>();
		for (int i = 0; i < _state.Enemies.Count; i++)
			if (_state.Enemies[i].CurrentHp > 0) aliveIndices.Add(i);
		if (aliveIndices.Count == 0) return;
		if (aliveIndices.Count == 1)
		{
			PlayCardWithTarget(idx, aliveIndices[0]);
			return;
		}

		// Несколько целей — переходим в режим выбора (или отменяем повторным кликом).
		if (_selectedHandIndex == idx)
		{
			CancelTargeting();
		}
		else
		{
			_selectedHandIndex = idx;
			_targetingHint.Text = $"Выберите цель для «{card.Name}» (ESC / ПКМ — отмена)";
			_targetingBanner.Visible = true;
		}
		RefreshUI();
	}

	private void OnEnemyTargeted(EnemyView view)
	{
		if (_selectedHandIndex < 0) return;
		if (view.Enemy == null || view.Enemy.CurrentHp <= 0) return;
		int handIdx = _selectedHandIndex;
		int enemyIdx = _state.Enemies.IndexOf(view.Enemy);
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;
		if (enemyIdx < 0) return;
		PlayCardWithTarget(handIdx, enemyIdx);
	}

	private void CancelTargeting()
	{
		if (_selectedHandIndex < 0) return;
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;
	}

	// Захватывает CardView ДО engine.Apply (после Apply карта уйдёт из руки),
	// чтобы AnimateCardOut получил корректный view-node.
	private async void PlayCardWithTarget(int handIndex, int targetEnemyIndex)
	{
		CardView playedView = null;
		if (handIndex < _handContainer.GetChildCount())
			playedView = _handContainer.GetChild(handIndex) as CardView;

		string cardId = handIndex >= 0 && handIndex < _state.Hand.Count ? _state.Hand[handIndex] : null;
		if (!string.IsNullOrEmpty(cardId))
		{
			var card = CardsDB.GetCard(cardId);
			if (card != null) Log($"[b]Сыграна карта:[/b] {card.Name}");
		}

		await ApplyActionAsync(new BattleAction
		{
			Type = BattleActionType.PlayCard,
			HandIndex = handIndex,
			TargetEnemyIndex = targetEnemyIndex,
		}, playedView);
	}

	// =====================================================================
	// View-рендеры BattleEvent
	// =====================================================================

	private void RenderDamageToEnemy(BattleEvent ev)
	{
		if (ev.EnemyIndex < 0 || ev.EnemyIndex >= _state.Enemies.Count) return;
		var enemy = _state.Enemies[ev.EnemyIndex];

		string critTag = ev.IsCrit ? "[color=#fc4]🎯 КРИТ![/color] " : "";
		string kind = ev.IsPhys ? "Физ" : "Маг";
		Log($"{critTag}→ {enemy.EnemyName}: {kind} урон {ev.Amount}");

		var view = FindEnemyView(enemy);
		if (view != null)
		{
			var pos = view.GlobalPosition + new Vector2(view.Size.X / 2f, view.Size.Y * 0.25f);
			Color color;
			int fontSize;
			string text;
			if (ev.IsCrit)
			{
				color = new Color(1.0f, 0.85f, 0.20f);
				fontSize = 38;
				text = $"-{ev.Amount}!";
			}
			else
			{
				color = ev.IsPhys ? new Color(1.0f, 0.6f, 0.35f) : new Color(0.75f, 0.55f, 1.0f);
				fontSize = 28;
				text = $"-{ev.Amount}";
			}
			SpawnFloatingText(pos, text, color, fontSize);
			view.Flash();
		}
		if (ev.IsCrit) Input.VibrateHandheld(60);
	}

	private void RenderDamageToPlayer(BattleEvent ev)
	{
		Log($"[color=#f88]{ev.IntentName ?? "Атака"}: получено {ev.Amount} урона[/color]");
		SpawnFloatingText(new Vector2(150, 100), $"-{ev.Amount}", UIStyle.DangerRed, 28);
		Input.VibrateHandheld(40);
	}

	private void RenderEffectApplied(BattleEvent ev)
	{
		// На цель (враг) или на игрока — определяется EnemyIndex.
		string targetName = ev.EnemyIndex >= 0 && ev.EnemyIndex < _state.Enemies.Count
			? _state.Enemies[ev.EnemyIndex].EnemyName
			: "Вы";
		switch (ev.EffectType)
		{
			case "phys_taken_pct":
				Log($"{targetName} получает +{ev.Amount}% физ. урона на {ev.EffectDuration} ходов.");
				break;
			case "magic_dmg_pct":
				Log($"{targetName} наносит +{ev.Amount}% маг. урона на {ev.EffectDuration} ходов.");
				break;
			case "phys_dmg_pct":
				Log($"{targetName}: ярость +{ev.Amount}% физ. урона ({ev.EffectDuration} ходов).");
				break;
			default:
				Log($"{targetName}: эффект {ev.EffectType} ({ev.EffectDuration} ходов).");
				break;
		}
	}

	private void RenderLootDropped(BattleEvent ev)
	{
		if (ev.DroppedItems == null) return;
		foreach (var item in ev.DroppedItems)
		{
			// Особый префикс "money:N" — выпавшие медяки (CombatEngine.DropLoot).
			// Кошель уже пополнен в engine; здесь только лог.
			if (item.StartsWith("money:"))
			{
				if (long.TryParse(item[6..], out long copper) && copper > 0)
					Log($"[color=#e9a35a]🪙 Монеты: +{Currency.FormatShort(copper)}[/color]");
				continue;
			}
			// item приходит как "itemId×count" — разбираем для лога.
			int x = item.IndexOf('×');
			string itemId = x > 0 ? item[..x] : item;
			string countStr = x > 0 ? item[(x + 1)..] : "1";
			var (name, rarity) = GetItemNameAndRarity(itemId);
			string color = RarityHexColor(rarity);
			Log($"[color={color}]💰 Лут: {name} ×{countStr}[/color]");
		}
	}

	// =====================================================================
	// Lookup helpers
	// =====================================================================

	private static (string name, ItemRarity rarity) GetItemNameAndRarity(string id)
	{
		var w = ItemsDB.GetWeapon(id);
		if (w != null) return (w.Name, w.Rarity);
		var a = ItemsDB.GetArmor(id);
		if (a != null) return (a.Name, a.Rarity);
		var p = PotionsDB.Get(id);
		if (p != null) return (p.Name, p.Rarity);
		return (id, ItemRarity.Common);
	}

	private static string RarityHexColor(ItemRarity r) => r switch
	{
		ItemRarity.Uncommon => "#5af55a",
		ItemRarity.Rare     => "#5aafff",
		ItemRarity.Epic     => "#c878ff",
		_                   => "#dddddd",
	};
}
