using Godot;
using System;
using System.Linq;

// Combat — игра карты + выбор цели + применение урона.
public partial class Combat
{
	private void OnCardClicked(CardView view)
	{
		if (_combatOver) return;

		// Найти индекс кликнутой карты в руке (по позиции в HBox).
		int idx = -1;
		for (int i = 0; i < _handContainer.GetChildCount(); i++)
			if (_handContainer.GetChild(i) == view) { idx = i; break; }
		if (idx < 0 || idx >= _hand.Count) return;

		var card = view.CardData;
		var p = GameData.Instance.Character;

		if (p.CurrentMp < card.Cost)
		{
			Log($"[color=#888]Недостаточно маны: {card.Name} стоит {card.Cost}.[/color]");
			return;
		}

		bool needsTarget = card.Effect == "damage_phys"
			|| card.Effect == "damage_magic"
			|| card.Effect == "debuff_phys";

		if (!needsTarget)
		{
			PlayCard(idx, null);
			return;
		}

		var alive = _encounter.Where(e => e.CurrentHp > 0).ToList();
		if (alive.Count == 0) return;
		if (alive.Count == 1)
		{
			PlayCard(idx, alive[0]);
			return;
		}

		// Несколько целей — переходим в режим выбора.
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
		int idx = _selectedHandIndex;
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;
		PlayCard(idx, view.Enemy);
	}

	private void CancelTargeting()
	{
		if (_selectedHandIndex < 0) return;
		_selectedHandIndex = -1;
		_targetingBanner.Visible = false;
	}

	private void PlayCard(int handIndex, EnemyData target)
	{
		if (handIndex < 0 || handIndex >= _hand.Count) return;

		// Захватываем view ДО изменения данных — для анимации розыгрыша.
		CardView playedView = null;
		if (handIndex < _handContainer.GetChildCount())
			playedView = _handContainer.GetChild(handIndex) as CardView;

		var cardId = _hand[handIndex];
		var card = CardsDB.GetCard(cardId);
		var p = GameData.Instance.Character;

		p.CurrentMp -= card.Cost;
		Log($"[b]Сыграна карта:[/b] {card.Name} [color=#5af](-{card.Cost} MP)[/color]");

		switch (card.Effect)
		{
			case "damage_phys":
			{
				int dmg = CardsDB.ComputePhysDamage(card, p, target);
				bool isCrit = p.TryConsumeCrit();
				if (isCrit) dmg = (int)Math.Round(dmg * p.CritMultiplier());
				ApplyDamageToEnemy(target, dmg, true, isCrit);
				break;
			}
			case "damage_magic":
			{
				int dmg = CardsDB.ComputeMagicDamage(card, p, target);
				bool isCrit = p.TryConsumeCrit();
				if (isCrit) dmg = (int)Math.Round(dmg * p.CritMultiplier());
				ApplyDamageToEnemy(target, dmg, false, isCrit);
				break;
			}
			case "block":
			{
				int amount = CardsDB.ComputeBlock(card, p);
				p.CurrentBlock += amount;
				Log($"Блок: +{amount} (всего {p.CurrentBlock})");
				SpawnFloatingText(new Vector2(150, 110), $"+{amount} БЛОК", UIStyle.BlockCyan, 22);
				break;
			}
			case "heal":
			{
				int amount = CardsDB.ComputeHeal(card, p);
				int before = p.CurrentHp;
				p.CurrentHp = Math.Min(p.CurrentHp + amount, p.MaxHp());
				int healed = p.CurrentHp - before;
				Log($"[color=#7fa]Исцеление: +{healed} ХП[/color]");
				SpawnFloatingText(new Vector2(150, 110), $"+{healed} ХП", UIStyle.HealGreen, 22);
				break;
			}
			case "debuff_phys":
				if (target != null)
				{
					target.AddEffect("armor_break", "phys_taken_pct", card.AmountPct, card.Duration);
					Log($"{target.EnemyName} получает +{card.AmountPct}% физ. урона на {card.Duration} х.");
				}
				break;
			case "buff_magic":
				p.AddEffect("magic_focus", "magic_dmg_pct", card.AmountPct, card.Duration);
				Log($"Вы наносите +{card.AmountPct}% маг. урона {card.Duration} ходов.");
				break;
		}

		_hand.RemoveAt(handIndex);
		_discard.Add(cardId);

		// Анимация розыгрыша: карта вылетает вверх и тает.
		if (playedView != null) AnimateCardOut(playedView);

		if (AllEnemiesDead()) OnAllEnemiesDead();
		RefreshUI();
	}

