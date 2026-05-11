namespace GuildOfGreed.Server.Db;

// Одна миграция схемы БД. Список всех миграций живёт в Migrations.All.
public class Migration
{
	public int Version;
	public string Description;
	public string Sql;
}
