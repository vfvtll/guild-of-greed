using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Реестр крафтовых ресурсов (руда / кожа / ткань / дерево / магическая эссенция)
// по грейдам. Базовый уровень: только E и D — этого хватает для рецептов
// E/D-предметов из ItemsDB + ItemsCatalog.
//
// Ресурсы — стакаемые предметы инвентаря (Inventory.TryAdd с maxStack=MaxStack).
// Они НЕ создают instance-объектов, не имеют аффиксов и редкости.
//
// ID имеют формат "res_<kind>_<grade>", в нижнем регистре грейд:
//   res_ore_e, res_leather_e, res_cloth_e, res_wood_e, res_essence_e
//   res_ore_d, res_leather_d, res_cloth_d, res_wood_d, res_essence_d
//
// Дроп: см. EnemyData LootTable — все мобы существующих локаций кидают
// ресурсы случайным образом (см. EnemyData.AddResourceDrops).
public static class ResourcesDB
{
	public const int MaxStack = 999;

	public enum Kind
	{
		Ore,        // руда — тяжёлая броня, оружие
		Leather,    // кожа — лёгкая броня
		Cloth,      // ткань — роба
		Wood,       // дерево — посохи, древки, луки
		Essence,    // магическая эссенция — топовые элементы, магия
	}

	public class ResourceData
	{
		public string Id;
		public Kind Kind;
		public string Grade;
		public string Name;
		public string Icon;
	}

	private static readonly Dictionary<string, ResourceData> All = new();

	static ResourcesDB()
	{
		Register(Kind.Ore,     "E", "Сырая руда",          "⛏");
		Register(Kind.Leather, "E", "Сырая кожа",          "🪶");
		Register(Kind.Cloth,   "E", "Грубая ткань",        "🧵");
		Register(Kind.Wood,    "E", "Свежее дерево",       "🌲");
		Register(Kind.Essence, "E", "Тусклая эссенция",    "✨");

		Register(Kind.Ore,     "D", "Очищенная руда",      "⛏");
		Register(Kind.Leather, "D", "Выделанная кожа",     "🪶");
		Register(Kind.Cloth,   "D", "Плотная ткань",       "🧵");
		Register(Kind.Wood,    "D", "Закалённое дерево",   "🌲");
		Register(Kind.Essence, "D", "Чистая эссенция",     "✨");
	}

	private static void Register(Kind k, string grade, string name, string icon)
	{
		string id = $"res_{KindCode(k)}_{grade.ToLowerInvariant()}";
		All[id] = new ResourceData { Id = id, Kind = k, Grade = grade, Name = name, Icon = icon };
	}

	public static string KindCode(Kind k) => k switch
	{
		Kind.Ore => "ore", Kind.Leather => "leather", Kind.Cloth => "cloth",
		Kind.Wood => "wood", Kind.Essence => "essence",
		_ => "unknown",
	};

	public static string Id(Kind k, string grade)
		=> $"res_{KindCode(k)}_{grade.ToLowerInvariant()}";

	public static ResourceData Get(string id)
		=> id != null && All.TryGetValue(id, out var r) ? r : null;

	public static bool IsResource(string id) => Get(id) != null;

	public static IEnumerable<ResourceData> AllResources() => All.Values;

	public static IEnumerable<ResourceData> ByGrade(string grade)
	{
		foreach (var r in All.Values)
			if (r.Grade == grade) yield return r;
	}
}
