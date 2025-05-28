using Android.Views;
using Google.Android.Material.TextField;

namespace ChatApp
{
	public class MessageActionModeCallback : Java.Lang.Object, ActionMode.ICallback
	{
		#region Поля

		private readonly ChatActivity _activity;
		private readonly MessageAdapter _adapter;

		#endregion

		#region Конструктор

		public MessageActionModeCallback(ChatActivity activity, MessageAdapter adapter)
		{
			_activity = activity;
			_adapter = adapter;
		}

		#endregion

		#region Методы интерфейса ICallback

		public bool OnCreateActionMode(ActionMode actionMode, IMenu menu)
		{
			actionMode.MenuInflater.Inflate(Resource.Menu.message_context_menu, menu);
			return true;
		}

		public bool OnPrepareActionMode(ActionMode actionMode, IMenu menu)
		{
			var selectedMessages = _adapter.GetSelectedMessages();
			bool allFromUser = selectedMessages.All(m => m.Sender == "User");
			bool isSingleUserMessage = selectedMessages.Count == 1 && selectedMessages[0].Sender == "User";
			bool isSingleMessage = selectedMessages.Count == 1;

			menu.FindItem(Resource.Id.action_edit).SetVisible(isSingleUserMessage);
			menu.FindItem(Resource.Id.action_delete).SetVisible(allFromUser && selectedMessages.Count > 0);
			menu.FindItem(Resource.Id.action_reply).SetVisible(isSingleMessage);
			menu.FindItem(Resource.Id.action_forward).SetVisible(selectedMessages.Count > 0);

			return true;
		}

		public bool OnActionItemClicked(ActionMode actionMode, IMenuItem item)
		{
			var selectedMessages = _adapter.GetSelectedMessages();
			if (selectedMessages.Count == 0)
			{
				actionMode.Finish();
				return false;
			}

			switch (item.ItemId)
			{
				case Resource.Id.action_edit:
					if (selectedMessages.Count == 1)
					{
						var message = selectedMessages[0];
						_adapter.EditMessage(message);

						var inputField = _activity.FindViewById<TextInputEditText>(Resource.Id.messageInput);
						if (inputField != null)
						{
							inputField.Text = message.Text;
							inputField.SetSelection(message.Text?.Length ?? 0);
						}
					}
					break;

				case Resource.Id.action_delete:
					_adapter.DeleteSelectedMessages();
					break;

				case Resource.Id.action_reply:
					_activity.StartReplyToMessage(selectedMessages[0]);
					break;

				case Resource.Id.action_forward:
					_activity.ForwardMessages(selectedMessages);
					break;
			}

			actionMode.Finish();
			return true;
		}

		public void OnDestroyActionMode(ActionMode mode)
		{
			_adapter.CancelSelection();
		}

		#endregion
	}
}
