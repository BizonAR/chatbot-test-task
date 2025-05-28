using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Android.OS;
using Android.Runtime;
using Java.Lang;
using SQLite;

[Serializable]
public class Chat : Java.Lang.Object, IParcelable, ISerializable
{
	#region Константы

	public static readonly IParcelableCreator Creator = new ParcelableCreator<Chat>();

	#endregion

	#region Поля

	private bool _disposed = false;

	#endregion

	#region Свойства

	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	public string Name { get; set; }

	public string LastMessage { get; set; }

	public DateTime LastMessageDate { get; set; }

	public string LastSender { get; set; }

	[Ignore]
	public List<Message> Messages { get; set; } = new List<Message>();

	#endregion

	#region Конструкторы

	public Chat() { }

	protected Chat(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

	public Chat(SerializationInfo info, StreamingContext context)
	{
		Id = info.GetInt32(nameof(Id));
		Name = info.GetString(nameof(Name)) ?? string.Empty;
		LastMessage = info.GetString(nameof(LastMessage)) ?? string.Empty;
		LastMessageDate = info.GetDateTime(nameof(LastMessageDate));
		LastSender = info.GetString(nameof(LastSender)) ?? string.Empty;
		Messages = info.GetValue(nameof(Messages), typeof(List<Message>)) as List<Message> ?? new List<Message>();
	}

	#endregion

	#region Финализаторы

	~Chat()
	{
		Dispose(false);
	}

	#endregion

	#region Методы

	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue(nameof(Id), Id);
		info.AddValue(nameof(Name), Name);
		info.AddValue(nameof(LastMessage), LastMessage);
		info.AddValue(nameof(LastMessageDate), LastMessageDate);
		info.AddValue(nameof(LastSender), LastSender);
		info.AddValue(nameof(Messages), Messages);
	}

	public void Reset()
	{
		Id = 0;
		Name = string.Empty;
		LastMessage = string.Empty;
		LastMessageDate = DateTime.MinValue;
		LastSender = string.Empty;
		Messages?.Clear();
	}

	public Chat CopyFrom(Chat source)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));

		Id = source.Id;
		Name = source.Name;
		LastMessage = source.LastMessage;
		LastMessageDate = source.LastMessageDate;
		LastSender = source.LastSender;
		Messages = source.Messages != null ? new List<Message>(source.Messages) : new List<Message>();

		return this;
	}

	public int DescribeContents() => 0;

	public void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
	{
		dest.WriteInt(Id);
		dest.WriteString(Name);
		dest.WriteString(LastMessage);
		dest.WriteLong(LastMessageDate.Ticks);
		dest.WriteString(LastSender);
		dest.WriteTypedList(Messages);
	}

	public new void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected new virtual void Dispose(bool disposing)
	{
		if (_disposed) return;

		if (disposing)
		{
			Name = string.Empty;
			LastMessage = string.Empty;
			LastSender = string.Empty;

			if (Messages != null)
			{
				foreach (var message in Messages)
				{
					message?.Dispose();
				}
				Messages.Clear();
				Messages = new List<Message>();
			}
		}

		_disposed = true;
		base.Dispose();
	}

	#endregion

	#region Вложенные классы

	public class ParcelableCreator<T> : Java.Lang.Object, IParcelableCreator where T : Chat, new()
	{
		public Java.Lang.Object CreateFromParcel(Parcel source)
		{
			var chat = new T
			{
				Id = source.ReadInt(),
				Name = source.ReadString() ?? string.Empty,
				LastMessage = source.ReadString() ?? string.Empty,
				LastMessageDate = new DateTime(source.ReadLong()),
				LastSender = source.ReadString() ?? string.Empty,
				Messages = new List<Message>()
			};

			source.ReadTypedList(chat.Messages, Message.Creator);
			return chat;
		}

		public Java.Lang.Object[] NewArray(int size) => new T[size];
	}

	#endregion
}