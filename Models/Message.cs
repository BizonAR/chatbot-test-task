using System;
using Android.OS;
using Android.Runtime;
using Java.Lang;
using SQLite;


public class Message : Java.Lang.Object, IParcelable
{
	#region Поля

	private bool _disposed = false;

	#endregion

	#region Константы

	public static readonly IParcelableCreator Creator = new ParcelableCreator<Message>();

	#endregion

	#region Свойства

	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	public int ChatId { get; set; }

	public string Text { get; set; }

	public DateTime Date { get; set; }

	public string Sender { get; set; }

	public int? ReplyToMessageId { get; set; }

	public string ReplyPreviewText { get; set; }

	#endregion

	#region Конструкторы

	public Message() { }

	protected Message(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

	#endregion

	#region Финализаторы

	~Message()
	{
		Dispose(false);
	}

	#endregion

	#region Методы

	public void Reset()
	{
		Id = 0;
		ChatId = 0;
		Text = string.Empty;
		Date = DateTime.MinValue;
		Sender = string.Empty;
	}

	public Message CopyFrom(Message source)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));

		Id = source.Id;
		ChatId = source.ChatId;
		Text = source.Text;
		Date = source.Date;
		Sender = source.Sender;

		return this;
	}

	public int DescribeContents() => 0;

	public void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
	{
		dest.WriteInt(Id);
		dest.WriteInt(ChatId);
		dest.WriteString(Text);
		dest.WriteLong(Date.Ticks);
		dest.WriteString(Sender);
	}

	public new void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected new virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			Text = string.Empty;
			Sender = string.Empty;
		}

		_disposed = true;
		base.Dispose();
	}

	public override bool Equals(object obj)
	{
		return obj is Message other && Id == other.Id;
	}

	public override int GetHashCode()
	{
		return Id.GetHashCode();
	}

	#endregion

	#region Вложенные классы

	private class ParcelableCreator<T> : Java.Lang.Object, IParcelableCreator where T : Message, new()
	{
		public Java.Lang.Object CreateFromParcel(Parcel source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var message = new T
			{
				Id = source.ReadInt(),
				ChatId = source.ReadInt(),
				Text = source.ReadString() ?? string.Empty,
				Date = new DateTime(source.ReadLong()),
				Sender = source.ReadString() ?? string.Empty
			};

			return message;
		}

		public Java.Lang.Object[] NewArray(int size) => new T[size];

		public new void Dispose()
		{
			base.Dispose();
		}
	}

	#endregion
}
