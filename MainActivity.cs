using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.RecyclerView.Widget;
using AndroidX.SwipeRefreshLayout.Widget;
using Google.Android.Material.Button;
using Google.Android.Material.Dialog;
using Google.Android.Material.Snackbar;
using Google.Android.Material.TextField;

namespace ChatApp
{
    [Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/AppTheme")]
    public class MainActivity : AppCompatActivity
    {
        #region Константы

        private const string CHATS_KEY = "chats";
        private const string SEARCH_QUERY_KEY = "search_query";
        private const int MAX_CHAT_NAME_LENGTH = 50;

        #endregion

        #region Поля

        private SwipeRefreshLayout _swipeRefreshLayout;
        private RecyclerView _chatRecyclerView;
        private ChatAdapter _adapter;
        private List<Chat> _chats;
        private ProgressBar _progressBar;
        private TextView _emptyPlaceholder;
        private MaterialButton _addChatButton;
        private ChatViewModel _viewModel;
        private TextInputEditText _searchInput;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isAddChatButtonEnabled = true;
        private bool _isChatsLoaded;

        #endregion

        #region Жизненный цикл

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ChatStorage.Init(this);
            SetContentView(Resource.Layout.activity_main);

            if (!CheckAndroidVersion()) return;
            await WarnIfDatabaseTooBigAsync();
            InitializeViews();

            if (!ValidateViews()) return;

            SetupRecyclerView(savedInstanceState);
            SetupEventHandlers();

            if (!_isChatsLoaded)
                LoadChatsAsync();
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (!_isChatsLoaded)
                LoadChatsAsync();
        }

        protected override void OnPause()
        {
            base.OnPause();
            _cts.Cancel();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _cts.Dispose();
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
			outState.PutParcelableArrayList(CHATS_KEY, new JavaList<Android.OS.IParcelable>(_chats.Cast<Android.OS
                .IParcelable>().ToList()));
			outState.PutString(SEARCH_QUERY_KEY, _searchInput.Text);
        }

        public override bool DispatchTouchEvent(MotionEvent ev)
        {
            if (ev.Action == MotionEventActions.Down && CurrentFocus is EditText editText)
            {
                var outRect = new Android.Graphics.Rect();
                editText.GetGlobalVisibleRect(outRect);

                if (!outRect.Contains((int)ev.RawX, (int)ev.RawY))
                {
                    editText.ClearFocus();
                    var imm = (InputMethodManager)GetSystemService(InputMethodService);
                    imm.HideSoftInputFromWindow(editText.WindowToken, HideSoftInputFlags.None);
                }
            }

            return base.DispatchTouchEvent(ev);
        }

        #endregion

        #region Инициализация

        private void InitializeViews()
        {
            _viewModel = new ChatViewModel();
            _chatRecyclerView = FindViewById<RecyclerView>(Resource.Id.chatRecyclerView);
            _progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);
            _emptyPlaceholder = FindViewById<TextView>(Resource.Id.emptyPlaceholder);
            _addChatButton = FindViewById<MaterialButton>(Resource.Id.addChatButton);
            _swipeRefreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            _searchInput = FindViewById<TextInputEditText>(Resource.Id.searchInput);
        }

        private bool ValidateViews()
        {
            if (_chatRecyclerView == null || _progressBar == null || _emptyPlaceholder == null ||
                _addChatButton == null || _swipeRefreshLayout == null || _searchInput == null)
            {
                Snackbar.Make(FindViewById(Android.Resource.Id.Content), GetString(Resource.String.error_loading_chats), Snackbar.LengthLong).Show();
                Finish();
                return false;
            }

            return true;
        }

