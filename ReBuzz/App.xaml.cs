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
            ExitOrIgnoreDependingOnUserChoice(e.Exception);
            e.Handled = true;
        }

        public static void ADExHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ExitOrIgnoreDependingOnUserChoice(ex);
            }
        }

        private static void ExitOrIgnoreDependingOnUserChoice(Exception ex)
        {
            var result = MessageBox.Show(
                GetFullExceptionMessage(ex)
                + "Do you want to the application to terminate? " +
                "Not terminating the application might leave an unresponsive process running in the background.",
                "Unhandled Exception — " + (ex.Source ?? "ReBuzz"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Error,
                MessageBoxResult.No);

            if (result == MessageBoxResult.Yes)
            {
                Environment.Exit(1);
            }
        }

        private static string GetFullExceptionMessage(Exception ex)
        {
            var message = "";
            while (ex != null)
            {
                message += ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace + "\n\n";
                ex = ex.InnerException;
            }
            return message;
        }
    }
}
