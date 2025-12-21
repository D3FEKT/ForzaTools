using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;

namespace ForzaTools.ForzaAnalyzer
{
    public partial class App : Application
    {
        // Import MessageBox for Release mode debugging
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);

        public static Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();

            // 1. Hook into the UI Thread Exception (WinUI specific)
            this.UnhandledException += App_UnhandledException;

            // 2. Hook into the Background Thread Exception (System specific)
            // FIX: Use 'System.UnhandledExceptionEventHandler' explicitly to avoid ambiguity
            AppDomain.CurrentDomain.UnhandledException += new System.UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
        }

        /// <summary>
        /// Handles exceptions on the main UI thread (WinUI).
        /// </summary>
        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // We use the P/Invoke MessageBox because standard XAML dialogs might fail if the UI thread is corrupted.
            MessageBox(IntPtr.Zero, $"UI Crash: {e.Message}\n\nStack: {e.Exception.StackTrace}", "Critical UI Error", 0x10);

            // Setting Handled = true keeps the app running, but it might be in an unstable state.
            e.Handled = true;
        }

        /// <summary>
        /// Handles exceptions on background threads (System).
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            string msg = ex != null ? ex.Message : "Unknown Error";
            string stack = ex != null ? ex.StackTrace : "";

            MessageBox(IntPtr.Zero, $"Background Crash: {msg}\n\nStack: {stack}", "Critical Background Error", 0x10);
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}