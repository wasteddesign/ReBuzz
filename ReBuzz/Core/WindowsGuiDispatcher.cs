using System;
using System.Windows;
using System.Windows.Threading;

namespace ReBuzz.Core;

public interface IUiDispatcher
{
  void Invoke(Action action);
  void BeginInvoke(Action action);
  void BeginInvoke(Action action, DispatcherPriority priority);
}

public class WindowsGuiDispatcher //bug move
  : IUiDispatcher
{
  public void Invoke(Action action)
  {
    Application.Current.Dispatcher.Invoke(action);
  }

  public void BeginInvoke(Action action)
  {
    Application.Current.Dispatcher.BeginInvoke(action);
  }

  public void BeginInvoke(Action action, DispatcherPriority priority)
  {
    Application.Current.Dispatcher.BeginInvoke(action, priority);
  }
}