using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Zenith.Core.Models
{
    public class ServerPropertiesConfig
    {
        private readonly Dictionary<string, ServerPropertyItem> _properties = new(StringComparer.OrdinalIgnoreCase);

        public List<ServerPropertyItem> Items => _properties.Values.ToList();

        public ServerPropertiesConfig()
        {
            InitializeDefaultSchema();
        }

        private void InitializeDefaultSchema()
        {
            AddSchemaItem("motd", "A Minecraft Server", "A Minecraft Server", PropertyType.String, PropertyCategory.General, "Mensaje del Día (MOTD)", "Descripción visible en la lista de servidores de Minecraft.");
            AddSchemaItem("server-port", "25565", "25565", PropertyType.Integer, PropertyCategory.Network, "Puerto del Servidor", "Puerto TCP/UDP de conexión (Por defecto: 25565).");
            AddSchemaItem("max-players", "20", "20", PropertyType.Integer, PropertyCategory.General, "Jugadores Máximos", "Número máximo de jugadores simultáneos.");
            AddSchemaItem("gamemode", "survival", "survival", PropertyType.Enum, PropertyCategory.Gameplay, "Modo de Juego", "Modo de juego predeterminado para nuevos jugadores.", new List<string> { "survival", "creative", "adventure", "spectator" });
            AddSchemaItem("difficulty", "easy", "easy", PropertyType.Enum, PropertyCategory.Gameplay, "Dificultad", "Dificultad global del mundo.", new List<string> { "peaceful", "easy", "normal", "hard" });
            AddSchemaItem("pvp", "true", "true", PropertyType.Boolean, PropertyCategory.Gameplay, "Permitir PVP", "Permite que los jugadores se ataquen entre sí.");
            AddSchemaItem("hardcore", "false", "false", PropertyType.Boolean, PropertyCategory.Gameplay, "Modo Hardcore", "Si es verdadero, los jugadores mueren permanentemente en modo espectador.");
            AddSchemaItem("online-mode", "true", "true", PropertyType.Boolean, PropertyCategory.Security, "Modo Online (Cuentas Premium)", "Verifica las cuentas con los servidores de Mojang.");
            AddSchemaItem("white-list", "false", "false", PropertyType.Boolean, PropertyCategory.Security, "Lista Blanca (Whitelist)", "Solo permite la entrada a jugadores autorizados en whitelist.json.");
            AddSchemaItem("spawn-protection", "16", "16", PropertyType.Integer, PropertyCategory.World, "Protección de Spawn", "Radio en bloques alrededor del spawn protegido para op.");
            AddSchemaItem("view-distance", "10", "10", PropertyType.Integer, PropertyCategory.Performance, "Distancia de Visión (View Distance)", "Distancia en chunks enviada a los clientes.");
            AddSchemaItem("simulation-distance", "10", "10", PropertyType.Integer, PropertyCategory.Performance, "Distancia de Simulación", "Distancia en chunks donde las entidades/mobs están activas.");
            AddSchemaItem("allow-flight", "false", "false", PropertyType.Boolean, PropertyCategory.Gameplay, "Permitir Vuelo", "Permite el vuelo en modo supervivencia si se detectan mods o plugins.");
            AddSchemaItem("level-name", "world", "world", PropertyType.String, PropertyCategory.World, "Nombre del Mundo", "Nombre de la carpeta del mundo principal.");
            AddSchemaItem("level-seed", "", "", PropertyType.String, PropertyCategory.World, "Semilla del Mundo (Seed)", "Semilla personalizada para la generación del mundo.");
            AddSchemaItem("enable-rcon", "false", "false", PropertyType.Boolean, PropertyCategory.Network, "Habilitar RCON", "Permite acceso de comandos remoto.");
            AddSchemaItem("rcon.port", "25575", "25575", PropertyType.Integer, PropertyCategory.Network, "Puerto RCON", "Puerto para conexiones RCON.");
            AddSchemaItem("rcon.password", "", "", PropertyType.String, PropertyCategory.Network, "Contraseña RCON", "Contraseña requerida para RCON.");
            AddSchemaItem("enable-command-block", "true", "true", PropertyType.Boolean, PropertyCategory.Gameplay, "Habilitar Comandos (Command Blocks)", "Permite la ejecución de bloques de comandos.");
            AddSchemaItem("spawn-monsters", "true", "true", PropertyType.Boolean, PropertyCategory.Gameplay, "Generar Monstruos", "Determina si los monstruos pueden aparecer.");
            AddSchemaItem("spawn-animals", "true", "true", PropertyType.Boolean, PropertyCategory.Gameplay, "Generar Animales", "Determina si los animales pueden aparecer.");
            AddSchemaItem("spawn-npcs", "true", "true", PropertyType.Boolean, PropertyCategory.Gameplay, "Generar Aldeanos (NPCs)", "Determina si los aldeanos pueden aparecer.");
            AddSchemaItem("allow-nether", "true", "true", PropertyType.Boolean, PropertyCategory.World, "Permitir Nether", "Habilita la dimensión del Nether.");
            AddSchemaItem("generate-structures", "true", "true", PropertyType.Boolean, PropertyCategory.World, "Generar Estructuras", "Genera aldeas, templos y fortalezas.");
        }

        private void AddSchemaItem(string key, string value, string defaultValue, PropertyType type, PropertyCategory category, string displayName, string description, List<string>? allowedValues = null)
        {
            _properties[key] = new ServerPropertyItem(key, value, defaultValue, type, category, displayName, description, allowedValues);
        }

        public string GetProperty(string key, string defaultValue = "")
        {
            return _properties.TryGetValue(key, out var item) ? item.Value : defaultValue;
        }

        public void SetProperty(string key, string value)
        {
            if (_properties.TryGetValue(key, out var item))
            {
                item.Value = value;
            }
            else
            {
                _properties[key] = new ServerPropertyItem(key, value, value, PropertyType.String, PropertyCategory.General, key, $"Propiedad personalizada: {key}");
            }
        }

        public void LoadFromString(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            using var reader = new StringReader(content);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.StartsWith("#") || line.StartsWith("!") || !line.Contains('='))
                    continue;

                int index = line.IndexOf('=');
                string key = line.Substring(0, index).Trim();
                string val = line.Substring(index + 1).Trim();

                SetProperty(key, val);
            }
        }

        public void LoadFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                LoadFromString(File.ReadAllText(filePath, Encoding.UTF8));
            }
        }

        public string SaveToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Minecraft server properties");
            sb.AppendLine($"# Generated by Zenith MC Server Launcher - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            foreach (var category in Enum.GetValues<PropertyCategory>())
            {
                var categoryItems = _properties.Values.Where(p => p.Category == category).ToList();
                if (categoryItems.Count == 0) continue;

                sb.AppendLine();
                sb.AppendLine($"# --- Category: {category} ---");
                foreach (var item in categoryItems)
                {
                    sb.AppendLine($"{item.Key}={item.Value}");
                }
            }

            // Custom keys outside standard schema
            var customItems = _properties.Values.Where(p => !Enum.IsDefined(p.Category)).ToList();
            if (customItems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("# --- Custom Properties ---");
                foreach (var item in customItems)
                {
                    sb.AppendLine($"{item.Key}={item.Value}");
                }
            }

            return sb.ToString();
        }

        public void SaveToFile(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath) ?? "";
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, SaveToString(), Encoding.UTF8);
        }
    }
}
