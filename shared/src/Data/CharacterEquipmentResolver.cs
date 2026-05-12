using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// До И6.2 этот класс резолвил строковые Id в WeaponData/ArmorData объекты
// после JSON-десериализации. С И6.2 экипировка хранится как полные instance-
// объекты прямо в CharacterData — резолв не нужен. Метод оставлен как no-op,
// чтобы внешние вызовы (если где-то ещё остались) компилировались.
public static class CharacterEquipmentResolver
{
	public static void ResolveEquipment(this CharacterData ch)
	{
		// no-op (И6.2)
	}
}
