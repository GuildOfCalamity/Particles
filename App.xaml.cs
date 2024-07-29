using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Particles
{
    /// <summary>
    /// NOTE: To run this as a screen saver from \Windows\System32 directory...
    /// Assuming you have a 64 bit machine, keep in mind that System32 is a folder that is reserved for 64 bit application usage, 
    /// and although it may seem strange, SysWOW64 contains 32 bit dlls and is reserved for 32-bit applications.Typically, 32-bit 
    /// applications that access System32 will go through a file system redirector to the SysWOW64 folder. More info here.
    /// However, when your application (which runs as a 32-bit process) runs in System32 itself, the redirector probably doesn't 
    /// do anything because it thinks there isn't any need to redirect, which is why your app works outside of System32 but not 
    /// inside it.
    /// So to solve this, uncheck Prefer 32-bit (build properties of project) so that it will try to target 64 bit platform, 
    /// ...or better yet, put the app elsewhere and add the application directory to your environment path variable. That way 
    /// you can still access your application .exe anywhere, and it won't pollute your System32 folder which should only be 
    /// used for Windows files anyways.
    ///
    /// Alternative Answer...
    /// If you put your 32-bit exe in both the System32 and the SysWOW64 folder.It works just fine.Not one, not the other, 
    /// but both folders. This might sound strange, but try it.If you put the same exe in both folders it will start up 
    /// without any modifications.
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _mutex;


        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            //if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            //    App.Current.Shutdown();

            bool aIsNewInstance = false;
            _mutex = new Mutex(true, "Orbs_By_Manaconda_v1", out aIsNewInstance);
            if (!aIsNewInstance)
            {
                //MessageBox.Show("Already an instance is running...");
                App.Current.Shutdown();
            }

            // Run our new window in the lowest priority.
            /*
            App.Current.Dispatcher.Invoke((Action)delegate ()
            {
                MainWindow w = new MainWindow();
                w.Show();
            }, System.Windows.Threading.DispatcherPriority.Background);
            */

            MainWindow w = new MainWindow();
            w.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            base.OnExit(e);
        }
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Unhandled exception thrown from Dispatcher {e.Dispatcher.Thread.ManagedThreadId}: {e.Exception}");
                MessageBox.Show(e.Exception.Message, "DispatcherUnhandledException");
                //System.Diagnostics.EventLog.WriteEntry(SystemTitle, $"Unhandled exception thrown from Dispatcher {e.Dispatcher.Thread.ManagedThreadId}: {e.Exception.ToString()}");
                e.Handled = true;
            }
            catch (Exception) { }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Unhandled exception thrown: {((Exception)e.ExceptionObject).Message}");
                MessageBox.Show(((Exception)e.ExceptionObject).Message, "DomainUnhandledException");
                //System.Diagnostics.EventLog.WriteEntry(SystemTitle, $"Unhandled exception thrown:\r\n{((Exception)e.ExceptionObject).ToString()}");
            }
            catch (Exception) { }
        }
    }

}
