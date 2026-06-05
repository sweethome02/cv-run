using System.IO;

namespace ClipNestWpf;

public static class Logger
{
    static readonly string _dir;
    static readonly object _lock = new();

    static Logger()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(_dir);
    }

    static string LogPath => Path.Combine(_dir, $"clipnest-{DateTime.Now:yyyy-MM-dd}.log");
    static string ErrorLogPath => Path.Combine(_dir, $"clipnest-error-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string category, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{category}] {message}";
        lock (_lock)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        System.Diagnostics.Debug.WriteLine(line);
    }

    public static void Error(string category, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{category}] ERROR: {message}";
        lock (_lock)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
            File.AppendAllText(ErrorLogPath, line + Environment.NewLine);
        }
        System.Diagnostics.Debug.WriteLine(line);
    }

    public static void CleanupOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-7);
            lock (_lock)
            {
                foreach (var file in Directory.EnumerateFiles(_dir, "clipnest-*.log"))
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                        File.Delete(file);
                }
            }
        }
        catch { }
    }
}
