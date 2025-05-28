using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Util;
using SQLite;

namespace ChatApp
{
	public class ChatStorage : IDisposable
	{
		#region Статические поля и свойства

		private static SQLiteConnection _database;
		private static ChatStorage _instance;
		private static Context _appContext;

		public static ChatStorage Instance { get; private set; }

		#endregion

		#region Поля

		private readonly string _dbPath;
		private readonly MessagePool _messagePool;
		private readonly ChatPool _chatPool;
		private bool _disposed;

		#endregion

		#region Конструкторы

		private ChatStorage(Context context)
		{
			_appContext = context.ApplicationContext;

			var dbName = "chats.db3";
			_dbPath = Path.Combine(context.FilesDir.Path, dbName);

			if (!File.Exists(_dbPath))
			{
				Log.Debug("ChatStorage", "Database file not found. Will be created.");
			}

			_database = new SQLiteConnection(
				_dbPath,
				SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex
			);

			_database.CreateTable<Chat>();
			_database.CreateTable<Message>();
			_database.Execute("CREATE INDEX IF NOT EXISTS idx_message_chatid ON Message(ChatId)");

			_messagePool = new MessagePool();
			_chatPool = new ChatPool();
		}

		#endregion

		#region Статические методы

		public static void Init(Context context)
		{
			if (Instance == null)
			{
				Instance = new ChatStorage(context);
			}
		}

		#endregion

		#region Интерфейс IDisposable

		public void Dispose()
		{
			if (_disposed) return;

			try
			{
				_database?.Close();
				_database?.Dispose();
				_messagePool.Clear();
				_chatPool.Clear();
				MessageFactory.ClearMessagePool();
				Log.Debug("ChatStorage", "Database connection closed and pools cleared");
			}
			catch (Exception ex)
			{
				Log.Error("ChatStorage", $"Error disposing ChatStorage: {ex.Message}");
			}

			_disposed = true;
		}

		#endregion

		#region Публичные методы

		public async Task<bool> CheckDatabaseSizeAsync(CancellationToken cancellationToken = default)
		{
			const long MAX_DATABASE_SIZE_BYTES = 50 * 1024 * 1024;

			try
			{
				return await Task.Run(() =>
				{
					var fileInfo = new FileInfo(_dbPath);
					return !(fileInfo.Exists && fileInfo.Length > MAX_DATABASE_SIZE_BYTES);
				}, cancellationToken);
			}
			catch (Exception ex)
			{
				Log.Error("ChatStorage", $"Error checking database size: {ex.Message}");
				throw new ApplicationException("Failed to check database size", ex);
			}
		}

		public async Task<bool> IsChatNameUniqueAsync(string chatName, int excludeChatId = 0, CancellationToken cancellationToken = default)
		{
			try
			{
				var count = await Task.Run(() =>
					_database.ExecuteScalar<int>("SELECT COUNT(*) FROM Chat WHERE LOWER(Name) = LOWER(?) AND Id != ?", chatName, excludeChatId),
					cancellationToken);

				return count == 0;
			}
			catch (Exception ex)
			{
				Log.Error("ChatStorage", $"Error checking chat name uniqueness: {ex.Message}");
				throw new ApplicationException("Failed to check chat name uniqueness", ex);
			}
		}

		public async Task<List<Chat>> GetChatsAsync(CancellationToken cancellationToken = default)
		{
			try
			{
				var chats = await Task.Run(() => _database.Table<Chat>().ToList(), cancellationToken);
				var result = chats.Select(c => _chatPool.GetChat().CopyFrom(c)).ToList();

				foreach (var chat in result)
				{
					var messages = await Task.Run(() => _database.Table<Message>().Where(m => m.ChatId == chat.Id).ToList(), cancellationToken);
					chat.Messages = messages.Select(m => _messagePool.GetMessage().CopyFrom(m)).ToList();
				}

				return result;
			}
			catch (Exception ex)
			{
				Log.Error("ChatStorage", $"Error retrieving chats: {ex.Message}");
				return new List<Chat>();
			}
		}

		public async Task SaveChatAsync(Chat chat, CancellationToken cancellationToken = default)
		{
			int retries = 3;

			while (retries > 0)
			{
				try
				{
					var pooledChat = _chatPool.GetChat().CopyFrom(chat);

					if (chat.Id == 0)
					{
						pooledChat.Id = 0;
						Log.Debug("ChatStorage", $"Inserting new chat: {pooledChat.Name}");
						_database.Insert(pooledChat);
						chat.Id = pooledChat.Id;
						Log.Debug("ChatStorage", $"Assigned ID to chat: {chat.Id}");
					}
					else
					{
						Log.Debug("ChatStorage", $"Updating existing chat: Id={pooledChat.Id}");
						_database.Update(pooledChat);
					}

					_chatPool.ReturnChat(pooledChat);
					return;
				}
				catch (Exception ex)
				{
					Log.Error("ChatStorage", $"Error saving chat: {ex.Message}. Retries left: {retries}");
					retries--;
					if (retries == 0)
					{
						throw new ApplicationException("Failed to save chat after retries", ex);
					}

					await Task.Delay(1000, cancellationToken);
				}
			}
		}

		public async Task SaveMessageAsync(Message message, CancellationToken cancellationToken = default)
		{
			try
			{
				var pooledMessage = _messagePool.GetMessage().CopyFrom(message);

				if (message.Id == 0)
				{
					await Task.Run(() => _database.Insert(pooledMessage), cancellationToken);
					message.Id = pooledMessage.Id;
				}
				else
				{
					await Task.Run(() => _database.Update(pooledMessage), cancellationToken);
				}

				_messagePool.ReturnMessage(pooledMessage);
			}
			catch (Exception ex)
			{
				Log.Error("ChatStorage", $"Error saving message: {ex.Message}");
				throw new ApplicationException("Failed to save message", ex);
			}
		}

		public async Task<List<Message>> GetMessagesAsync(int chatId, CancellationToken cancellationToken = default)
		{
			try
			{
				var messages = await Task.Run(() => _database.Table<Message>().Where(m => m.ChatId == chatId).ToList(), cancellationToken);

				foreach (var msg in messages)
				{
					Log.Debug("ChatStorage", $"Fetched message: Id={msg.Id}, Text={msg.Text}");
				}

				return messages.Select(m => _messagePool.GetMessage().CopyFrom(m)).Where(m => m != null).ToList();
			}
			catch (Exception ex)
			{
				Log.Error("ChatStorage", $"Error retrieving messages: {ex.Message}");
				throw new ApplicationException("Failed to retrieve messages", ex);
			}
		}

		public async Task DeleteMessageAsync(Message message, CancellationToken cancellationToken = default)
		{
			try
			{
				await Task.Run(() => _database.Delete(message), cancellationToken);
				_messagePool.ReturnMessage(message);
			}
			catch (Exception ex)
			{
				Log.Error("ChatStorage", $"Error deleting message: {ex.Message}");
				throw new ApplicationException("Failed to delete message", ex);
			}
		}

		public async Task DeleteChatAsync(Chat chat, CancellationToken cancellationToken = default)
		{
			try
			{
				await Task.Run(() =>
				{
					_database.Execute("DELETE FROM Message WHERE ChatId = ?", chat.Id);
					_database.Delete(chat);
				}, cancellationToken);

				_chatPool.ReturnChat(chat);
			}
			catch (Exception ex)
			{
				Log.Error("ChatStorage", $"Error deleting chat: {ex.Message}");
				throw new ApplicationException("Failed to delete chat", ex);
			}
		}

		#endregion
	}
}
