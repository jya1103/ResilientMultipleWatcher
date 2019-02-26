using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TheLostDotNETChronicles.FileSystemWatchers
{
    /// <summary>
    /// Simple test program for NetFileSystemWatcher
    /// </summary>
    internal class Program
    {
        private static void Log(string format, params object[] arg)
        {
            Console.Write("{0:T} ", DateTime.Now);
            Console.WriteLine(format, arg);
        }

        private static void HandleFileEvent(object sender, FileSystemEventArgs e)
        {
            Log("Event received: {0} Path: {1}", e.ChangeType, e.FullPath);
        }

        private static void HandleRenameEvent(object sender, RenamedEventArgs e)
        {
            Log("Event received: {0} Path: {1} OldPath: {2}", e.ChangeType, e.FullPath, e.OldFullPath);
        }

        private static void HandleErrorEvent(object sender, ErrorEventArgs e)
        {
            var exception = e.GetException();

            Log("Error received: {0} ({1})", exception.Message, exception.GetType());
        }

        private static void HandleConnectStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            Log("Connect state changed: {0}", e.ConnectionState);
        }

        private static void Main(string[] args)
        {
            var checkConnectionInterval = TimeSpan.FromSeconds(10); // Low value for testing!
            var directory = @"\\192.168.1.4\c$\Local";
            var pattern = "*.*";

            using (var watcher = new NetFileSystemWatcher(checkConnectionInterval, directory, pattern))
            {
                // These are the settings we used in our code
                watcher.IncludeSubdirectories = false;
                watcher.NotifyFilter = NotifyFilters.FileName;

                //Subscribe to all events.
                watcher.Created += HandleFileEvent;
                watcher.Changed += HandleFileEvent;
                watcher.Deleted += HandleFileEvent;
                watcher.Renamed += HandleRenameEvent;
                watcher.Error += HandleErrorEvent;
                watcher.ConnectionStateChanged += HandleConnectStateChanged;

                //Enable the NetFileSystemWatcher events.
                watcher.EnableRaisingEvents = true;

                Console.WriteLine("Press ENTER to exit");
                Console.ReadLine();
            }
        }
    }
}
