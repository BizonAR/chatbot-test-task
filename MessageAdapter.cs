using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.Dialog;
using Google.Android.Material.Snackbar;
using System;
using System.Collections.Generic;
using System.Linq;
using Android.Views.Animations;
using Android.Util;

namespace ChatApp
{
	public class SelectionChangedEventArgs : EventArgs
	{
		public bool HasSelection { get; set; }
	}

	public class MessageAdapter : RecyclerView.Adapter
	{
		private const int VIEW_TYPE_USER = 1;
		private const int VIEW_TYPE_ROBOT = 2;

		private List<Message> _messages;
		private List<Message> _selectedMessages;
		private bool _isInSelectionMode;
		private readonly RecyclerView _recyclerView;
		private Message _messageBeingEdited;

		public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

		public MessageAdapter(List<Message> messages, RecyclerView recyclerView)
		{
			_messages = messages ?? new List<Message>();
			_selectedMessages = new List<Message>();
			_recyclerView = recyclerView ?? throw new ArgumentNullException(nameof(recyclerView));
		}

		public bool GetIsInSelectionMode() => _isInSelectionMode;

		public void SetIsInSelectionMode(bool isInSelectionMode)
		{
			_isInSelectionMode = isInSelectionMode;
			NotifyDataSetChanged();
		}

		public List<Message> GetSelectedMessages() => new List<Message>(_selectedMessages);

		public void SetSelectedMessages(List<Message> selectedMessages)
		{
			_selectedMessages = selectedMessages != null ? new List<Message>(selectedMessages) : new List<Message>();
			NotifyDataSetChanged();
		}

		public int[] GetSelectedMessageIds() => _selectedMessages.Select(m => m.Id).ToArray();

		private void OnItemClick(int position)
		{
			if (_isInSelectionMode)
			{
				var message = _messages[position];
				if (_selectedMessages.Contains(message))
				{
					_selectedMessages.Remove(message);
				}
				else
				{
					_selectedMessages.Add(message);
				}
				NotifyItemChanged(position);
				SelectionChanged?.Invoke(this, new SelectionChangedEventArgs { HasSelection = _selectedMessages.Count > 0 });
			}
		}

		private void OnItemLongClick(int position)
		{
			if (!_isInSelectionMode)
			{
				_isInSelectionMode = true;
				_selectedMessages.Add(_messages[position]);
				NotifyItemChanged(position);
				SelectionChanged?.Invoke(this, new SelectionChangedEventArgs { HasSelection = true });
			}
		}

		public void EditMessage(Message message)
		{
			_messageBeingEdited = message;
		}

		public Message GetMessageBeingEdited() => _messageBeingEdited;

		public void ClearEditMessage() => _messageBeingEdited = null;

		public override int GetItemViewType(int position)
		{
			return _messages[position].Sender == "User" ? VIEW_TYPE_USER : VIEW_TYPE_ROBOT;
		}

		public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
		{
			var layout = viewType == VIEW_TYPE_USER ? Resource.Layout.message_user_item : Resource.Layout.message_robot_item;
			var itemView = LayoutInflater.From(parent.Context).Inflate(layout, parent, false);
			return new MessageViewHolder(itemView, OnItemClick, OnItemLongClick);
		}

		public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
		{
			var message = _messages[position];
			var viewHolder = holder as MessageViewHolder;

			viewHolder.MessageText.Text = message?.Text ?? string.Empty;
			viewHolder.MessageSender.Text = message?.Sender ?? string.Empty;
			viewHolder.MessageDate.Text = message?.Date.ToString("HH:mm") ?? string.Empty;
			viewHolder.CheckBox.Visibility = _isInSelectionMode ? ViewStates.Visible : ViewStates.Gone;
			viewHolder.CheckBox.Checked = _selectedMessages.Contains(message);

			if (position >= _messages.Count - 1)
			{
				var animation = AnimationUtils.LoadAnimation(viewHolder.ItemView.Context, Resource.Animation.fade_in);
				viewHolder.ItemView.StartAnimation(animation);
			}
		}

		public override int ItemCount => _messages?.Count ?? 0;

