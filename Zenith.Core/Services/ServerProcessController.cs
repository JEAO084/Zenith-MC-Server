using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class ServerProcessController
    {
        private Process? _process;
        private ServerInstance? _currentServer;

        public event Action<string>? OutputDataReceived;
        public event Action<string>? ErrorDataReceived;
        public event Action<ServerStatus>? StatusChanged;

        public ServerInstance? CurrentServer => _currentServer;
        public bool IsRunning => _process != null && !_process.HasExited;

        public async Task<bool> StartServerAsync(ServerInstance server, string javaExecutablePath)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("A server is already running in this process controller.");
            }

            _currentServer = server;
            _currentServer.Status = ServerStatus.Starting;
            StatusChanged?.Invoke(ServerStatus.Starting);

            string jarPath = server.ExecutableJarPath;
            if (string.IsNullOrWhiteSpace(jarPath) || !File.Exists(jarPath))
            {
                // Ensure server directory exists and create dummy executable jar if missing
                Directory.CreateDirectory(server.ServerPath);
                jarPath = Path.Combine(server.ServerPath, "server.jar");
                if (!File.Exists(jarPath))
                {
                    await File.WriteAllTextAsync(jarPath, "DUMMY SERVER JAR CONTENT");
                }
                server.ExecutableJarPath = jarPath;
            }

            string javaExe = string.IsNullOrWhiteSpace(javaExecutablePath) ? "java" : javaExecutablePath;

            var startInfo = new ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = $"-Xms{server.MemoryMinMb}M -Xmx{server.MemoryMaxMb}M {server.JvmArguments} -jar \"{server.ExecutableJarPath}\" nogui",
                WorkingDirectory = server.ServerPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    OutputDataReceived?.Invoke(e.Data);
                }
            };

            _process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    ErrorDataReceived?.Invoke(e.Data);
                }
            };

            _process.Exited += (s, e) =>
            {
                if (_currentServer != null)
                {
                    _currentServer.Status = ServerStatus.Stopped;
                }
                StatusChanged?.Invoke(ServerStatus.Stopped);
            };

            try
            {
                bool started = _process.Start();
                if (started)
                {
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                    _currentServer.Status = ServerStatus.Running;
                    _currentServer.LastStartedAt = DateTime.UtcNow;
                    StatusChanged?.Invoke(ServerStatus.Running);
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorDataReceived?.Invoke($"[Zenith Engine Error] Failed to launch process: {ex.Message}");
                _currentServer.Status = ServerStatus.Error;
                StatusChanged?.Invoke(ServerStatus.Error);
            }

            return false;
        }

        public void SendCommand(string command)
        {
            if (IsRunning && _process != null)
            {
                _process.StandardInput.WriteLine(command);
                _process.StandardInput.Flush();
            }
        }

        public async Task StopServerAsync()
        {
            if (!IsRunning || _process == null) return;

            if (_currentServer != null)
            {
                _currentServer.Status = ServerStatus.Stopping;
                StatusChanged?.Invoke(ServerStatus.Stopping);
            }

            SendCommand("stop");

            int waitMs = 0;
            while (IsRunning && waitMs < 10000)
            {
                await Task.Delay(200);
                waitMs += 200;
            }

            if (IsRunning)
            {
                ForceKillServer();
            }
        }

        public async Task RestartServerAsync(string javaExecutablePath)
        {
            var server = _currentServer;
            if (server == null) return;

            await StopServerAsync();
            await StartServerAsync(server, javaExecutablePath);
        }

        public void ForceKillServer()
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Fallback single kill
                    _process.Kill();
                }
            }

            if (_currentServer != null)
            {
                _currentServer.Status = ServerStatus.Stopped;
                StatusChanged?.Invoke(ServerStatus.Stopped);
            }
        }
    }
}
