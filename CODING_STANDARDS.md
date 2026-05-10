# Coding Standards — Guild of Greed

Последнее обновление: 2026-05-10. Проект — Godot 4.6 .NET (C# / .NET 8) клиент. Сервер планируется отдельно на C# с шарингом Domain/Data через class library.

---

## 1. Структура папок

```
src/
├── Core/        — autoloads, синглтоны, глобальный стейт игры (Godot-aware)
├── Data/        — статические БД (предметы, карты, локации) + расчётные хелперы. Без Godot.
├── Domain/      — POCO-сущности (CharacterData, EnemyData, Rng). Без Godot.
└── <Feature>/   — фичи (Combat, Town, Inventory, Crafting, Auction, Extraction).
                  Каждая фича — свой контроллер сцены + UI + (опционально) UIStyle.
assets/          — арт, звук, шрифты (заполняется по мере)
scenes/          — .tscn-файлы
```

**Правило зависимостей (одностороннее):**

```
Feature → Data → Domain
   ↓
  Core → (Godot)
```

Запрещено: `Domain → Godot`, `Domain → Data`, `Data → Feature`, `Data → Core`.

**Why:** `Domain` и `Data` будут вынесены в shared class library — её подключит и клиент (Godot), и сервер (C# console / ASP.NET). Любой `using Godot;` или ссылка вверх ломает портативность.

## 2. Именование

| Сущность | Конвенция | Пример |
|---|---|---|
| Класс / метод / public свойство | `PascalCase` | `CardData`, `RollIntent()`, `CurrentHp` |
| Приватное поле | `_camelCase` | `_handContainer`, `_combatOver` |
| Параметр / локальная переменная | `camelCase` | `var dmg = ...; void Foo(int handIndex)` |
| Константа / `static readonly` | `PascalCase` | `MaxStat`, `WarriorDeck` |
| String-ID (эффекты, ID карт/предметов, ключи локализации) | `snake_case` английский | `"phys_taken_pct"`, `"sword_1h_low"`, `"ui.combat.end_turn"` |
| Файл | `PascalCase.cs`, имя совпадает с главным классом | `CardView.cs` |

## 3. Форматирование

- **Отступы:** табы.
- **Скобки:** Allman — `{` на новой строке для классов и методов; для switch допустим формат в строку.
- **Длина строки:** ~120 символов мягкий лимит.
- **`var`:** допустим когда тип очевиден из правой части (`var bar = new ProgressBar();`). Иначе явный тип.
- **Скобки у `if`/`for`:** всегда. Однострочные `=> expression` только для тривиальных лямбд.
- **`using` директивы:** `using Godot;` первым (если файл клиентский), потом `System.*`, потом проектные.

## 4. Размер файлов

- **Hard limit:** 500 строк на файл.
- При превышении — split:
  - Godot-классы наследующие Node — через `partial class` в нескольких файлах: `Combat.cs`, `Combat.Cards.cs`, `Combat.UI.cs`, `Combat.Animations.cs`.
  - Прочие классы — выделить ответственность в новый класс.
- **Soft target:** 300 строк. Если файл подбирается к 400 — пора задуматься о разбиении.

**Why:** длинные файлы трудно читать, в них появляются скрытые зависимости между не связанными частями кода, и diff-ы становятся нечитаемыми. Уже разбивали Combat.cs.

## 5. Godot 4 / .NET специфика

- Все классы наследующие `Node`/`Control`/`Resource` — `public partial class`.
- Методы жизненного цикла Godot — PascalCase: `_Ready()`, `_Process()`, `_UnhandledInput()`.
- Сигналы: `[Signal] public delegate void XxxEventHandler(args)`. Эмит — `EmitSignal(SignalName.Xxx, ...)`. Подписка — `view.Xxx += OnXxx;`.
- Tween коллбэки — `Callable.From(action)` или `Callable.From<T>(method)`.
- Вложенные enum'ы Godot квалифицировать когда не очевидно: `BoxContainer.AlignmentMode.Center`, `Control.MouseFilterEnum.Stop`.
- `MouseFilter` ставится явно на каждый интерактивный Control (не полагайся на default).
- `IsInstanceValid(node)` проверять перед операциями над Godot-объектами из задержанных коллбэков (Tween/Timer).

## 6. Архитектурные правила

- **Domain** — POCO. Никаких `using Godot;`. Будут вынесены в shared library.
- **Data** — `public static class XxxDB`. Содержит словари + чистые расчётные хелперы (`ComputePhysDamage`, `DescribeCurrent`, `DescribeFormula`). Тоже portable.
- **Core/GameData** — единственный синглтон через `public static GameData Instance { get; private set; }`. Назначается в `_Ready`.
- **Feature/View-классы** (`CardView`, `EnemyView`) — только отображение, шлют сигналы, не меняют игровую модель.
- **Feature/Controller** (`Combat`) — координирует View и Domain через сигналы и явные вызовы.

## 7. Игровые паттерны

- Расчёт урона/блока/хила — единый источник правды в `CardsDB.Compute*`. View вызывает их же для отображения, чтобы не было расхождения "обещанного" и "фактического".
- Эффекты (баффы/дебаффы) идентифицируются `Type` строкой (`"phys_taken_pct"`, `"magic_dmg_pct"`). Хранятся в `List<StatusEffect>` на сущности.
- Длительности эффектов тикают через `TickEffects()` — единое поведение у `CharacterData` и `EnemyData`.
- **Новый тип карты** → `CardsDB.Cards` + если новое поведение — case в `Combat.PlayCard` switch.
- **Новый враг** → factory-метод в `EnemyData.CreateXxx()`.
- **Новая локация** → пункт в `GameData.LocationNames` + кейс в `SpawnEnemies()`.
- **Random** — только через `Rng.Next(max)` / `Rng.NextFloat()` (Domain/Rng.cs). Не использовать `GD.Randi()` или `new System.Random()` напрямую в Domain/Data.

## 8. UI / визуал

- Все цвета/стили — через `UIStyle` (палитра + фабрики). Никаких magic-color literals в feature-коде.
- Кнопки → `UIStyle.StyleButton(btn, primary?)`.
- Прогрессбары → `UIStyle.StyleProgressBar(bar, fill, empty)` (НЕ через `Modulate`).
- Лейблы поверх контента — `UIStyle.MakeLabel(text, size, color)` (с outline для читаемости).
- Layout — Container'ы (`HBoxContainer`/`VBoxContainer`/`PanelContainer`). `Position` — только для внешнего размещения относительно root сцены.

## 9. Анимации (Tween)

- Использовать `CreateTween()` для transient анимаций UI/боя.
- Перед запуском нового твина по той же цели — `_tween?.Kill()`.
- Easing по умолчанию: `Cubic` + `Out`.
- Длительности: 0.1–0.2с — UI feedback (hover, нажатие); 0.3–0.5с — розыгрыш карты, флэш врага; 0.6–1.0с — нарративные beats (всплывающий урон).
- В конце цепочки твинов на временных объектах — `TweenCallback(Callable.From(node.QueueFree))`.

## 10. Локализация (мультиязычность)

- Все UI-строки и игровой текст должны быть локализуемы через `Lang.T(key)`.
- Ключи — английские snake_case с пространством имён через точку: `"ui.combat.end_turn"`, `"card.strike.name"`, `"item.sword_1h_low.name"`, `"log.card_played"`.
- Параметры через format-args: `Lang.T("log.damage_dealt", enemyName, dmg)`.
- Локали хранятся в `src/Core/Locales/<lang>.cs` (или единый `Lang.cs` со словарями по локалям для прототипа).
- Дефолтная локаль — `Locale.Ru`. Английский — fallback и для будущего экспорта.
- На прототипе допустимо хардкодить русские строки в UI и постепенно мигрировать на `Lang.T()`. Новый код — сразу через `Lang.T()`.

## 11. Разрешения (permissions)

- Роль игрока хранится в `UserSession.CurrentRole` (доступ через `GameData.Instance.Session`).
- Проверка действия: `GameData.Instance.Session.Can(Permission.X)`.
- Роли: `Admin`, `Moderator`, `Player`, `Guest`.
- Permissions определены в `Core/Permissions.cs` как enum + словарь `Role → HashSet<Permission>` в `PermissionsDB`.
- На прототипе клиент проверяет локально (для UX). На сервере (когда появится) — авторитетная проверка тех же `PermissionsDB.Has(...)`. Поэтому `PermissionsDB` лежит так, чтобы быть портативным — без зависимостей от Godot.

## 12. Client/Server подготовка

- Текущий код — клиент-only прототип. Но пишем как будто сервер уже на горизонте:
  - Игровая логика (формулы урона, эффекты, прогрессия) — в `Data/`. Server подключит как библиотеку и будет авторитетно вычислять то же самое.
  - Состояние сущностей (`CharacterData`, `EnemyData`) — POCO без Godot. Сериализуется в JSON для сети.
  - Никаких UI-вызовов из Domain/Data.
  - Random через `Rng` — на сервере подменим реализацию на seeded для replay/детерминизма.
- Когда сервер начнётся: создать `GuildOfGreed.Core.csproj` (class library), переместить туда `src/Domain` и `src/Data`. И клиент, и сервер будут ссылаться на этот проект.

## 13. Комментарии и язык

- **Идентификаторы и string-ID — английский.** Пример: `armor_break`, `phys_taken_pct`, `WarriorDeck`.
- **Комментарии и текст для игрока — русский.** Пример: `_log("Сыграна карта: ...")`, `// Реген маны на 1м ходу.`
- Класс/метод с непростой ролью — короткий `//` на 1–3 строки сверху о цели и инвариантах.
- Не комментировать очевидное (`// increment counter` над `i++`).
- TODO/FIXME с контекстом: `// TODO(combat): добавить AOE-карту`.

## 14. Безопасность и null

- На всех публичных входных точках — валидация индексов и null-проверки. Не падаем — логируем и `return`.
- `?.` и `??` для цепочек обращения к Equipment/Weapon/Armor.
- `Math.Max(1, dmg)` для урона — не наносим 0.
- При неуверенности в живости Godot-узла — `IsInstanceValid`.

## 15. Self-check после каждой задачи

После любого изменения **до сообщения "готово" или коммита** пройтись по списку:

1. **Компиляция (ментально):** все типы и using-директивы на месте, нет ссылок на удалённые поля/методы.
2. **Зависимости:** при изменении публичного API — обновить все вызовы в проекте (Grep, не только Edit).
3. **Layer-правила:** `Domain` и `Data` без `using Godot;`. Нет циклов.
4. **Размер файла:** не превышен 500. Если близко — split.
5. **Стандарты:** именование, отступы, outline на лейблах, UIStyle для цветов.
6. **Verify by Grep:** при переименовании или удалении — Grep по старому имени, чтобы убедиться что нет orphan-ссылок.

**Why:** редактор Godot не запускается после каждого инкремента — ответственность на разработчике (или Claude).

## 16. Git

- Коммиты — императив, английский, < 70 символов первая строка: `Add boss enemy`, `Fix card play animation`.
- Тело коммита (если нужно) объясняет WHY, не WHAT.
- Один коммит = один логический инкремент. Не складировать смесь.
- Не коммитим `.godot/`, `bin/`, `obj/`, `.vs/`, `.vscode/`, `.claude/settings.local.json` (в .gitignore).

## 17. Анти-паттерны (НЕ делать)

- `using Godot;` в `Domain/` или `Data/`.
- Дублировать формулы между логикой и отображением.
- `GD.Print` в продакшен-логике (только для дебага, удалять перед коммитом).
- Менять стейт изнутри View-классов (CardView не меняет CurrentMp).
- Передавать чистые C# POCO через Godot-сигналы (передавать сам Node, у которого внутри POCO).
- Magic numbers — балансные числа (HP/урон/мульты) в БД, UI-числа в `UIStyle`, ключи локализации в `Lang`.
- Файл > 500 строк без split.
- `GD.Randi()` / `new Random()` напрямую — только через `Rng`.
