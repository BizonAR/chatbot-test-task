using System;
using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;

namespace ChatApp
{
	public class ChatAdapter : RecyclerView.Adapter
	{
		#region Поля

		private List<Chat> _chats;
		private readonly Action<Chat> _onEditChat;
		private readonly Action<int> _onChatSelected;
		private readonly ChatStorage _chatStorage = ChatStorage.Instance ?? throw new InvalidOperationException("ChatStorage.Instance is not initialized");
		private readonly ChatPool _chatPool = new ChatPool();

		#endregion

		#region Конструкторы

		public ChatAdapter(List<Chat> chats, Action<Chat> onEditChat, Action<int> onChatSelected)
		{
			_chats = chats?.Select(c => _chatPool.GetChat().CopyFrom(c)).ToList() ?? new List<Chat>();
			_onEditChat = onEditChat;
			_onChatSelected = onChatSelected;
		}

		#endregion

		#region Свойства

		public override int ItemCount => _chats?.Count ?? 0;

		#endregion

		#region Методы

		public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
		{
			try
			{
				var view = LayoutInflater.From(parent.Context)
					.Inflate(Resource.Layout.chat_item, parent, false);
				Log.Debug("ChatAdapter", "ViewHolder created successfully");
				return new ChatViewHolder(view, this);
			}
			catch (Exception ex)
			{
				Log.Error("ChatAdapter", $"Error creating ViewHolder: {ex.Message}");
				throw;
			}
		}

		public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
		{
			var chat = _chats[position];
			var viewHolder = holder as ChatViewHolder;

			viewHolder.ChatName.Text = chat?.Name ?? string.Empty;
			viewHolder.LastMessage.Text = chat?.LastMessage ?? string.Empty;
			viewHolder.LastMessageDate.Text = chat?.LastMessageDate.ToString("g") ?? string.Empty;
			viewHolder.LastSender.Text = chat?.LastSender ?? string.Empty;

			if (position >= _chats.Count - 1)
			{
				viewHolder.ItemView.Alpha = 0f;
				viewHolder.ItemView.Animate()
					.Alpha(1f)
					.SetDuration(500)
					.Start();
			}
		}

		public void UpdateChats(List<Chat> newChats)
		{
			foreach (var chat in _chats)
			{
				_chatPool.ReturnChat(chat);
			}

			var diffCallback = new ChatDiffCallback(_chats, newChats);
			var diffResult = DiffUtil.CalculateDiff(diffCallback);
			_chats = newChats?.Select(c => _chatPool.GetChat().CopyFrom(c)).ToList() ?? new List<Chat>();
			diffResult.DispatchUpdatesTo(this);
		}

		#endregion

		#region Вложенные классы

		public class ChatViewHolder : RecyclerView.ViewHolder
		{
			#region Свойства

			public ImageView ChatIcon { get; }
			public TextView ChatName { get; set; }
			public TextView LastMessage { get; set; }
			public TextView LastMessageDate { get; set; }
			public TextView LastSender { get; set; }

			#endregion

			#region Поля

			private readonly ChatAdapter _adapter;

			#endregion

			#region Конструкторы

			public ChatViewHolder(View itemView, ChatAdapter adapter) : base(itemView)
			{
				_adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
				ChatIcon = itemView.FindViewById<ImageView>(Resource.Id.chatIcon);
				ChatName = itemView.FindViewById<TextView>(Resource.Id.chatName);
				LastMessage = itemView.FindViewById<TextView>(Resource.Id.lastMessage);
				LastMessageDate = itemView.FindViewById<TextView>(Resource.Id.lastMessageDate);
				LastSender = itemView.FindViewById<TextView>(Resource.Id.lastSender);

				itemView.Click += (s, e) =>
				{
					var position = BindingAdapterPosition;
					if (position != RecyclerView.NoPosition)
					{
						_adapter._onChatSelected?.Invoke(_adapter._chats[position].Id);
					}
				};

				itemView.LongClick += (s, e) =>
				{
					var position = BindingAdapterPosition;
					if (position != RecyclerView.NoPosition)
					{
						_adapter._onEditChat?.Invoke(_adapter._chats[position]);
					}
				};
			}

			#endregion
		}

		public class ChatDiffCallback : DiffUtil.Callback
		{
			#region Поля

			private readonly List<Chat> _oldChats;
			private readonly List<Chat> _newChats;

			#endregion

			#region Конструкторы

			public ChatDiffCallback(List<Chat> oldChats, List<Chat> newChats)
			{
				_oldChats = oldChats ?? new List<Chat>();
				_newChats = newChats ?? new List<Chat>();
			}

			#endregion

			#region Переопределённые методы

			public override int OldListSize => _oldChats?.Count ?? 0;

			public override int NewListSize => _newChats?.Count ?? 0;

			public override bool AreItemsTheSame(int oldItemPosition, int newItemPosition)
			{
				return _oldChats[oldItemPosition]?.Id == _newChats[newItemPosition]?.Id;
			}

			public override bool AreContentsTheSame(int oldItemPosition, int newItemPosition)
			{
				var oldChat = _oldChats[oldItemPosition];
				var newChat = _newChats[newItemPosition];

				return oldChat?.Name == newChat?.Name &&
					   oldChat?.LastMessage == newChat?.LastMessage &&
					   oldChat?.LastMessageDate == newChat?.LastMessageDate &&
					   oldChat?.LastSender == newChat?.LastSender;
			}

			#endregion
		}

		#endregion
	}
}
