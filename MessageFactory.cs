using Android.Util;

namespace ChatApp
{
	public static class MessageFactory
	{
		private static readonly string[] RobotResponses =
		{
			"Привет! Как я могу помочь?",
			"Какой вопрос, такой ответ!",
			"Что ты хочешь узнать?"
		};

		private static readonly MessagePool _messagePool = new MessagePool();
		private const int MAX_MESSAGE_LENGTH = 500;

		public static Message CreateUserMessage(int chatId, string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new ArgumentException("Message text cannot be null or empty", nameof(text));
			}
			if (text.Length > MAX_MESSAGE_LENGTH)
			{
				throw new ArgumentException($"Message cannot exceed {MAX_MESSAGE_LENGTH} characters", nameof(text));
			}

			var message = _messagePool.GetMessage();
			message.ChatId = chatId;
			message.Text = text;
			message.Date = DateTime.Now;
			message.Sender = "User";
			Log.Debug("MessageFactory", $"Created user message: {text}");
			return message;
		}

		public static Message CreateRobotMessage(int chatId)
		{
			var message = _messagePool.GetMessage();
			Random random = new Random();
			string response = RobotResponses[random.Next(RobotResponses.Length)];
			message.ChatId = chatId;
			message.Text = response;
			message.Date = DateTime.Now;
			message.Sender = "Robot";
			Log.Debug("MessageFactory", $"Created robot message: {response}");
			return message;
		}

		public static void ClearMessagePool()
		{
			Log.Debug("MessageFactory", $"Clearing message pool. Current size: {_messagePool.CurrentPoolSize()}");
			_messagePool.Clear();
			Log.Debug("MessageFactory", "Message pool cleared");
		}
	}
}