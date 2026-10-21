using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class FileManagerPage : Page
    {
        private MainWindow? _mainWindow;
        private FileTreeNode? _selectedFileTreeNode;

        public FileManagerPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
            RefreshFileList();
        }

        public void RefreshFileList()
        {
            if (_mainWindow == null || _mainWindow.ActiveProfile == null || TreeFileExplorer == null) return;
            var profile = _mainWindow.ActiveProfile;

            if (!Directory.Exists(profile.DirectoryPath))
            {
                Directory.CreateDirectory(profile.DirectoryPath);
            }

            var treeRoot = FileTreeNode.BuildTree(profile.DirectoryPath);
            TreeFileExplorer.ItemsSource = treeRoot.Children;
        }

        private void BtnRefreshFiles_Click(object sender, RoutedEventArgs e)
        {
            RefreshFileList();
        }

        private void TreeFileExplorer_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (_mainWindow == null || args.InvokedItem is not FileTreeNode node) return;

            _selectedFileTreeNode = node;
            if (TxtEditingFileName != null) TxtEditingFileName.Text = node.Name;

            if (!node.IsDirectory)
            {
                string content = _mainWindow.FileManager.ReadFileText(node.FullPath);
                if (TxtCodeEditor != null) TxtCodeEditor.Text = content;
                if (TxtFileEditorStatus != null) TxtFileEditorStatus.Text = $"Editando: {node.RelativePath} ({node.FormattedSize})";
            }
            else
            {
                if (TxtCodeEditor != null) TxtCodeEditor.Text = $"// Carpeta: {node.RelativePath}";
                if (TxtFileEditorStatus != null) TxtFileEditorStatus.Text = $"Carpeta seleccionada: {node.RelativePath}";
            }
        }

        private void BtnSaveFileContent_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow != null && _selectedFileTreeNode != null && !_selectedFileTreeNode.IsDirectory && TxtCodeEditor != null)
            {
                _mainWindow.FileManager.SaveFileText(_selectedFileTreeNode.FullPath, TxtCodeEditor.Text);
                if (TxtFileEditorStatus != null) TxtFileEditorStatus.Text = $"¡Guardado exitosamente a las {DateTime.Now:HH:mm:ss}!";
                RefreshFileList();
            }
        }

        private void BtnNewFile_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null || _mainWindow.ActiveProfile == null) return;
            var profile = _mainWindow.ActiveProfile;

            string targetDir = profile.DirectoryPath;
            if (_selectedFileTreeNode != null)
            {
                targetDir = _selectedFileTreeNode.IsDirectory ? _selectedFileTreeNode.FullPath : Path.GetDirectoryName(_selectedFileTreeNode.FullPath) ?? profile.DirectoryPath;
            }

            string newFileName = $"config_custom_{DateTime.Now:mm_ss}.txt";
            string newFilePath = Path.Combine(targetDir, newFileName);
            _mainWindow.FileManager.CreateFile(newFilePath, "# Archivo de configuración\n");
            RefreshFileList();
        }

        private void BtnDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow != null && _selectedFileTreeNode != null)
            {
                _mainWindow.FileManager.DeleteItem(_selectedFileTreeNode.FullPath);
                if (TxtCodeEditor != null) TxtCodeEditor.Text = string.Empty;
                if (TxtEditingFileName != null) TxtEditingFileName.Text = "Ningún archivo seleccionado";
                _selectedFileTreeNode = null;
                RefreshFileList();
            }
        }
    }
}
