using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Роли и разрешения. Портативно (без Godot) — будет шариться с сервером.
//
// Использование:
//   GameData.Instance.Session.Can(Permission.AccessAuction)
//   PermissionsDB.Has(role, Permission.AdminPanel)
//
// На клиенте проверка локальная (для UX — спрятать/задизейблить кнопку).
// На сервере — авторитетная проверка тех же правил перед выполнением действия.
public enum Role
{
	Guest,       // не залогинен / банное состояние
	Player,      // обычный игрок
	Moderator,   // может банить, видеть жалобы, читать лог чата
	Admin,       // полный доступ, отладочные команды, экономика
}

public enum Permission
{
	// Базовый геймплей
	AccessCombat,
	AccessTown,
	AccessAuction,
	AccessCrafting,
	UseChat,

	// Прогрессия
	GradeUp,
	OpenLockedDungeons,

	// Модерация
	KickPlayer,
	BanPlayer,
	ViewModerationLog,

	// Админ
	EditEconomy,
	SpawnItems,
	OpenDebugPanel,
	OpenAdminPanel,
}

public static class PermissionsDB
{
	private static readonly Dictionary<Role, HashSet<Permission>> RolePermissions = new()
	{
		[Role.Guest] = new HashSet<Permission>
		{
			Permission.AccessCombat,
		},
		[Role.Player] = new HashSet<Permission>
		{
			Permission.AccessCombat,
			Permission.AccessTown,
			Permission.AccessAuction,
			Permission.AccessCrafting,
			Permission.UseChat,
			Permission.GradeUp,
			Permission.OpenLockedDungeons,
		},
		[Role.Moderator] = new HashSet<Permission>
		{
			Permission.AccessCombat,
			Permission.AccessTown,
			Permission.AccessAuction,
			Permission.AccessCrafting,
			Permission.UseChat,
			Permission.GradeUp,
			Permission.OpenLockedDungeons,
			Permission.KickPlayer,
			Permission.BanPlayer,
			Permission.ViewModerationLog,
		},
		[Role.Admin] = new HashSet<Permission>(System.Enum.GetValues<Permission>()),
	};

	public static bool Has(Role role, Permission permission)
		=> RolePermissions.TryGetValue(role, out var set) && set.Contains(permission);

	public static IReadOnlySet<Permission> All(Role role)
		=> RolePermissions.TryGetValue(role, out var set) ? set : new HashSet<Permission>();
}
