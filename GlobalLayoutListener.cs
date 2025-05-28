using System;
using Android.Views;

namespace ChatApp
{
	public class GlobalLayoutListener : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
	{
		#region Поля

		private readonly Action _action;

		#endregion

		#region Конструкторы

		public GlobalLayoutListener(Action action)
		{
			_action = action;
		}

		#endregion

		#region Методы

		public void OnGlobalLayout()
		{
			_action?.Invoke();
		}

		#endregion
	}
}
