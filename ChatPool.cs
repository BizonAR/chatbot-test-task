using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Util;

namespace ChatApp
{
	public class ChatPool
	{
		#region Поля

		private readonly int _maxPoolSize;
		private Stack<Chat> _chatPool;

		#endregion

		#region Конструкторы

		public ChatPool(int maxPoolSize = 50)
		{
			_chatPool = new Stack<Chat>();
			_maxPoolSize = Math.Min(maxPoolSize, CalculateMaxPoolSize());
			LogPoolSize();
		}

		#endregion

		#region Публичные методы

		public Chat GetChat()
		{
			if (_chatPool.Count > 0)
			{
				var chat = _chatPool.Pop();
				LogPoolSize();
				return chat;
			}

			var newChat = new Chat();
			LogPoolSize();
			return newChat;
		}

		public void ReturnChat(Chat chat)
		{
			chat.Reset();

			if (_chatPool.Count < _maxPoolSize)
			{
				_chatPool.Push(chat);
				LogPoolSize();
			}
			else
			{
				Log.Warn(nameof(ChatPool), "Pool full, discarding chat.");
			}
		}

		public void Clear()
		{
			_chatPool.Clear();
			LogPoolSize();
		}

		public int CurrentPoolSize()
		{
			return _chatPool.Count;
		}

		#endregion

		#region Приватные методы

		private int CalculateMaxPoolSize()
		{
			var activityManager = (ActivityManager)Application.Context.GetSystemService(Context.ActivityService);
			var memoryInfo = new ActivityManager.MemoryInfo();
			activityManager.GetMemoryInfo(memoryInfo);
			var availableMemory = memoryInfo.AvailMem / 1024 / 1024;

			return availableMemory < 100
				? 50
				: Math.Min(100, (int)(availableMemory / 2));
		}

		private void LogPoolSize()
		{
			Log.Debug(nameof(ChatPool), $"Current pool size: {_chatPool.Count}/{_maxPoolSize}");
		}

		#endregion
	}
}
