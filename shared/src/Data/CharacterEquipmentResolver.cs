using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Из строковых ID-ов в CharacterData.Equipped* достаём реальные WeaponData/
// ArmorData объекты и кладём в [JsonIgnore]-поля. Используется и клиентом
// (после загрузки JSON с сервера), и сервером (после Load из SQLite перед
// расчётом боя через CombatEngine).
public static class CharacterEquipmentResolver
{
	public static void ResolveEquipment(this CharacterData ch)
	{
		if (ch == null) return;
		ch.Weapon = ItemsDB.GetWeapon(ch.EquippedWeaponId)?.Clone();
		ch.Chest  = ItemsDB.GetArmor(ch.EquippedChestId)?.Clone();
		ch.Helmet = ItemsDB.GetArmor(ch.EquippedHelmetId)?.Clone();
		ch.Gloves = ItemsDB.GetArmor(ch.EquippedGlovesId)?.Clone();
		ch.Boots  = ItemsDB.GetArmor(ch.EquippedBootsId)?.Clone();
		ch.Amulet = ItemsDB.GetArmor(ch.EquippedAmuletId)?.Clone();
		ch.Ring1  = ItemsDB.GetArmor(ch.EquippedRing1Id)?.Clone();
		ch.Ring2  = ItemsDB.GetArmor(ch.EquippedRing2Id)?.Clone();
	}
}
