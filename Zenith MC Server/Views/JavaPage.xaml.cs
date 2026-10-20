using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class JavaPage : Page
    {
        private MainWindow? _mainWindow;

        public JavaPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
            LoadJavaRuntimesUI();
        }

        public void LoadJavaRuntimesUI()
        {
            if (_mainWindow == null || LstJavaVersions == null) return;
            var versions = _mainWindow.JavaService.GetAvailableJavaVersions();
            LstJavaVersions.ItemsSource = versions;
        }

        private async void BtnInstallJavaItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JavaVersionOption option && _mainWindow != null)
            {
                btn.IsEnabled = false;
                if (JavaDownloadProgress != null) JavaDownloadProgress.Visibility = Visibility.Visible;
                if (TxtJavaProgressStatus != null) TxtJavaProgressStatus.Text = $"Descargando {option.Name}...";

                try
                {
                    string path = await _mainWindow.JavaService.DownloadAndExtractJavaAsync(option, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => { if (JavaDownloadProgress != null) JavaDownloadProgress.Value = p; });
                    });

                    if (TxtJavaProgressStatus != null) TxtJavaProgressStatus.Text = $"¡Java {option.MajorVersion} instalado correctamente!";
                    LoadJavaRuntimesUI();
                }
                catch (Exception ex)
                {
                    if (TxtJavaProgressStatus != null) TxtJavaProgressStatus.Text = $"Error instalando JDK: {ex.Message}";
                }
                finally
                {
                    btn.IsEnabled = true;
                    if (JavaDownloadProgress != null) JavaDownloadProgress.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
