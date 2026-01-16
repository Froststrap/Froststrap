using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Animation;
using Froststrap.UI.ViewModels.About;
using System.Collections.Generic;
using System.Linq;

namespace Froststrap.UI.Elements.About.Pages
{
	public partial class AboutPage : UserControl
	{
		private readonly Queue<Key> _keys = new();

		private readonly List<Key> _expectedKeys = new() { Key.M, Key.A, Key.T, Key.T, Key.LeftShift, Key.D1 };

		private bool _triggered = false;

		public AboutPage()
		{
			DataContext = new AboutViewModel();
			InitializeComponent();
		}

		private async void UiPage_KeyDown(object sender, KeyEventArgs e)
		{
			if (_triggered)
				return;

			if (_keys.Count >= 6)
				_keys.Dequeue();

			var key = e.Key;

			if (key == Key.RightShift)
				key = Key.LeftShift;

			_keys.Enqueue(key);

			if (_keys.SequenceEqual(_expectedKeys))
			{
				_triggered = true;

				if (Resources.TryGetResource("EggAnimation", null, out object? res) && res is Animation animation)
				{
					var task1 = animation.RunAsync(this.FindControl<Image>("Image1"));
					var task2 = animation.RunAsync(this.FindControl<Image>("Image2"));

					await System.Threading.Tasks.Task.WhenAll(task1, task2);
				}
			}
		}
	}
}