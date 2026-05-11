// Сессия текущего игрока на клиенте.
// На прототипе живёт всё время в GameData.Instance.Session.
// Когда появится сервер — Role будет приходить с сервера при логине.
//
// Использование:
//   if (GameData.Instance.Session.Can(Permission.AccessAuction)) { ... }
public class UserSession
{
	public string UserId = "local";       // на сервере — реальный UID
	public string DisplayName = "Игрок";
	public Role Role = Role.Player;       // дефолт для прототипа

	public bool Can(Permission permission) => PermissionsDB.Has(Role, permission);
}
