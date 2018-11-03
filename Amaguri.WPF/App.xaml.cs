using System;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Amaguri.WPF
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        [DllImport("user32.dll", SetLastError = true)]
        private extern static void AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private extern static void RemoveClipboardFormatListener(IntPtr hwnd);

        public NotifyIconWrapper notifyIcon { get; } = new NotifyIconWrapper();

        public Window MonitorWindow = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            MonitorWindow = new Window() // 隠しウィンドウを作ってクリップボードを監視
            {
                Title = "Amaguri Clipboard Monitor",
                Top = -10000,
                Left = -10000,
                Width = 0,
                Height = 0,
                Visibility = Visibility.Hidden,
            };

            MonitorWindow.Loaded += (s, a) =>
            {
                var handle = new WindowInteropHelper(MonitorWindow).Handle;
                HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook(WndProc));
                AddClipboardFormatListener(handle);
            };

            MonitorWindow.Closed += (s, a) =>
            {
                var handle = new WindowInteropHelper(MonitorWindow).Handle;
                RemoveClipboardFormatListener(handle);
            };

            MonitorWindow.Show();

            notifyIcon.ShowBalloonTip(3 * 1000, "Amaguri", "起動しました", System.Windows.Forms.ToolTipIcon.Info);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            notifyIcon.Dispose();
        }

        private const int WM_CLIPBOARDUPDATE = 0x31D;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_CLIPBOARDUPDATE) return IntPtr.Zero;

            var defaultSettings = WPF.Properties.Settings.Default;

            BitmapSource source = null;

            try
            {
                if (Clipboard.ContainsImage())
                {
                    source = Clipboard.GetImage();
                }
                else if (Clipboard.ContainsFileDropList() && defaultSettings.ReplaceClipboardImageFileToImageData)
                {
                    var path = Clipboard.GetFileDropList().Cast<string>().FirstOrDefault();
                    source = new BitmapImage(new Uri(path));
                }
                else
                {
                    return IntPtr.Zero;
                }

                source.Freeze(); // メモリリーク対策らしい
            }
            catch (Exception exception)
            {
                ShowNotification($"{exception.Message}\nPlease try later");

                return IntPtr.Zero;
            }

            if (source == null || source.HasAnimatedProperties) return IntPtr.Zero; // 不完全だけど、アニメーション GIF 対策

            if (defaultSettings.ScaleClipboardImageData && !(defaultSettings.SkipScaleIfShiftKeyDown && IsShiftKeyDown()))
            {
                double scale = 1.0;

                if (defaultSettings.ScaleClipboardImageDataIfExceedWidth &&
                    defaultSettings.MaxWidth < source.PixelWidth)
                {
                    if (defaultSettings.ScaleClipboardImageDataIfExceedHeight &&
                        defaultSettings.MaxHeight < source.PixelHeight)
                    {
                        if (source.PixelHeight > source.PixelWidth)
                        {
                            scale = (double)defaultSettings.MaxHeight / source.PixelHeight;
                        }
                        else
                        {
                            scale = (double)defaultSettings.MaxWidth / source.PixelWidth;
                        }
                    }
                    else
                    {
                        scale = (double)defaultSettings.MaxWidth / source.PixelWidth;
                    }
                }

                if (scale < 1.0)
                {
                    if (defaultSettings.UseScaledImageInSavingToDesktop)
                    {
                        SaveClipboardImageToDesktop(source, "Clipboard-", ".original");
                    }

                    // 一部アプリがコピーしたイメージが黒くなる？
                    // var transformedBitmap = new TransformedBitmap(source, new ScaleTransform(scale, scale));
                    // transformedBitmap.Freeze();
                    // Clipboard.SetImage(transformedBitmap);

                    var width = Convert.ToInt32(source.PixelWidth * scale);
                    var height = Convert.ToInt32(source.PixelHeight * scale);

                    System.Drawing.Bitmap original = null;
                    System.Drawing.Bitmap scaled = null;

                    try
                    {
                        original = source.ToBitmap(); // ここでエラーなるかも
                        scaled = new System.Drawing.Bitmap(width, height);

                        using (var g = System.Drawing.Graphics.FromImage(scaled))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(original, 0, 0, width, height);

                            Clipboard.SetImage(scaled.ToBitmapSource());
                        }
                    }
                    catch (Exception exception)
                    {
                        ShowNotification($"{exception.Message}\nPlease try later");
                    }
                    finally
                    {
                        original?.Dispose();
                        scaled?.Dispose();
                    }

                    return IntPtr.Zero;
                }
            }

            if (defaultSettings.SaveClipboardImageDataToDesktop)
            {
                SaveClipboardImageToDesktop(source, "Clipboard-");

                if (defaultSettings.BeepOnSaving)
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }

                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private void ShowNotification(string message)
        {
            notifyIcon.ShowBalloonTip(
                3 * 1000,
                "Amaguri", message,
                System.Windows.Forms.ToolTipIcon.Error
            );
        }

        private static void SaveClipboardImageToDesktop(BitmapSource source, string prefix = "", string suffix = "")
        {
            // Bitmap じゃないと黒い画像が出力されることがある

            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var fileName = $"{prefix}{DateTime.Now.ToString("yyyMMdd-hhmmss")}{suffix}.bmp";

            using (var fileStream = new System.IO.FileStream(System.IO.Path.Combine(desktopDir, fileName), System.IO.FileMode.Create))
            {
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(fileStream);
            }
        }

        private static bool IsShiftKeyDown()
        {
            return (Keyboard.GetKeyStates(Key.LeftShift) & KeyStates.Down) == KeyStates.Down
                || (Keyboard.GetKeyStates(Key.RightShift) & KeyStates.Down) == KeyStates.Down;
        }
    }
}
