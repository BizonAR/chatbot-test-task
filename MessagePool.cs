using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Util;
using Java.Lang;

namespace ChatApp
{
	public class MessagePool
	{
		private Stack<Message> _messagePool;
		private readonly int _maxPoolSize;

		public MessagePool(int maxPoolSize = 100)
		{
			_messagePool = new Stack<Message>();
			_maxPoolSize = System.Math.Min(maxPoolSize, CalculateMaxPoolSize());
			LogPoolSize();
		}

		private int CalculateMaxPoolSize()
		{
			var activityManager = (ActivityManager)Android.App.Application.Context.GetSystemService(Context.ActivityService);
			var memoryInfo = new ActivityManager.MemoryInfo();
			activityManager.GetMemoryInfo(memoryInfo);
			var maxMemory = Runtime.GetRuntime().MaxMemory() / 1024 / 1024;
			var availableMemory = memoryInfo.AvailMem / 1024 / 1024;
			return availableMemory < 100 || maxMemory < 256 ? 50 : 100;
		}

		private void LogPoolSize()
		{
			Log.Debug("MessagePool", $"Текущий размер пула сообщений: {_messagePool.Count}");
		}

		public Message GetMessage()
		{
			if (_messagePool.Count > 0)
			{
				var message = _messagePool.Pop();
				LogPoolSize();
				return message;
			}
			else
			{
				var newMessage = new Message();
				LogPoolSize();
				return newMessage;
			}
		}

		public void ReturnMessage(Message message)
		{
			message.Reset();

			if (_messagePool.Count < _maxPoolSize)
			{
				_messagePool.Push(message);
				LogPoolSize();
			}
			else
			{
				LogPoolSize();
			}
		}

		public void Clear()
		{
			_messagePool.Clear();
			LogPoolSize();
		}

		public int CurrentPoolSize()
		{
			return _messagePool.Count;
		}
	}
}
