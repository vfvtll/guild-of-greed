using System;
using System.Collections.Generic;

namespace GuildOfGreed.Server;

// Простой sliding-window rate-limiter. Хранит timestamps последних событий
// и при каждом Allow() обрезает окно по now-window. Если в окне >= limit —
// отказ.
//
// Используется per-Session (не shared между соединениями): пер-IP лимит даёт
// настоящую защиту от brute-force, но требует thread-safe global store —
// добавим позже. Сейчас защищаемся от:
//   - спам команд из одного соединения (cmd-flood)
//   - перебор пароля в одной TCP-сессии (auth-attempts)
public sealed class RateLimiter
{
	private readonly int _limit;
	private readonly TimeSpan _window;
	private readonly Queue<DateTime> _events = new();

	public RateLimiter(int limit, TimeSpan window)
	{
		_limit = limit;
		_window = window;
	}

	public bool Allow()
	{
		var now = DateTime.UtcNow;
		var threshold = now - _window;
		while (_events.Count > 0 && _events.Peek() < threshold)
			_events.Dequeue();
		if (_events.Count >= _limit) return false;
		_events.Enqueue(now);
		return true;
	}

	public int CurrentCount
	{
		get
		{
			var threshold = DateTime.UtcNow - _window;
			while (_events.Count > 0 && _events.Peek() < threshold)
				_events.Dequeue();
			return _events.Count;
		}
	}
}
