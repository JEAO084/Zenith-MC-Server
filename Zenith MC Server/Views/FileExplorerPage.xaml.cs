using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using System.Text;
using Zenith.Core.Services;

namespace Zenith_MC_Server.Views
{
    public sealed partial class FileExplorerPage : Page
    {
        private string? _selectedFilePath;

        public FileExplorerPage()
        {
            InitializeComponent();
            Loaded += FileExplorerPage_Loaded;
        }

        private void FileExplorerPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var server = app.ProcessController.CurrentServer ?? (app.ServerManager.Servers.Count > 0 ? app.ServerManager.Servers[0] : null);
            if (server != null)
            {
                var tree = app.FileExplorerService.GetDirectoryStructure(server.ServerPath);
                FileTreeView.ItemsSource = tree;
            }
        }

        private void FileTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is FileExplorerItem item && !item.IsDirectory && MainWindow.Current != null)
            {
                _selectedFilePath = item.FullPath;
                CurrentFilePathTextBlock.Text = item.FullPath;

                string content = MainWindow.Current.FileExplorerService.ReadFileText(item.FullPath);
                FileEditorTextBox.Text = content;
                SaveFileButton.IsEnabled = true;
                UpdateLineNumbers();
            }
        }

        private void FileEditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLineNumbers();
        }

        private void UpdateLineNumbers()
        {
            string text = FileEditorTextBox.Text;
            int lineCount = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') lineCount++;
            }

            var sb = new StringBuilder();
            for (int i = 1; i <= lineCount; i++)
            {
                sb.AppendLine(i.ToString());
            }

            LineNumbersTextBlock.Text = sb.ToString();
        }

        private void SaveFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedFilePath) && MainWindow.Current != null)
            {
                string textToSave = FileEditorTextBox.Text;

                if (_selectedFilePath.EndsWith(".yml", System.StringComparison.OrdinalIgnoreCase) ||
                    _selectedFilePath.EndsWith(".yaml", System.StringComparison.OrdinalIgnoreCase))
                {
                    textToSave = MainWindow.Current.EditorService.FormatYamlIndentation(textToSave);
                }

                MainWindow.Current.FileExplorerService.WriteFileText(_selectedFilePath, textToSave);
            }
        }
    }
}
