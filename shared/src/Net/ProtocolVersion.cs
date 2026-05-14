namespace GuildOfGreed.Shared.Net;

// Версия wire-протокола между клиентом и сервером.
//
// Подъём правила:
//   - Любое изменение в Messages.cs (новое поле, новый тип, переименование)
//     обязано поднимать Current на +1.
//   - Сервер хранит свою константу. Клиент шлёт ClientHello.ProtocolVersion;
//     сервер отвечает ServerWelcome.Compatible.
//   - При несовместимости клиент показывает диалог "Обновитесь" и не пускает
//     дальше handshake'а.
//
// Совместимость определяется на сервере (а не побитовое сравнение): сервер
// решает какие старые версии он ещё поддерживает (например, версия 2 умеет
// читать запросы версии 1).
public static class ProtocolVersion
{
	public const int Current = 17;  // Grade promotion: PromoteGradeRequest + level-per-grade cap (20 уровней на грейд).
}