		public void DeleteSelectedMessages()
		{
			var context = _recyclerView.Context;
			if (context == null)
			{
				Log.Error("MessageAdapter", "RecyclerView context is null");
				Snackbar.Make(_recyclerView, Resource.String.message_send_error, Snackbar.LengthShort).Show();
				return;
			}

			var dialog = new MaterialAlertDialogBuilder(context)
				.SetTitle(Resource.String.delete_messages_title)
				.SetMessage($"Are you sure you want to delete {_selectedMessages.Count} messages?")
				.SetPositiveButton(Resource.String.delete_button, async (s, e) =>
				{
					try
					{
						foreach (var message in _selectedMessages.ToList())
						{
							await ChatStorage.Instance.DeleteMessageAsync(message);
							_messages.Remove(message);
						}

						_selectedMessages.Clear();
						_isInSelectionMode = false;

						UpdateMessages(new List<Message>(_messages));

						SelectionChanged?.Invoke(this, new SelectionChangedEventArgs { HasSelection = false });
					}
					catch (Exception ex)
					{
						Log.Error("MessageAdapter", $"Error deleting messages: {ex.Message}");
						Snackbar.Make(_recyclerView, Resource.String.message_send_error, Snackbar.LengthShort).Show();
					}
				})
				.SetNegativeButton(Resource.String.cancel_button, (s, e) => { })
				.Create();

			dialog.Show();
		}

		public void CancelSelection()
		{
			_selectedMessages.Clear();
			_isInSelectionMode = false;
			NotifyDataSetChanged();
			SelectionChanged?.Invoke(this, new SelectionChangedEventArgs { HasSelection = false });
		}

		public void UpdateMessages(List<Message> newMessages)
		{
			if (newMessages == null)
			{
				newMessages = new List<Message>();
			}

			var diffCallback = new MessageDiffCallback(_messages, newMessages);
			var diffResult = DiffUtil.CalculateDiff(diffCallback);

			_messages.Clear();
			_messages.AddRange(newMessages);

			diffResult.DispatchUpdatesTo(this);
		}

		public class MessageViewHolder : RecyclerView.ViewHolder
		{
			public TextView MessageText { get; set; }
			public TextView MessageSender { get; set; }
			public TextView MessageDate { get; set; }
			public CheckBox CheckBox { get; set; }

			public MessageViewHolder(View itemView, Action<int> onClick, Action<int> onLongClick) : base(itemView)
			{
				MessageText = itemView.FindViewById<TextView>(Resource.Id.messageText);
				MessageSender = itemView.FindViewById<TextView>(Resource.Id.messageSender);
				MessageDate = itemView.FindViewById<TextView>(Resource.Id.messageDate);
				CheckBox = itemView.FindViewById<CheckBox>(Resource.Id.messageCheckBox);

				itemView.Click += (s, e) => onClick?.Invoke(BindingAdapterPosition);
				itemView.LongClick += (s, e) => onLongClick?.Invoke(BindingAdapterPosition);
			}
		}

		private class MessageDiffCallback : DiffUtil.Callback
		{
			private readonly List<Message> _oldMessages;
			private readonly List<Message> _newMessages;

			public MessageDiffCallback(List<Message> oldMessages, List<Message> newMessages)
			{
				_oldMessages = oldMessages ?? new List<Message>();
				_newMessages = newMessages ?? new List<Message>();
			}

			public override int OldListSize => _oldMessages.Count;

			public override int NewListSize => _newMessages.Count;

			public override bool AreItemsTheSame(int oldItemPosition, int newItemPosition)
			{
				return _oldMessages[oldItemPosition].Id == _newMessages[newItemPosition].Id;
			}

			public override bool AreContentsTheSame(int oldItemPosition, int newItemPosition)
			{
				var oldMessage = _oldMessages[oldItemPosition];
				var newMessage = _newMessages[newItemPosition];
				return oldMessage.Text == newMessage.Text &&
					   oldMessage.Sender == newMessage.Sender &&
					   oldMessage.Date == newMessage.Date;
			}
		}
	}
}