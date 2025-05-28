using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.Util;

namespace ChatApp
{
	public class ChatViewModel
	{
		#region Поля

		private readonly ChatStorage _chatStorage;

		#endregion

		#region Конструкторы

		public ChatViewModel()
		{
			_chatStorage = ChatStorage.Instance;
		}

		#endregion

		#region Публичные методы

		public async Task<List<Chat>> GetChatsAsync()
		{
			if (_chatStorage == null)
			{
				Log.Error(nameof(ChatViewModel), "ChatStorage is null");
				throw new InvalidOperationException("ChatStorage instance is null");
			}

			Log.Debug(nameof(ChatViewModel), "Calling ChatStorage.GetChatsAsync()...");
			var chats = await _chatStorage.GetChatsAsync();

			if (chats == null)
			{
				Log.Error(nameof(ChatViewModel), "GetChatsAsync returned null");
				throw new ApplicationException("Failed to retrieve chats: result is null");
			}

			Log.Debug(nameof(ChatViewModel), $"GetChatsAsync returned {chats.Count} chats");
			return chats;
		}

		public async Task SaveChatAsync(Chat chat, CancellationToken cancellationToken = default)
		{
			await _chatStorage.SaveChatAsync(chat, cancellationToken);
		}

		public async Task<List<Message>> GetMessagesAsync(int chatId, CancellationToken cancellationToken = default)
		{
			return await _chatStorage.GetMessagesAsync(chatId, cancellationToken);
		}

		public async Task SaveMessageAsync(Message message, CancellationToken cancellationToken = default)
		{
			await _chatStorage.SaveMessageAsync(message, cancellationToken);
		}

		public async Task DeleteChatAsync(Chat chat, CancellationToken cancellationToken = default)
		{
			await _chatStorage.DeleteChatAsync(chat, cancellationToken);
		}

		#endregion
	}
}
