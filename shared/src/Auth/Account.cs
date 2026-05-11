using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Auth;

// Аккаунт игрока — серверная сущность.
//   Login           — уникальный, используется для входа.
//   Email           — уникальный, для восстановления пароля (отдельный flow позже).
//   PasswordHash    — формат PasswordHasher (PBKDF2 строка).
//   Characters      — список персонажей этого аккаунта (N слотов).
//
// PasswordHash НИКОГДА не отдаётся клиенту — сериализуется только в
// серверной БД. Клиенту шлётся AccountSummary через wire-сообщения.
public class Account
{
	public Guid Id;
	public string Login;
	public string Email;
	public string PasswordHash;
	public DateTime CreatedAt;
	public List<CharacterData> Characters = new();
}

// Активная сессия — связывает opaque token с AccountId.
// Клиент сохраняет Token локально, при следующем старте шлёт ResumeSession.
public class AccountSession
{
	public string Token;        // Криптослучайные ~32 байта в base64.
	public Guid AccountId;
	public DateTime IssuedAt;
	public DateTime ExpiresAt;  // По умолчанию +30 дней.
}
