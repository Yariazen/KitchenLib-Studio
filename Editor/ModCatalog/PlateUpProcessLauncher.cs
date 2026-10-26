using System;
using System.Diagnostics;
using System.IO;

namespace KitchenLib.Studio.Editor
{
    public interface IPlateUpProcess : IDisposable
    {
        bool HasExited { get; }
        int ExitCode { get; }
        void Terminate();
    }

    public interface IPlateUpProcessLauncher
    {
        IPlateUpProcess Launch(string executablePath);
    }

    public sealed class PlateUpProcessLauncher : IPlateUpProcessLauncher
    {
        public IPlateUpProcess Launch(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                throw new FileNotFoundException("The PlateUp executable was not found.", executablePath);
            var start = new ProcessStartInfo(executablePath)
            {
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true
            };
            Process process = Process.Start(start) ?? throw new InvalidOperationException("PlateUp did not start.");
            return new PlateUpProcess(process);
        }

        private sealed class PlateUpProcess : IPlateUpProcess
        {
            private readonly Process _process;
            public PlateUpProcess(Process process) { _process = process; }
            public bool HasExited => _process.HasExited;
            public int ExitCode => _process.ExitCode;
            public void Terminate() { if (!_process.HasExited) _process.Kill(); }
            public void Dispose() { _process.Dispose(); }
        }
    }
}