        private void SetupRecyclerView(Bundle savedInstanceState)
        {
            _chats = savedInstanceState?.GetParcelableArrayList(CHATS_KEY, Java.Lang.Class.FromType(typeof(Chat)))?.Cast<Chat>().ToList()
                     ?? new List<Chat>();

            _adapter = new ChatAdapter(_chats, OnEditChat, OnChatSelected);
            _chatRecyclerView.SetLayoutManager(new LinearLayoutManager(this));
            _chatRecyclerView.SetAdapter(_adapter);

            if (savedInstanceState != null)
            {
                var query = savedInstanceState.GetString(SEARCH_QUERY_KEY);
                _searchInput.Text = query;
            }
        }

        private void SetupEventHandlers()
        {
            _swipeRefreshLayout.SetColorSchemeResources(Android.Resource.Color.HoloBlueBright);

            _swipeRefreshLayout.Refresh += async (sender, e) =>
            {
                await LoadChatsAsync();
                _swipeRefreshLayout.Refreshing = false;
            };

            _addChatButton.Click += async (sender, e) =>
            {
                if (!_isAddChatButtonEnabled) return;

                _isAddChatButtonEnabled = false;
                _addChatButton.StartAnimation(AnimationUtils.LoadAnimation(this, Resource.Animation.scale_button));
                ShowAddChatDialog(_cts.Token);
                await Task.Delay(500);
                _isAddChatButtonEnabled = true;
            };

            _searchInput.TextChanged += async (s, e) =>
            {
                var query = _searchInput.Text?.Trim();
                await FilterChatsAsync(query);
            };
        }

        #endregion

        #region Обработка событий

        private void OnChatSelected(int chatId)
        {
            if (chatId == 0)
            {
                Snackbar.Make(FindViewById(Android.Resource.Id.Content), "Ошибка создания чата", Snackbar.LengthShort).Show();
                return;
            }

            var intent = new Intent(this, typeof(ChatActivity));
            intent.PutExtra("chatId", chatId);
            StartActivity(intent);
        }

        #endregion

        #region Вспомогательные методы

