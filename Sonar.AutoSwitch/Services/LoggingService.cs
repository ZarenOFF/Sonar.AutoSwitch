using Serilog;
using System;
using System.IO;

namespace Sonar.AutoSwitch.Services;

public static class LoggingService
{
    private static ILogger? _loggerInstance;
    private static string _logFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sonar.AutoSwitch", "sonar_autoswitch_advanced.log"
    );

    private static bool _enabled = false;

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            ConfigureLogger();
        }
    }

    public static void ConfigureLogger()
    {
        if (_enabled)
        {
            Log.CloseAndFlush();
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);

            _loggerInstance = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Async(a => a.File(
                    _logFilePath,
                    fileSizeLimitBytes: 10_000_000_000,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 5,
                    rollingInterval: RollingInterval.Infinite,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                ))
                .CreateLogger();

            Log.Logger = _loggerInstance;
        }
        else
        {
            Log.CloseAndFlush();
            _loggerInstance = null;
            Log.Logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
        }
    }

    public static void LogDebug(string msg) => _loggerInstance?.Debug(msg);
    public static void LogError(string msg, Exception? ex = null) => _loggerInstance?.Error(ex, msg);
    public static void LogInfo(string msg) => _loggerInstance?.Information(msg);
}