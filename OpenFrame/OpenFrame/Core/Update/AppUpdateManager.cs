using OpenFrame.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;
using MessageBox = System.Windows.MessageBox;

namespace OpenFrame.Core.Update
{
    public static class AppUpdateManager
    {
        public static void CheckForUpdates()
        {
            var githubSource = new GithubSource("https://github.com/flowhl/openframe", null, false);

            var mgr = new UpdateManager(githubSource, new UpdateOptions { AllowVersionDowngrade = false });
            // check for new version
            var newVersion = mgr.CheckForUpdates();
            if (newVersion == null)
                return; // no update available

            //ask user if they want to update
            var result = MessageBox.Show($"New version {newVersion?.TargetFullRelease?.Version.ToString()} available. Do you want to update?", "Update available", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.No)
                return;

            // download new version
            mgr.DownloadUpdates(newVersion);

            // install new version and restart app
            mgr.ApplyUpdatesAndRestart(newVersion);
        }
    }
}
