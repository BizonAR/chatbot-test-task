using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.View;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.Button;
using Google.Android.Material.Snackbar;
using Google.Android.Material.TextField;

namespace ChatApp
{
	[Activity(Label = "@string/chat_activity_title", Theme = "@style/AppTheme")]
	public class ChatActivity : AppCompatActivity
	{
		#region Поля

		private const int MAX_MESSAGE_LENGTH = 500;
		private const int MIN_MESSAGE_LENGTH = 1;

		private RecyclerView? _messagesRecyclerView;
		private MessageAdapter? _messageAdapter;
		private ProgressBar? _robotTypingProgressBar;
		private TextInputEditText? _messageInput;
		private MaterialButton? _sendButton;
		private List<Message>? _messages;
		private Chat? _currentChat;
		private ChatViewModel? _viewModel;
		private AndroidX.AppCompat.View.ActionMode? _actionMode;
		private Message? _replyToMessage;
		private Message? _messageBeingEdited;

		private bool _isSendButtonEnabled = true;
		private readonly CancellationTokenSource _cts = new CancellationTokenSource();

		#endregion

		#region Переопределённые методы

		protected override async void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.chat_activity);

			int chatId = Intent.GetIntExtra("chatId", -1);
			if (chatId == -1)
			{
				Finish();
				return;
			}

			_viewModel = new ChatViewModel();
			var chats = await _viewModel.GetChatsAsync();
			_currentChat = chats.FirstOrDefault(c => c.Id == chatId);
			if (_currentChat == null)
			{
				Finish();
				return;
			}

			_messagesRecyclerView = FindViewById<RecyclerView>(Resource.Id.messagesRecyclerView);
			_robotTypingProgressBar = FindViewById<ProgressBar>(Resource.Id.robotTypingProgressBar);
			_messageInput = FindViewById<TextInputEditText>(Resource.Id.messageInput);
			_sendButton = FindViewById<MaterialButton>(Resource.Id.sendButton);

			_messages = new List<Message>();
			_messageAdapter = new MessageAdapter(_messages, _messagesRecyclerView);
			_messagesRecyclerView.SetLayoutManager(new LinearLayoutManager(this));
			_messagesRecyclerView.SetAdapter(_messageAdapter);

			await LoadMessagesAsync();

			_sendButton.Click += async (sender, e) =>
			{
				if (!_isSendButtonEnabled) return;
				_isSendButtonEnabled = false;
				_sendButton.StartAnimation(AnimationUtils.LoadAnimation(this, Resource.Animation.scale_button));
				await SendMessageAsync(_messageInput.Text);
				await Task.Delay(500);
				_isSendButtonEnabled = true;
			};

