using System;
using System.Windows;
using System.Windows.Threading;

namespace ReBuzz
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Dispatcher.CurrentDispatcher.UnhandledException += ExHandler;
            AppDomain.CurrentDomain.UnhandledException += ADExHandler;
            base.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        public static void ExHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(string.Concat(e.Exception.Message + "\n", e.Exception.StackTrace), e.Exception.Source);
            e.Handled = true;
        }

        public static void ADExHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(string.Concat(ex.Message + "\n", ex.StackTrace), ex.Source);
            }
        }
    }
}
