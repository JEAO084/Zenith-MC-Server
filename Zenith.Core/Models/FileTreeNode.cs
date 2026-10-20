using System.Collections.Generic;
using System.IO;

namespace Zenith.Core.Models
{
    public class FileTreeNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long SizeBytes { get; set; }
        public string FormattedSize => IsDirectory ? "<CARPETA>" : $"{SizeBytes / 1024.0:F1} KB";
        public List<FileTreeNode> Children { get; set; } = new();

        public static FileTreeNode BuildTree(string rootPath)
        {
            var dirInfo = new DirectoryInfo(rootPath);
            var rootNode = new FileTreeNode
            {
                Name = dirInfo.Name,
                FullPath = dirInfo.FullName,
                RelativePath = dirInfo.Name,
                IsDirectory = true
            };

            PopulateChildren(rootNode, dirInfo, rootPath);
            return rootNode;
        }

        private static void PopulateChildren(FileTreeNode parentNode, DirectoryInfo dirInfo, string rootPath)
        {
            try
            {
                foreach (var dir in dirInfo.GetDirectories())
                {
                    var childDirNode = new FileTreeNode
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        RelativePath = Path.GetRelativePath(rootPath, dir.FullName),
                        IsDirectory = true
                    };
                    parentNode.Children.Add(childDirNode);
                    PopulateChildren(childDirNode, dir, rootPath);
                }

                foreach (var file in dirInfo.GetFiles())
                {
                    parentNode.Children.Add(new FileTreeNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        RelativePath = Path.GetRelativePath(rootPath, file.FullName),
                        IsDirectory = false,
                        SizeBytes = file.Length
                    });
                }
            }
            catch { }
        }
    }
}
