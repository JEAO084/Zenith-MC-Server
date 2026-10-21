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

        public event Action<string>? OutputReceived;
        public event Action<int>? ProcessExited;

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
                OutputReceived?.Invoke("[SUGERENCIA ZENITH] Tu servidor requiere una versión de Java superior a la configurada. Por favor actualiza a Java 21 o Java 25 en Ajustes.");
            }
            else if (line.Contains("Access to the path", StringComparison.OrdinalIgnoreCase) && line.Contains("denied", StringComparison.OrdinalIgnoreCase))
            {
                OutputReceived?.Invoke("[SUGERENCIA ZENITH] Permiso denegado al acceder a Java. Cambia la ruta de ejecuciones de Java en la pestaña de Ajustes a una carpeta fuera de AppX.");
            }
        }

        public void SendCommand(string command)
        {
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
