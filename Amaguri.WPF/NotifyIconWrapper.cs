using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Amaguri.WPF
{
    public partial class NotifyIconWrapper : Component
    {
        public NotifyIconWrapper()
        {
            InitializeComponent();

            notifyIcon1.Text = AppDomain.CurrentDomain.FriendlyName;
            notifyIcon1.DoubleClick += (s, e) => { ShowMainWindow(); };
            notifyIcon1.Icon = System.Drawing.Icon.ExtractAssociatedIcon(AppDomain.CurrentDomain.FriendlyName);

            toolStripMenuItemShow.Click += (s, e) => { ShowMainWindow(); };

            toolStripMenuItemExit.Click += (s, e) => { System.Windows.Application.Current.Shutdown(); };
        }

        private bool IsSettingsWindowShown { get; set; } = false;

        private void ShowMainWindow()
        {
            if (IsSettingsWindowShown) return;

            var settingsWindow = new Views.SettingsWindow()
            {
                Width = 640,
                Height = 480,
                WindowStyle = System.Windows.WindowStyle.ToolWindow,
            };

            settingsWindow.Closed += (s, a) =>
            {
                IsSettingsWindowShown = false; 
                
                // 設定が変わったら property notify
            };

            settingsWindow.Show();

            IsSettingsWindowShown = true;
        }

        // public NotifyIconWrapper(IContainer container)
        // {
        //     container.Add(this);
        // 
        //     InitializeComponent();
        // }
    }
}
