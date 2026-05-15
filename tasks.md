# Guild of Greed — задачи до первого реального теста

Дата составления: 2026-05-14
Цель: закрытый тест с 20–50 живыми игроками через 2–4 недели.

---

## Карта реализованных фич (что уже есть)

### Боевая система
- Детерминированный движок [shared/src/Combat/CombatEngine.cs](shared/src/Combat/CombatEngine.cs) — серверный авторитет.
- Действия: `PlayCard`, `UsePotion`, `EndTurn`, `Flee`. Только одиночная цель — **AoE нет**.
- 19 уникальных карт ([CardsDB.cs](shared/src/Data/CardsDB.cs)): 4 воин + 4 маг + 9 апгрейдов оружия (только ур.1).
- Статус-эффекты: `phys_taken_pct`, `magic_dmg_pct`, `balanced_shield_buff`, кровотечение.
- Стартовые колоды: Warrior / Mage / TwoHander / Knife.

### Контент
- ~62 врага в 3 файлах ([EnemyData.cs](shared/src/Domain/EnemyData.cs), [.Tier2.cs](shared/src/Domain/EnemyData.Tier2.cs), [.Tier3.cs](shared/src/Domain/EnemyData.Tier3.cs)): E → ранний D → поздний D → C-trial.
- 9 боссов (по одному на локацию).
- AI — рандом из фикс. списка интентов.
- 9 локаций в [MapGenerator.cs](shared/src/Data/MapGenerator.cs).
- 17 артефактов ([ArtifactsDB.cs](shared/src/Data/ArtifactsDB.cs)).
- 8 run-эффектов ([RunEffectsDB.cs](shared/src/Data/RunEffectsDB.cs)).

### Экипировка / предметы
- 8 слотов экипировки, dual-wield/щит на OffHand (UI неполный).
- 4 типа оружия × E/D × low/mid/top = ~24 ID.
- 3 архетипа брони × 4 слота — ~40+ ID.
- 3 щита — **только E-low** (дыра).
- 6 редкостей, 6 грейдов (E→S) с overlap.
- 16 аффиксов (8 префиксов + 8 суффиксов) в [AffixesDB.cs](shared/src/Data/AffixesDB.cs).
- 4 уникальных пассива оружия: PowerPerNonAttack, BleedOnHit, MagicChain, CritBonus.
- 12 сетов ([SetsDB.cs](shared/src/Domain/SetsDB.cs)) — C/B шаблонные.

### Крафт / экономика
- 7 скиллов крафта 0–100 ([CraftingDB.cs](shared/src/Data/CraftingDB.cs)), грейд-кэп, XP-кривая.
- 10 ресурсов (5 типов × E/D).
- Рецепты только E/D, **C/B/A/S рецептов нет**.
- Шоп ([ShopDB.cs](shared/src/Data/ShopDB.cs)): 7 зелий + 4 E-low оружия, выкуп 40%.
- Кузница ([ForgeDB.cs](shared/src/Data/ForgeDB.cs)): распыл, апгрейд редкости, реролл аффиксов.
- Валюты: медь/серебро/золото 100:1:1 + магическая эссенция.

### Прогрессия персонажа
- Сквозной Level, 20 уровней/грейд, грейды E→S.
- 6 статов (STR/INT/CON/WIT/MEN/DEX), +2 очка за уровень.
- XP оружия per-type, XP скиллов крафта per-skill.
- C-trial автопромо через локацию 8.

### Клиент (Godot 4.6)
- Полная цепочка: Connecting → Auth → CharacterSelect → CharacterCreation → LocationSelect → MapView → Combat → Town(Stash/Shop/Forge/Crafting/Grade) → Inventory.
- Туториал по экипировке после первого боя.
- Реконнект-оверлей.
- Единый [UIStyle.cs](client/src/Combat/UIStyle.cs).
- Локализация — скелет на Ru (~10 ключей).

### Сервер / сеть
- TCP+TLS 1.2/1.3, self-signed cert, TOFU-пиннинг.
- MessagePack, protocol v18, 1 MiB cap.
- PBKDF2-SHA256 100k итераций, сессии 30 дней.
- Серверный авторитет в бою.
- Command-pattern для мутаций в [Session.cs](server/src/Session.cs).
- SQLite + 1 миграция, `CharacterData` хранится JSON-блобом.

---

## Топ-проблемы (по убыванию критичности)

