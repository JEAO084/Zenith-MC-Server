using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class ServerConfigPage : Page
    {
        private MainWindow? _mainWindow;
        private ServerPropertiesConfig _currentProperties = new();

        public ServerConfigPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
            LoadConfigurationToUI();
        }

        public void LoadConfigurationToUI()
        {
            if (_mainWindow == null || _mainWindow.ActiveProfile == null) return;
            var profile = _mainWindow.ActiveProfile;

            _currentProperties = _mainWindow.ConfigService.LoadProperties(profile.DirectoryPath);

            if (CfgMotd != null) CfgMotd.Text = _currentProperties.GetProperty("motd", "A Minecraft Server");
            if (CfgMaxPlayers != null) CfgMaxPlayers.Text = _currentProperties.GetProperty("max-players", "20");
            if (CfgServerPort != null) CfgServerPort.Text = _currentProperties.GetProperty("server-port", "25565");

            if (CfgOnlineMode != null) CfgOnlineMode.IsOn = _currentProperties.GetProperty("online-mode", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (CfgPvp != null) CfgPvp.IsOn = _currentProperties.GetProperty("pvp", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (CfgAllowFlight != null) CfgAllowFlight.IsOn = _currentProperties.GetProperty("allow-flight", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

            if (ToggleEula != null) ToggleEula.IsOn = _mainWindow.ConfigService.CheckEulaAccepted(profile.DirectoryPath);

            if (TxtServerCustomJavaExe != null) TxtServerCustomJavaExe.Text = profile.JavaExecutablePath;
            if (TxtCustomJvmArgs != null) TxtCustomJvmArgs.Text = profile.CustomJvmArgs;

            var paperCfg = _mainWindow.ConfigService.LoadSpecificConfig(profile.DirectoryPath, "paper-global.yml", "# Paper Global Config\nverbose: false\n");
            if (TxtPaperConfigEditor != null) TxtPaperConfigEditor.Text = paperCfg.Content;

            var spigotCfg = _mainWindow.ConfigService.LoadSpecificConfig(profile.DirectoryPath, "spigot.yml", "# Spigot Config\nconfig-version: 12\n");
            if (TxtSpigotConfigEditor != null) TxtSpigotConfigEditor.Text = spigotCfg.Content;
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null || _mainWindow.ActiveProfile == null) return;
            var profile = _mainWindow.ActiveProfile;

            if (CfgMotd != null) _currentProperties.SetProperty("motd", CfgMotd.Text);
            if (CfgMaxPlayers != null) _currentProperties.SetProperty("max-players", CfgMaxPlayers.Text);
            if (CfgServerPort != null) _currentProperties.SetProperty("server-port", CfgServerPort.Text);

            if (CfgOnlineMode != null) _currentProperties.SetProperty("online-mode", CfgOnlineMode.IsOn.ToString().ToLower());
            if (CfgPvp != null) _currentProperties.SetProperty("pvp", CfgPvp.IsOn.ToString().ToLower());
            if (CfgAllowFlight != null) _currentProperties.SetProperty("allow-flight", CfgAllowFlight.IsOn.ToString().ToLower());

            if (TxtServerCustomJavaExe != null) profile.JavaExecutablePath = TxtServerCustomJavaExe.Text.Trim();
            if (TxtCustomJvmArgs != null) profile.CustomJvmArgs = TxtCustomJvmArgs.Text.Trim();

            _mainWindow.ServerManager.UpdateProfile(profile);
            _mainWindow.ConfigService.SaveProperties(profile.DirectoryPath, _currentProperties);
            if (ToggleEula != null) _mainWindow.ConfigService.SetEulaAccepted(profile.DirectoryPath, ToggleEula.IsOn);

            _mainWindow.AppendConsoleLog("[Zenith] Configuración guardada en server.properties y eula.txt");
        }

        private void BtnSavePaperConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null || _mainWindow.ActiveProfile == null || TxtPaperConfigEditor == null) return;
            _mainWindow.ConfigService.SaveSpecificConfig(_mainWindow.ActiveProfile.DirectoryPath, "paper-global.yml", TxtPaperConfigEditor.Text);
            _mainWindow.AppendConsoleLog("[Zenith] Guardado paper-global.yml exitosamente.");
        }

        private void BtnSaveSpigotConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null || _mainWindow.ActiveProfile == null || TxtSpigotConfigEditor == null) return;
            _mainWindow.ConfigService.SaveSpecificConfig(_mainWindow.ActiveProfile.DirectoryPath, "spigot.yml", TxtSpigotConfigEditor.Text);
            _mainWindow.AppendConsoleLog("[Zenith] Guardado spigot.yml exitosamente.");
        }
    }
}
