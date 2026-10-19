using System.IO;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.Common;

public sealed class LocalFileLoggerProvider(string path) : ILoggerProvider
{
    private readonly object gate = new();
    public ILogger CreateLogger(string categoryName) => new LocalLogger(this, categoryName);
    public void Dispose() { }

    private void Write(string line)
    {
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
                    File.Move(path, path + ".previous", overwrite: true);
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine("No se pudo escribir el log local: " + ex.GetType().Name);
            }
        }
    }

    private sealed class LocalLogger(LocalFileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                owner.Write($"{DateTimeOffset.Now:O} {logLevel} {category}: {formatter(state, exception)} {exception?.GetType().Name}");
        }
    }
}