	private bool AllEnemiesDead()
	{
		foreach (var e in _encounter)
			if (e.CurrentHp > 0) return false;
		return true;
	}

	private void ApplyDamageToEnemy(EnemyData enemy, int dmg, bool isPhys, bool isCrit = false)
	{
		if (enemy == null) return;
		int defense = isPhys ? enemy.PhysDef : enemy.MagicDef;
		dmg = Math.Max(1, dmg - defense);
		int absorbed = 0;
		if (enemy.CurrentBlock > 0)
		{
			absorbed = Math.Min(enemy.CurrentBlock, dmg);
			enemy.CurrentBlock -= absorbed;
			dmg -= absorbed;
		}
		enemy.CurrentHp = Math.Max(0, enemy.CurrentHp - dmg);
		string kind = isPhys ? "Физ" : "Маг";
		string critTag = isCrit ? "[color=#fc4]🎯 КРИТ![/color] " : "";
		Log(absorbed > 0
			? $"{critTag}→ {enemy.EnemyName}: {kind} урон {dmg} (поглощено блоком: {absorbed})"
			: $"{critTag}→ {enemy.EnemyName}: {kind} урон {dmg}");

		var ev = FindEnemyView(enemy);
		if (ev != null)
		{
			var pos = ev.GlobalPosition + new Vector2(ev.Size.X / 2f, ev.Size.Y * 0.25f);
			Color color;
			int fontSize;
			string text;
			if (isCrit)
			{
				color = new Color(1.0f, 0.85f, 0.20f);
				fontSize = 38;
				text = $"-{dmg}!";
			}
			else
			{
				color = isPhys ? new Color(1.0f, 0.6f, 0.35f) : new Color(0.75f, 0.55f, 1.0f);
				fontSize = 28;
				text = $"-{dmg}";
			}
			SpawnFloatingText(pos, text, color, fontSize);
			ev.Flash();
		}

		// Тактильная отдача на мобильном (на десктопе — no-op).
		if (isCrit) Input.VibrateHandheld(60);

		if (enemy.CurrentHp <= 0)
		{
			Log($"[color=#7f7]✓ {enemy.EnemyName} повержен.[/color]");
			Input.VibrateHandheld(120);
			DropLoot(enemy);
		}
	}

	// Прокатываем таблицу лута врага. Каждая запись — независимый ролл.
	// Цвет лога окрашивается по редкости. Если инвентарь полон — лут теряется.
	private void DropLoot(EnemyData enemy)
	{
		if (enemy.LootTable == null || enemy.LootTable.Count == 0) return;
		foreach (var entry in enemy.LootTable)
		{
			if (!Rng.Chance(entry.Chance)) continue;
			int count = Rng.Range(entry.MinCount, entry.MaxCount + 1);
			if (!GameData.Instance.AddItem(entry.ItemId, count))
			{
				Log($"[color=#f88]Инвентарь полон — {GetItemName(entry.ItemId)} потерян.[/color]");
				continue;
			}
			var (name, rarity) = GetItemNameAndRarity(entry.ItemId);
			string color = RarityHexColor(rarity);
			Log($"[color={color}]💰 Лут: {name} ×{count}[/color]");
		}
	}

	private static string GetItemName(string id) => GetItemNameAndRarity(id).name;

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

	private void ApplyDamageToPlayer(int dmg)
	{
		var p = GameData.Instance.Character;
		dmg = Math.Max(1, dmg - p.PhysDef());
		int absorbed = 0;
		if (p.CurrentBlock > 0)
		{
			absorbed = Math.Min(p.CurrentBlock, dmg);
			p.CurrentBlock -= absorbed;
			dmg -= absorbed;
		}
		p.CurrentHp = Math.Max(0, p.CurrentHp - dmg);
		Log(absorbed > 0
			? $"[color=#f88]Получен урон: {dmg} (поглощено: {absorbed})[/color]"
			: $"[color=#f88]Получен урон: {dmg}[/color]");

		SpawnFloatingText(new Vector2(150, 100), $"-{dmg}", UIStyle.DangerRed, 28);

		// Удар по игроку — короткая вибрация. Урон <= блок? всё равно вибрируем,
		// игрок ощутит что атака случилась.
		Input.VibrateHandheld(40);
	}
}
