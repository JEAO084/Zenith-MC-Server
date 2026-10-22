using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Zenith.Core.Services
{
    public class ServerProcessManager
    {
        private Process? _process;
        public bool IsRunning => _process != null && !_process.HasExited;

        private readonly HashSet<string> _dynamicServerCommands = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lockObj = new();

        public event Action<string>? OutputReceived;
        public event Action<int>? ProcessExited;

        public void RegisterDynamicCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            string clean = command.Trim().TrimStart('/');
            if (clean.Length > 0)
            {
                lock (_lockObj)
                {
                    _dynamicServerCommands.Add(clean);
                }
            }
        }

        public List<string> GetDynamicSuggestions(string query)
        {
            lock (_lockObj)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return _dynamicServerCommands.Take(10).ToList();
                }

                string q = query.Trim().TrimStart('/').ToLowerInvariant();
                return _dynamicServerCommands
                    .Where(c => c.ToLowerInvariant().StartsWith(q) || c.ToLowerInvariant().Contains(q))
                    .OrderBy(c => c.Length)
                    .Take(10)
                    .ToList();
            }
        }

        public bool StartServer(string javaExecutablePath, string serverDirectory, string jarFileName, int ramGb = 4, string customJvmArgs = "")
        {
            if (IsRunning) return false;

            string jarPath = Path.Combine(serverDirectory, jarFileName);
            if (!File.Exists(jarPath))
            {
                throw new FileNotFoundException($"El archivo JAR del servidor no fue encontrado en: {jarPath}");
            }

            string javaPath = string.IsNullOrWhiteSpace(javaExecutablePath) ? "java" : javaExecutablePath;

            var startInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = $"-Xms{ramGb}G -Xmx{ramGb}G {customJvmArgs} -jar \"{jarFileName}\" nogui",
                WorkingDirectory = serverDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    OutputReceived?.Invoke(e.Data);
                    AnalyzeConsoleLine(e.Data);
                }
            };

            _process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    OutputReceived?.Invoke($"[ERROR] {e.Data}");
                    AnalyzeConsoleLine(e.Data);
                }
            };

            _process.Exited += (s, e) =>
            {
                int exitCode = _process.ExitCode;
                ProcessExited?.Invoke(exitCode);
            };

            bool started = _process.Start();
            if (started)
            {
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }

            return started;
        }

        private void AnalyzeConsoleLine(string line)
        {
            if (line.Contains("UnsupportedClassVersionError", StringComparison.OrdinalIgnoreCase))
            {
                OutputReceived?.Invoke("[SUGERENCIA ZENITH] Tu servidor requiere una versión de Java superior a la configurada. Por favor actualiza la versión de Java en Ajustes.");
            }
            else if (line.Contains("Access to the path", StringComparison.OrdinalIgnoreCase) && line.Contains("denied", StringComparison.OrdinalIgnoreCase))
            {
                OutputReceived?.Invoke("[SUGERENCIA ZENITH] Permiso denegado al acceder a Java. Cambia la ruta de ejecuciones de Java en la pestaña de Ajustes a una carpeta fuera de AppX.");
            }

            // Extract dynamic commands from server logs (e.g. Registered command: /help, or Command [/op], etc.)
            ExtractDynamicCommandsFromLog(line);
        }

        private void ExtractDynamicCommandsFromLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            // Detect commands mentioned in log output (e.g., "/help", "/stop", "/gamemode", "/op", etc.)
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (word.StartsWith("/") && word.Length > 1)
                {
                    string cmdCandidate = word.TrimStart('/').TrimEnd('.', ':', ',', '!', '?', ')', '(');
                    if (cmdCandidate.Length > 0 && !cmdCandidate.Contains('/'))
                    {
                        RegisterDynamicCommand(cmdCandidate);
                    }
                }
            }
        }

        public void SendCommand(string command)
        {
            if (!string.IsNullOrWhiteSpace(command))
            {
                RegisterDynamicCommand(command);
            }

            if (IsRunning && _process != null && !_process.HasExited)
            {
                _process.StandardInput.WriteLine(command);
                _process.StandardInput.Flush();
                OutputReceived?.Invoke($"> {command}");
            }
        }

        public async Task StopServerAsync(int timeoutMs = 10000)
        {
            if (!IsRunning) return;

            SendCommand("stop");

            await Task.Run(() =>
            {
                if (_process != null && !_process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        _process.Kill();
                    }
                    catch { }
                }
            });
        }
    }
}
