using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.UI;
using Zenith.Core.Models;
using Zenith.Core.Parsers;

namespace Zenith_MC_Server.Views
{
    public sealed partial class ConfigEditorPage : Page
    {
        private string _serverPropertiesPath = string.Empty;
        private string _originalPropertiesContent = string.Empty;
        private List<ConfigProperty> _parsedProperties = new();

        public ConfigEditorPage()
        {
            InitializeComponent();
            Loaded += ConfigEditorPage_Loaded;
        }

        private void ConfigEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var server = app.ProcessController.CurrentServer ?? (app.ServerManager.Servers.Count > 0 ? app.ServerManager.Servers[0] : null);
            if (server != null)
            {
                _serverPropertiesPath = Path.Combine(server.ServerPath, "server.properties");
                if (File.Exists(_serverPropertiesPath))
                {
                    _originalPropertiesContent = app.FileExplorerService.ReadFileText(_serverPropertiesPath);
                    _parsedProperties = app.ConfigEditorService.ParseServerProperties(_originalPropertiesContent);
                    PopulateControls();
                }
            }
        }

        private void PopulateControls()
        {
            foreach (var prop in _parsedProperties)
            {
                switch (prop.Key.ToLowerInvariant())
                {
                    case "motd":
                        MotdInputBox.Text = prop.Value;
                        break;
                    case "max-players":
                        if (double.TryParse(prop.Value, out double maxP)) MaxPlayersBox.Value = maxP;
                        break;
                    case "server-port":
                        if (double.TryParse(prop.Value, out double port)) ServerPortBox.Value = port;
                        break;
                    case "difficulty":
                        SelectComboValue(DifficultyCombo, prop.Value);
                        break;
                    case "gamemode":
                        SelectComboValue(GamemodeCombo, prop.Value);
                        break;
                    case "pvp":
                        PvpToggle.IsOn = prop.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "allow-flight":
                        AllowFlightToggle.IsOn = prop.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "hardcore":
                        HardcoreToggle.IsOn = prop.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "online-mode":
                        OnlineModeToggle.IsOn = prop.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }
            UpdateMotdPreview();
        }

        private void SelectComboValue(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item && item.Content.ToString()?.Equals(value, StringComparison.OrdinalIgnoreCase) == true)
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
        }

        private void MotdInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateMotdPreview();
        }

        private void UpdateMotdPreview()
        {
            MotdPreviewRichText.Blocks.Clear();
            var paragraph = new Paragraph();

            var segments = MinecraftMotdParser.ParseMotd(MotdInputBox.Text);
            foreach (var seg in segments)
            {
                var run = new Run { Text = seg.Text };
                try
                {
                    if (!string.IsNullOrEmpty(seg.ColorHex) && seg.ColorHex.Length >= 7)
                    {
                        byte r = Convert.ToByte(seg.ColorHex.Substring(1, 2), 16);
                        byte g = Convert.ToByte(seg.ColorHex.Substring(3, 2), 16);
                        byte b = Convert.ToByte(seg.ColorHex.Substring(5, 2), 16);
                        var color = Color.FromArgb(255, r, g, b);
                        run.Foreground = new SolidColorBrush(color);
                    }
                }
                catch { }

                if (seg.IsBold) run.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
                if (seg.IsItalic) run.FontStyle = Windows.UI.Text.FontStyle.Italic;

                paragraph.Inlines.Add(run);
            }

            MotdPreviewRichText.Blocks.Add(paragraph);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null || string.IsNullOrEmpty(_serverPropertiesPath)) return;

            UpdateProperty("motd", MotdInputBox.Text);
            UpdateProperty("max-players", MaxPlayersBox.Value.ToString());
            UpdateProperty("server-port", ServerPortBox.Value.ToString());
            if (DifficultyCombo.SelectedItem is ComboBoxItem diffItem) UpdateProperty("difficulty", diffItem.Content.ToString() ?? "easy");
            if (GamemodeCombo.SelectedItem is ComboBoxItem gmItem) UpdateProperty("gamemode", gmItem.Content.ToString() ?? "survival");
            UpdateProperty("pvp", PvpToggle.IsOn ? "true" : "false");
            UpdateProperty("allow-flight", AllowFlightToggle.IsOn ? "true" : "false");
            UpdateProperty("hardcore", HardcoreToggle.IsOn ? "true" : "false");
            UpdateProperty("online-mode", OnlineModeToggle.IsOn ? "true" : "false");

            string updatedContent = app.ConfigEditorService.SaveServerProperties(_originalPropertiesContent, _parsedProperties);
            app.FileExplorerService.WriteFileText(_serverPropertiesPath, updatedContent);
            _originalPropertiesContent = updatedContent;
        }

        private void UpdateProperty(string key, string value)
        {
            var prop = _parsedProperties.Find(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (prop != null)
            {
                prop.Value = value;
            }
            else
            {
                _parsedProperties.Add(new ConfigProperty { Key = key, Value = value });
            }
        }
    }
}