			_messageAdapter.SelectionChanged += OnSelectionChanged;
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			if (item.ItemId == Android.Resource.Id.Home)
			{
				Finish();
				return true;
			}
			return base.OnOptionsItemSelected(item);
		}

		#endregion

		#region Публичные методы

		public void EditMessage(Message message)
		{
			_messageBeingEdited = message;
		}

		public void StartReplyToMessage(Message message)
		{
			_replyToMessage = message;

			var replyPreview = FindViewById<LinearLayout>(Resource.Id.replyPreviewLayout);
			var replyText = FindViewById<TextView>(Resource.Id.replyTextView);
			var replySender = FindViewById<TextView>(Resource.Id.replySenderTextView);
			var replyCancel = FindViewById<ImageButton>(Resource.Id.replyCancelButton);

			if (replyPreview != null && replyText != null && replySender != null && replyCancel != null)
			{
				replyText.Text = message.Text;
				replySender.Text = message.Sender;
				replyPreview.Visibility = ViewStates.Visible;

				replyCancel.Click += (s, e) =>
				{
					replyPreview.Visibility = ViewStates.Gone;
					_replyToMessage = null;
				};
			}
		}

		public void ForwardMessages(List<Message> messagesToForward)
		{
			if (messagesToForward == null || messagesToForward.Count == 0)
				return;

			string combinedText = string.Join("\n", messagesToForward.Select(m => $"{m.Sender}: {m.Text}"));

			var messageInput = FindViewById<TextInputEditText>(Resource.Id.messageInput);
			if (messageInput != null)
			{
				messageInput.Text = combinedText;
				messageInput.SetSelection(messageInput.Text?.Length ?? 0);
			}
		}

		#endregion

		#region Приватные методы

		private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.HasSelection && _actionMode == null)
			{
				_actionMode = StartSupportActionMode(new ActionModeCallback(this));
			}
			else if (!e.HasSelection && _actionMode != null)
			{
				_actionMode.Finish();
			}
			else
			{
				_actionMode?.Invalidate();
			}
		}

		private async Task LoadMessagesAsync()
		{
			if (_currentChat == null) return;

			try
			{
				_robotTypingProgressBar.Visibility = ViewStates.Visible;
				(_robotTypingProgressBar.IndeterminateDrawable as AnimationDrawable)?.Start();

				var messagesFromDb = await _viewModel.GetMessagesAsync(_currentChat.Id, _cts.Token);
				_messages.Clear();
				_messages.AddRange(messagesFromDb);
				_messageAdapter.UpdateMessages(new List<Message>(_messages));

				_messagesRecyclerView.ViewTreeObserver.AddOnGlobalLayoutListener(new GlobalLayoutListener(() =>
				{
					ScrollToLastMessage();
				}));
			}
			catch (Exception ex)
			{
				Log.Error("ChatActivity", $"Error loading messages: {ex.Message}");
				Snackbar.Make(FindViewById(Android.Resource.Id.Content), GetString(Resource.String.error_loading_messages), Snackbar.LengthLong).Show();
			}
			finally
			{
				(_robotTypingProgressBar.IndeterminateDrawable as AnimationDrawable)?.Stop();
				_robotTypingProgressBar.Visibility = ViewStates.Gone;
			}
		}

		private async Task SendMessageAsync(string messageText)
		{
			messageText = messageText?.Trim();
			if (string.IsNullOrEmpty(messageText) || messageText.Length < MIN_MESSAGE_LENGTH) return;
			if (messageText.Length > MAX_MESSAGE_LENGTH) return;

			try
			{
				var editingMessage = _messageAdapter.GetMessageBeingEdited();
				if (editingMessage != null)
				{
					editingMessage.Text = messageText;
					editingMessage.Date = DateTime.Now;

					await _viewModel.SaveMessageAsync(editingMessage, _cts.Token);

					int index = _messages.FindIndex(m => m.Id == editingMessage.Id);
					if (index >= 0)
					{
						_messages[index] = editingMessage;
						_messageAdapter.NotifyItemChanged(index);
					}

					_messageAdapter.ClearEditMessage();
				}
				else
				{
					var userMessage = MessageFactory.CreateUserMessage(_currentChat.Id, messageText);
					await _viewModel.SaveMessageAsync(userMessage, _cts.Token);

					_currentChat.LastMessage = userMessage.Text;
					_currentChat.LastMessageDate = userMessage.Date;
					_currentChat.LastSender = userMessage.Sender;
					await _viewModel.SaveChatAsync(_currentChat, _cts.Token);

					_messages.Add(userMessage);
					_messageAdapter.UpdateMessages(new List<Message>(_messages));
					ScrollToLastMessage();

					_robotTypingProgressBar.Visibility = ViewStates.Visible;
					(_robotTypingProgressBar.IndeterminateDrawable as AnimationDrawable)?.Start();
					await Task.Delay(2000, _cts.Token);

					var robotMessage = MessageFactory.CreateRobotMessage(_currentChat.Id);
					await _viewModel.SaveMessageAsync(robotMessage, _cts.Token);

					_currentChat.LastMessage = robotMessage.Text;
					_currentChat.LastMessageDate = robotMessage.Date;
					_currentChat.LastSender = robotMessage.Sender;
					await _viewModel.SaveChatAsync(_currentChat, _cts.Token);

					_messages.Add(robotMessage);
					_messageAdapter.UpdateMessages(new List<Message>(_messages));
					ScrollToLastMessage();
				}
			}
			catch (Exception ex)
			{
				Log.Error("ChatActivity", $"SendMessageAsync error: {ex.Message}");
			}
			finally
			{
				_robotTypingProgressBar.Visibility = ViewStates.Gone;
				(_robotTypingProgressBar.IndeterminateDrawable as AnimationDrawable)?.Stop();
				_messageInput.Text = string.Empty;
			}
		}

		private void ScrollToLastMessage()
		{
			if (_messagesRecyclerView == null || _messages == null || _messages.Count == 0)
				return;

			var layoutManager = (LinearLayoutManager)_messagesRecyclerView.GetLayoutManager();
			if (layoutManager == null) return;

			int lastVisibleItemPosition = layoutManager.FindLastCompletelyVisibleItemPosition();
			if (lastVisibleItemPosition < _messages.Count - 1)
			{
				_messagesRecyclerView.SmoothScrollToPosition(_messages.Count - 1);
			}
		}

		#endregion

		#region Вложенные классы

		private class ActionModeCallback : Java.Lang.Object, AndroidX.AppCompat.View.ActionMode.ICallback
		{
			private readonly ChatActivity _activity;

			public ActionModeCallback(ChatActivity activity)
			{
				_activity = activity;
			}

			public bool OnCreateActionMode(AndroidX.AppCompat.View.ActionMode mode, IMenu? menu)
			{
				mode.MenuInflater.Inflate(Resource.Menu.selection_menu, menu);
				return true;
			}

			public bool OnPrepareActionMode(AndroidX.AppCompat.View.ActionMode mode, IMenu? menu)
			{
				var selected = _activity._messageAdapter.GetSelectedMessages();
				menu.FindItem(Resource.Id.action_edit).SetVisible(selected.Count == 1 && selected[0].Sender == "User");
				menu.FindItem(Resource.Id.action_delete).SetVisible(selected.All(m => m.Sender == "User"));
				menu.FindItem(Resource.Id.action_reply).SetVisible(selected.Count == 1);
				menu.FindItem(Resource.Id.action_forward).SetVisible(selected.Count >= 1);
				return true;
			}

			public bool OnActionItemClicked(AndroidX.AppCompat.View.ActionMode mode, IMenuItem? item)
			{
				var selected = _activity._messageAdapter.GetSelectedMessages();

				switch (item.ItemId)
				{
					case Resource.Id.action_edit:
						if (selected.Count == 1)
						{
							var message = selected[0];
							_activity._messageInput.Text = message.Text;
							_activity._messageInput.SetSelection(_activity._messageInput.Text.Length);
							_activity._messageAdapter.EditMessage(message);
						}
						break;

					case Resource.Id.action_delete:
						_activity._messageAdapter.DeleteSelectedMessages();
						break;

					case Resource.Id.action_reply:
						if (selected.Count == 1)
						{
							_activity.StartReplyToMessage(selected[0]);
						}
						break;

					case Resource.Id.action_forward:
						_activity.ForwardMessages(selected);
						break;
				}

				mode.Finish();
				return true;
			}

			public void OnDestroyActionMode(AndroidX.AppCompat.View.ActionMode mode)
			{
				_activity._messageAdapter.CancelSelection();
				_activity._actionMode = null;
			}
		}

		#endregion
	}
}
