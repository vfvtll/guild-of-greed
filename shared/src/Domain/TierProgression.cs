namespace GuildOfGreed.Shared.Domain;

// Шкала множителей силы для (Grade × Rank). Используется и ItemsCatalog
// (статы заглушечных комплектов), и SetsDB (магнитуды сет-бонусов), чтобы
// прогрессия предметов и бонусов поднималась согласованно.
//
// Логика: middle грейда X по силе ≈ low грейда (X+1), но low следующего
// чуть выше mid предыдущего — L2-style overlap, апгрейд по грейду
// должен ощущаться всегда:
//   E low 1.00 → E mid 1.40 → E top 1.90
//   D low 1.55 → D mid 2.15 → D top 2.90
//   C low 2.30 → C mid 3.10 → C top 4.20
//   B low 3.35 → B mid 4.50 → B top 6.00
//   A low 4.80 → A mid 6.40 → A top 8.50
//   S low 6.80 → S mid 9.00 → S top 12.00
//
// A и S сейчас не имеют каталожных предметов, но магнитуды аффиксов и веса
// rarity-ролла на A/S уже определены — fallback на 1.0 (как было) обнулил бы
// статы базы у будущих A/S предметов до E-low. Закрываем эту дыру заранее.
public static class TierProgression
{
	public static float Mult(string grade, string rank)
	{
		return (grade, rank) switch
		{
			("E", "low") => 1.00f,
			("E", "mid") => 1.40f,
			("E", "top") => 1.90f,
			("D", "low") => 1.55f,
			("D", "mid") => 2.15f,
			("D", "top") => 2.90f,
			("C", "low") => 2.30f,
			("C", "mid") => 3.10f,
			("C", "top") => 4.20f,
			("B", "low") => 3.35f,
			("B", "mid") => 4.50f,
			("B", "top") => 6.00f,
			("A", "low") => 4.80f,
			("A", "mid") => 6.40f,
			("A", "top") => 8.50f,
			("S", "low") => 6.80f,
			("S", "mid") => 9.00f,
			("S", "top") => 12.00f,
			_            => 1.00f,
		};
	}
}
