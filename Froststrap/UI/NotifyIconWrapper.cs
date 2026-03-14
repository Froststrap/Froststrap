using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Froststrap.Integrations;
using Froststrap.UI.Elements.ContextMenu;

namespace Froststrap.UI
{
	public class NotifyIconWrapper : IDisposable
	{
		public NotifyIconWrapper(Watcher watcher)
		{
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);

		}
	}
}