        private bool CheckAndroidVersion()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M) return true;

            new MaterialAlertDialogBuilder(this)
                .SetTitle("Unsupported Android Version")
                .SetMessage("This application requires Android 6.0 (API 23) or higher to run securely.")
                .SetPositiveButton("OK", (s, e) => Finish())
                .SetCancelable(false)
                .Show();

            return false;
        }

        private async Task WarnIfDatabaseTooBigAsync()
        {
            try
            {
                if (!await ChatStorage.Instance.CheckDatabaseSizeAsync())
                {
                    new MaterialAlertDialogBuilder(this)
                        .SetTitle("Database Size Warning")
                        .SetMessage("The database size exceeds the recommended limit. Please delete some chats to free up space.")
                        .SetPositiveButton("OK", (s, e) => { })
                        .Show();
                }
            }
            catch (Exception ex)
            {
                Log.Error(nameof(MainActivity), $"Error checking database size: {ex.Message}");
            }
        }

        private async Task LoadChatsAsync()
        {
            try
            {
                _progressBar.Visibility = ViewStates.Visible;
                _chats = await _viewModel.GetChatsAsync() ?? new List<Chat>();
                _emptyPlaceholder.Visibility = _chats.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
                _adapter.UpdateChats(_chats);
                _isChatsLoaded = true;
            }
            catch (Exception ex)
            {
                Log.Error(nameof(MainActivity), $"Error loading chats: {ex.Message}");
                Snackbar.Make(FindViewById(Android.Resource.Id.Content), GetString(Resource.String.error_loading_chats), Snackbar.LengthLong).Show();
            }
            finally
            {
                _progressBar.Visibility = ViewStates.Gone;
            }
        }

        private async Task FilterChatsAsync(string query)
        {
            try
            {
                _progressBar.Visibility = ViewStates.Visible;
                var allChats = await _viewModel.GetChatsAsync();
                _chats = string.IsNullOrWhiteSpace(query)
                    ? allChats
                    : allChats.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                _emptyPlaceholder.Visibility = _chats.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
                _adapter.UpdateChats(_chats);
            }
            catch (Exception ex)
            {
                Log.Error(nameof(MainActivity), $"Error filtering chats: {ex.Message}");
                Snackbar.Make(FindViewById(Android.Resource.Id.Content), GetString(Resource.String.error_loading_chats), Snackbar.LengthLong).Show();
            }
            finally
            {
                _progressBar.Visibility = ViewStates.Gone;
            }
        }

        private async Task<bool> IsChatNameUniqueAsync(string chatName, int excludeChatId = 0)
        {
            return await ChatStorage.Instance.IsChatNameUniqueAsync(chatName, excludeChatId);
        }

		#endregion

		#region Диалоги (создание и редактирование)

		private void ShowAddChatDialog(CancellationToken cancellationToken = default)
		{
			var dialogView = LayoutInflater.From(this).Inflate(Resource.Layout.dialog_add_chat, null);
			if (dialogView == null)
			{
				Log.Error("MainActivity", "Failed to inflate dialog_add_chat layout");
				Snackbar.Make(FindViewById(Android.Resource.Id.Content), GetString(Resource.String.error_loading_chats), Snackbar.LengthShort).Show();
				return;
			}

			var dialog = new MaterialAlertDialogBuilder(this)
				.SetView(dialogView)
				.SetTitle(Resource.String.add_chat_title)
				.Create();

			var chatNameEditText = dialogView.FindViewById<EditText>(Resource.Id.chatNameEditText);
			var addButton = dialogView.FindViewById<MaterialButton>(Resource.Id.addChatButton);
			var cancelButton = dialogView.FindViewById<MaterialButton>(Resource.Id.cancelButton);

			if (chatNameEditText == null || addButton == null || cancelButton == null)
			{
				Log.Error("MainActivity", "Failed to find elements in dialog_add_chat layout");
				Snackbar.Make(FindViewById(Android.Resource.Id.Content), GetString(Resource.String.error_loading_chats), Snackbar.LengthShort).Show();
				return;
			}

			addButton.Click += async (sender, e) =>
			{
				string chatName = chatNameEditText.Text?.Trim();
				if (string.IsNullOrWhiteSpace(chatName))
				{
					Snackbar.Make(dialogView, GetString(Resource.String.invalid_chat_name), Snackbar.LengthShort).Show();
					return;
				}
				if (chatName.Length < 3)
				{
					Snackbar.Make(dialogView, GetString(Resource.String.chat_name_too_short), Snackbar.LengthShort).Show();
					return;
				}
				if (chatName.Length > MAX_CHAT_NAME_LENGTH)
				{
					Snackbar.Make(dialogView, GetString(Resource.String.chat_name_too_long, MAX_CHAT_NAME_LENGTH), Snackbar.LengthShort).Show();
					return;
				}
				if (!await IsChatNameUniqueAsync(chatName))
				{
					Snackbar.Make(dialogView, "Chat name already exists", Snackbar.LengthShort).Show();
					return;
				}

				try
				{
					var newChat = new Chat
					{
						Name = chatName,
						LastMessage = GetString(Resource.String.default_last_message),
						LastMessageDate = DateTime.Now,
						LastSender = "Robot"
					};

					await _viewModel.SaveChatAsync(newChat, cancellationToken);
					Log.Debug("MainActivity", $"New chat saved with Id = {newChat.Id}");

					if (newChat.Id == 0)
					{
						Log.Error("MainActivity", "Chat ID is 0 after saving. Aborting.");
						Snackbar.Make(FindViewById(Android.Resource.Id.Content), "Ошибка: не удалось создать чат", Snackbar.LengthShort).Show();
						return;
					}

					await LoadChatsAsync(); // обязательно обновляем список, иначе ChatActivity не найдёт чат
					dialog.Dismiss();

					// Переход к чату
					OnChatSelected(newChat.Id);
				}

				catch (System.Threading.Tasks.TaskCanceledException)
				{
					Log.Debug("MainActivity", "Add chat operation cancelled");
					Snackbar.Make(dialogView, GetString(Resource.String.operation_cancelled), Snackbar.LengthShort).Show();
				}
				catch (Exception ex)
				{
					Log.Error("MainActivity", $"Error creating chat: {ex.Message}");
					// FirebaseCrashlytics.GetInstance().RecordException(ex);
					Snackbar.Make(dialogView, GetString(Resource.String.error_loading_chats), Snackbar.LengthShort).Show();
				}
			};

			cancelButton.Click += (sender, e) => dialog.Dismiss();
			dialog.Show();
		}

		private void OnEditChat(Chat chat)
		{
			if (chat == null)
			{
				Log.Error("MainActivity", "Chat object is null in OnEditChatAsync");
				Snackbar.Make(FindViewById(Android.Resource.Id.Content), GetString(Resource.String.error_loading_chats), Snackbar.LengthShort).Show();
				return;
			}

			var dialogView = LayoutInflater.From(this).Inflate(Resource.Layout.dialog_add_chat, null);
			if (dialogView == null)
			{
				Log.Error("MainActivity", "Failed to inflate dialog_add_chat layout");
				Snackbar.Make(FindViewById(Android.Resource.Id.Content), GetString(Resource.String.error_loading_chats), Snackbar.LengthShort).Show();
				return;
			}

			var dialog = new MaterialAlertDialogBuilder(this)
				.SetView(dialogView)
				.SetTitle(Resource.String.edit_chat_title)
				.Create();

			var chatNameEditText = dialogView.FindViewById<EditText>(Resource.Id.chatNameEditText);
			var addButton = dialogView.FindViewById<MaterialButton>(Resource.Id.addChatButton);
			var cancelButton = dialogView.FindViewById<MaterialButton>(Resource.Id.cancelButton);

			if (chatNameEditText == null || addButton == null || cancelButton == null)
			{
				Log.Error("MainActivity", "Failed to find elements in dialog_add_chat layout");
				Snackbar.Make(FindViewById(Android.Resource.Id.Content), GetString(Resource.String.error_loading_chats), Snackbar.LengthShort).Show();
				return;
			}

			chatNameEditText.Text = chat.Name;

			addButton.Click += async (sender, e) =>
			{
				string newName = chatNameEditText.Text?.Trim();
				if (string.IsNullOrWhiteSpace(newName))
				{
					Snackbar.Make(dialogView, GetString(Resource.String.invalid_chat_name), Snackbar.LengthShort).Show();
					return;
				}
				if (newName.Length < 3)
				{
					Snackbar.Make(dialogView, GetString(Resource.String.chat_name_too_short), Snackbar.LengthShort).Show();
					return;
				}
				if (newName.Length > MAX_CHAT_NAME_LENGTH)
				{
					//Snackbar.Make(dialogView, GetString(Resource.String.chat_name_too_long, MAX_CHAT_NAME_LENGTH), Snackbar.LengthShort).Show();
					return;
				}
				if (!await IsChatNameUniqueAsync(newName, chat.Id))
				{
					Snackbar.Make(dialogView, "Chat name already exists", Snackbar.LengthShort).Show();
					return;
				}

				try
				{
					chat.Name = newName;
					await _viewModel.SaveChatAsync(chat, _cts.Token);
					Log.Debug("MainActivity", $"Chat updated: {chat.Name}");

					await LoadChatsAsync();
					dialog.Dismiss();
				}
				catch (System.Threading.Tasks.TaskCanceledException)
				{
					Log.Debug("MainActivity", "Edit chat operation cancelled");
					Snackbar.Make(dialogView, GetString(Resource.String.operation_cancelled), Snackbar.LengthShort).Show();
				}
				catch (Exception ex)
				{
					Log.Error("MainActivity", $"Error updating chat: {ex.Message}");
					// FirebaseCrashlytics.GetInstance().RecordException(ex);
					Snackbar.Make(dialogView, GetString(Resource.String.error_loading_chats), Snackbar.LengthShort).Show();
				}
			};

			cancelButton.Click += (sender, e) => dialog.Dismiss();
			dialog.Show();
		}

		#endregion
	}
}