| # | Проблема | Где | Почему критично |
|---|------|-----|------|
| 1 | Бой не переживает дисконнект | [Main.cs:100](client/src/Core/Main.cs#L100), `BattleSession` in-memory | Любой обрыв сети → потерянный run |
| 2 | TLS self-signed + TOFU | [TlsCertificate.cs](server/src/TlsCertificate.cs), [ServerTrustStore.cs](client/src/Net/ServerTrustStore.cs) | MITM на первом коннекте, проблемы с corporate Wi-Fi |
| 3 | Нет rate-limit на auth/команды | [Session.cs:182](server/src/Session.cs#L182) | Перебор паролей, спам соединений |
| 4 | Нет восстановления пароля | [AccountStore.cs](server/src/AccountStore.cs) | Забыл пароль = аккаунт мёртв |
| 5 | Dev-режим в GradeOverlay | [GradeOverlay.cs:4](client/src/Town/GradeOverlay.cs#L4) | Бесплатный апгрейд грейда без требований |
| 6 | Валидация input в бою не делается | [Session.cs:397](server/src/Session.cs#L397) | Кривой индекс → исключение, дисконнект |
| 7 | Захардкоженный стартовый бой | [Main.cs:299](client/src/Core/Main.cs#L299) | `LocationOverride=1`, `NodeTypeOverride=(int)Tutorial` |
| 8 | Локализация ~10 ключей из сотен | [Lang.cs](client/src/Core/Lang.cs) | Игрок видит `snake_case.dotted` |
| 9 | Тестов нет | весь проект | Любая правка ломает client↔server синхру незаметно |
| 10 | Контента мало для retention | CardsDB(19), Artifacts(17), RunEffects(8) | Игрок «видит всё» за 2 вечера |
| 11 | Дисбаланс на тестовых цифрах | везде | Ничто не сбалансировано на живых игроков |
| 12 | Щиты только E-low | [ShieldsDB.cs](shared/src/Data/ShieldsDB.cs) | Прогрессирующего игрока с щитом некуда вести |

---

## Этап A — «безопасно запустить» ✅ ВЫПОЛНЕН

- [x] **A1. Persistence боя на сервере.**
  Миграция БД v3 → `active_battles`. `BattleSnapshotDto` в shared с RNG-восстановлением через `(seed, RngCalls)`. Save после каждого ApplyAction, clear на ended/EndRun. `SelectCharacterResponse.HasActiveBattle` → клиент сразу в Combat в режиме resume. Protocol 19 → 20.

- [x] **A2. Bounds-валидация в `HandleBattleAction`.**
  Проверка ActionType, HandIndex, TargetEnemyIndex, PotionId. Структурированные ошибки `bad_hand_index` / `bad_target_index` / `bad_action_type` / `bad_potion_id` вместо ArgumentOutOfRangeException.

- [x] **A3. Rate-limit на auth и команды.**
  `RateLimiter` (sliding window) в [server/src/RateLimiter.cs](server/src/RateLimiter.cs). Per-session: 60 cmd/sec, 5 auth/30 sec. На клиенте — перевод `rate_limited`.

- [x] **A4. Убрать dev-режим из GradeOverlay.**
  Промоушн требует `IsAtGradeCap()` + плата (E→D=1k, …, A→S=50k медяков). Trial-локации остаются как авто-альтернатива.

- [x] **A5. Структурированные логи в файл.**
  `Logger.ConfigureFileSink(dataDir)` → `data/logs/server-YYYY-MM-DD.log`, суточная ротация. Console-вывод сохраняется.

- [x] **A6. Crash-handler на клиенте.**
  `CrashLogger` на AppDomain.UnhandledException + TaskScheduler.UnobservedTaskException, пишет в `user://crashes/`.

- [x] **A7. Убрать `catch { }` в OnLogoutRequested.**
  Логируем ServerException/Exception в `GD.Print` вместо silent swallow.

---

## Этап Б — «контент не должен закончиться за вечер» (1–2 недели)

- [ ] **Б1. Расширить пул карт x2–3** (целить в ~50 карт).
  Минимум одна AoE-механика. Если AoE — расширить `BattleAction.TargetEnemyIndex` до `Targets[]` или ввести `TargetMode` (single/all/random).
  Файл: [CardsDB.cs](shared/src/Data/CardsDB.cs), [BattleAction.cs](shared/src/Combat/BattleAction.cs).

- [ ] **Б2. Дерево апгрейдов карт за уровни оружия 2–5.**
  Сейчас обрыв на ур.1.
  Файл: [CardsDB.cs:149](shared/src/Data/CardsDB.cs#L149) (DeckUpgrades).

- [ ] **Б3. D-грейд щитов + C/B нешаблонные сеты.**
  Файлы: [ShieldsDB.cs](shared/src/Data/ShieldsDB.cs), [SetsDB.cs](shared/src/Domain/SetsDB.cs).

- [ ] **Б4. +10 артефактов, +6 run-эффектов.**
  Уклон в синергии оружие↔артефакт.
  Файлы: [ArtifactsDB.cs](shared/src/Data/ArtifactsDB.cs), [RunEffectsDB.cs](shared/src/Data/RunEffectsDB.cs).

- [ ] **Б5. AI врагов с очередью интентов.**
  2–3 шаблона за врага, телеграфирование следующего хода.
  Файлы: [EnemyData.cs](shared/src/Domain/EnemyData.cs), [CombatEngine.cs](shared/src/Combat/CombatEngine.cs).

- [ ] **Б6. C/B/A/S рецепты крафта.**
  Сейчас только E/D, выше — генерация только из ItemsDB.
  Файл: [CraftingDB.cs:251](shared/src/Data/CraftingDB.cs#L251).

---

## Этап В — «UX без острых углов» (параллельно с Б)

- [ ] **В1. Локализация Ru на 100% UI.**
  Пройти все экраны, заполнить ключи.
  Файл: [Lang.cs](client/src/Core/Lang.cs).

- [ ] **В2. Заменить захардкоженные `LocationOverride=1` / `NodeTypeOverride=(int)Tutorial`.**
  Именованные константы или enum-ссылки.
  Файл: [Main.cs:299](client/src/Core/Main.cs#L299).

- [ ] **В3. Туториал для боя.**
  Сейчас только для экипировки.
  Файл: [EquipmentTutorialView.cs](client/src/Tutorial/EquipmentTutorialView.cs) (по образцу).

- [ ] **В4. Информативные экраны ошибок.**
  Сейчас на ошибках в основном молчание или disconnect.

- [ ] **В5. UI для выбора dual-wield vs щит на OffHand.**
  Слот есть, переключателя нет.
  Файлы: [CharacterData.cs:52](shared/src/Domain/CharacterData.cs#L52), [InventoryOverlay.cs](client/src/Inventory/InventoryOverlay.cs).

---

## Этап Д — Создание персонажа (бесклассовая система)

Дизайн-решения (зафиксированы): имя + статы (35–45 рандом + 10 очков, без капов), оружие из стартового боя, респек статов за деньги в Гильдии.

- [ ] **Д1. Валидация имени на сервере.**
  Длина 3–20 символов, разрешённые символы (Ru/En + цифры + `-`/`_`), trim.
  Файл: [AccountStore.cs:301](server/src/AccountStore.cs#L301) (`IsValidCharStats` → `IsValidCharData`).

- [ ] **Д2. Валидация каждого стата отдельно.**
  Сейчас проверяется только сумма 210..280 — пропускает `STR=280, остальные=0`. Каждый стат должен быть в диапазоне `[35, 45 + max_single_bonus]`.
  Файл: [AccountStore.cs:301](server/src/AccountStore.cs#L301).

- [ ] **Д3. Real-time валидация имени на клиенте.**
  Подпись под `_nameInput` с цветом (зелёный/красный). Кнопка «Начать игру» disabled если имя невалидно.
  Файл: [CharacterCreation.cs](client/src/CharacterCreation/CharacterCreation.cs).

- [ ] **Д4. Confirm-диалог перед созданием.**
  Показывать итоговое имя + статы, два явных шага «Подтвердить / Отмена».

- [ ] **Д5. Loading-state на «Начать игру».**
  Кнопка disabled + текст «Создание...» пока летит сетевой запрос.
  Файл: [Main.cs:262](client/src/Core/Main.cs#L262) `OnCharacterCreationConfirmed`.

- [ ] **Д6. Показ ошибок сервера на клиенте.**
  Вместо `GD.PrintErr` + откат на CharacterSelect — показать сообщение прямо в CharacterCreation.
  Файл: [Main.cs:270](client/src/Core/Main.cs#L270).

- [ ] **Д7. Кнопка «Назад» в CharacterCreation.**
  Возврат в CharacterSelect без создания.

- [ ] **Д8. Profanity filter и резервные имена.**
  Простой блек-лист на сервере (admin, gm, moderator, мат). Можно начать с маленького списка.

- [ ] **Д9. Respec статов в Гильдии.**
  Новая команда `RespecStatsRequest` в [CharacterCommands.cs](shared/src/Commands/CharacterCommands.cs).
  Сбрасывает распределённые очки в `UnspentStatPoints`, возвращая базу 35–45 (та что выпала при создании? или фиксированная 40?). Цена растёт с уровнем.
  Новый overlay в Town рядом с [GradeOverlay.cs](client/src/Town/GradeOverlay.cs).

- [ ] **Д10. Удаление персонажа — безопасное.**
  Confirm с вводом имени персонажа для подтверждения. Soft-delete на сервере (`deleted_at` колонка), восстановление в течение 7 дней. Cooldown на удаления (например 1 в сутки).
  Файлы: [CharacterSelectView.cs](client/src/Auth/CharacterSelectView.cs), [AccountStore.cs](server/src/AccountStore.cs), новая миграция БД.

---

## Этап Г — «production-grade» (после первого теста)

- [ ] Реальный CA-сертификат (Let's Encrypt + домен), отказ от TOFU.
- [ ] Восстановление пароля по email.
- [ ] Argon2id вместо PBKDF2 (опционально).
- [ ] xUnit-тесты: `CombatEngine`, `CharacterCommands`, `CardsDB.Compute*` — особенно детерминизм по seed.
- [ ] Балансировка по телеметрии (начать собирать с этапа А).
- [ ] Модерация: ban_reason/banned_at в accounts, проверка при login.
- [ ] Аудит-лог критических операций.
- [ ] Поддержка 2–3 protocol версий одновременно (graceful deprecation).
- [ ] Redis cache для sessions.

---

## С чего начать завтра

Самая дешёвая и самая высокоценная пара:

1. **A1 — Battle persistence** (1–2 дня): спасает весь тест.
2. **A2 — Bounds-валидация в HandleBattleAction** (1 час): закрывает целый класс крашей.

После — параллельно тянуть Б (контент) и В (UX-полишинг). А+часть В = обязательный зелёный свет для теста.
