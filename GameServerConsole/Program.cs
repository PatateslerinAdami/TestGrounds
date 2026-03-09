using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using GameServerConsole.Properties;
using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServerConsole.Logic;
using LeagueSandbox.GameServerConsole.Utility;
using log4net;

namespace LeagueSandbox.GameServerConsole;

/// <summary>
///     Class representing the program piece, or commandline piece of the server; where everything starts
///     (GameServerConsole -> GameServer, etc).
/// </summary>
internal abstract class Program {
    // So we can print debug info via the command line interface.
    private static readonly ILog Logger = LoggerProvider.GetLogger();

    private static void Main(string[] args) {
        AppDomain.CurrentDomain.UnhandledException +=
            (sender, args) => Logger.Fatal(null, (Exception) args.ExceptionObject);

        // If the command line interface was ran with additional parameters (perhaps via a shortcut or just via another command line)
        // Refer to ArgsOptions for all possible launch parameters
        var parsedArgs = ArgsOptions.Parse(args);
        parsedArgs.GameInfoJson = LoadConfig(
            parsedArgs.GameInfoJsonPath,
            parsedArgs.GameInfoJson,
            Encoding.UTF8.GetString(Resources.GameInfo));

        var gameServerLauncher = new GameServerLauncher(
            parsedArgs.ServerPort,
            parsedArgs.GameInfoJson);

#if DEBUG
        // When debugging, optionally the game client can be launched automatically given the path (placed in GameServerSettings.json) to the folder containing the League executable.
        var configGameServerSettings = GameServerConfig.LoadFromJson(LoadConfig(
                                                                         parsedArgs.GameServerSettingsJsonPath,
                                                                         parsedArgs.GameServerSettingsJson,
                                                                         Encoding.UTF8.GetString(
                                                                             Resources.GameServerSettings)));

        if (configGameServerSettings.AutoStartClient) {
            // TODO: launch a client for each player in config
            var launcherArgs = string.Format(
                "127.0.0.1 {0} {1} 1",
                parsedArgs.ServerPort,
                gameServerLauncher.game.Config.Players.First().BlowfishKey);

            var argsString = $"\"8394\" \"LoLLauncher.exe\" \"\" \"{launcherArgs}\"";

            Process leagueProcess = null;

            var isWindows =
                Environment.OSVersion.Platform == PlatformID.Win32NT      ||
                Environment.OSVersion.Platform == PlatformID.Win32S       ||
                Environment.OSVersion.Platform == PlatformID.Win32Windows ||
                Environment.OSVersion.Platform == PlatformID.WinCE;

            if (isWindows) {
                var leaguePath = GetExpandedPath(configGameServerSettings.ClientLocation);
                if (Directory.Exists(leaguePath)) leaguePath = Path.Combine(leaguePath, "League of Legends.exe");

                if (File.Exists(leaguePath)) {
                    var startInfo = new ProcessStartInfo(leaguePath) {
                        Arguments        = argsString,
                        WorkingDirectory = Path.GetDirectoryName(leaguePath)
                    };

                    leagueProcess = Process.Start(startInfo);

                    Logger.Info("Launching League of Legends. You can disable this in GameServerSettings.json.");

                    WindowsConsoleCloseDetection.SetCloseHandler(_ => {
                        if (leagueProcess != null && !leagueProcess.HasExited) leagueProcess.Kill();
                        return true;
                    });
                } else { 
                    Logger.Warn(
                        "Unable to find League of Legends.exe. Check GameServerSettings.json and your League location.");
                }
            } else {
                var scriptPath = ResolveLaunchScriptPath(configGameServerSettings);
                if (string.IsNullOrWhiteSpace(scriptPath)) {
                    Logger.Warn(
                        "No launch script path configured. Set clientLaunchScriptPath in GameServerSettings.json.");
                } else if (File.Exists(scriptPath)) {
                    var startInfo = new ProcessStartInfo("/bin/bash") {
                        UseShellExecute = false
                    };

                    // Pass the same launch args to the script. The script should forward "$@" to wine.
                    startInfo.ArgumentList.Add(scriptPath);
                    startInfo.ArgumentList.Add("8394");
                    startInfo.ArgumentList.Add("LoLLauncher.exe");
                    startInfo.ArgumentList.Add(string.Empty);
                    startInfo.ArgumentList.Add(launcherArgs);

                    var scriptDirectory = Path.GetDirectoryName(scriptPath);
                    if (!string.IsNullOrWhiteSpace(scriptDirectory)) startInfo.WorkingDirectory = scriptDirectory;

                    var clientLocation = GetExpandedPath(configGameServerSettings.ClientLocation);
                    if (!string.IsNullOrWhiteSpace(clientLocation))
                        startInfo.Environment["LEAGUE_GAME_DIR"] = clientLocation;

                    var winePrefix = GetExpandedPath(configGameServerSettings.WinePrefix);
                    if (!string.IsNullOrWhiteSpace(winePrefix))
                        startInfo.Environment["WINEPREFIX"] = winePrefix;

                    leagueProcess = Process.Start(startInfo);

                    Logger.Info(
                        "Launching League of Legends via configured script. You can disable this in GameServerSettings.json.");
                } else { Logger.Warn($"Unable to find Linux launch script at: {scriptPath}"); }
            }
        } else { Logger.Info("Server is ready, clients can now connect."); }
#endif
        // This is where the actual GameServer starts.
        gameServerLauncher.StartNetworkLoop();
    }

    /// <summary>
    ///     Used to parse any of the configuration files used for the GameServer, ex: GameInfo.json or GameServerSettings.json.
    /// </summary>
    /// <param name="filePath">Full path to the configuration file.</param>
    /// <param name="currentJsonString">String representing the content of the configuration file. Usually empty.</param>
    /// <param name="defaultJsonString">
    ///     String representing the default content of the configuration file. Usually what is
    ///     already defined in the respective configuration file.
    /// </param>
    /// <returns>The string defined in the configuration file or defined via launch arguments.</returns>
    private static string LoadConfig(string filePath, string currentJsonString, string defaultJsonString) {
        if (!string.IsNullOrEmpty(currentJsonString))
            return currentJsonString;

        try {
            if (File.Exists(filePath))
                return File.ReadAllText(filePath);

            var settingsDirectory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(settingsDirectory))
                throw new Exception($"Creating Config File failed. Invalid Path: {filePath}");

            Directory.CreateDirectory(settingsDirectory);

            File.WriteAllText(filePath, defaultJsonString);
        } catch (Exception e) { Logger.Error(null, e); }

        return defaultJsonString;
    }

    private static string GetExpandedPath(string path) {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (path.StartsWith("~/") || path.StartsWith("~\\"))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Substring(2));

        return path;
    }

    private static string ResolveLaunchScriptPath(GameServerConfig gameServerSettings) {
        var configuredPath = GetExpandedPath(gameServerSettings.ClientLaunchScriptPath);
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        var fallbackPath = GetExpandedPath(gameServerSettings.ClientLocation);
        if (!string.IsNullOrWhiteSpace(fallbackPath) &&
            string.Equals(Path.GetExtension(fallbackPath), ".sh", StringComparison.OrdinalIgnoreCase))
            return fallbackPath;

        return string.Empty;
    }
}

/// <summary>
///     Class housing launch arguments and their parsing used for the GameServerConsole.
/// </summary>
public class ArgsOptions {
    [Option("config", Default = "Settings/GameInfo.json")]
    public string GameInfoJsonPath { get; set; }

    [Option("config-gameserver", Default = "Settings/GameServerSettings.json")]
    public string GameServerSettingsJsonPath { get; set; }

    [Option("config-json", Default = "")] public string GameInfoJson { get; set; }

    [Option("config-gameserver-json", Default = "")]
    public string GameServerSettingsJson { get; set; }

    [Option("port", Default = (ushort) 5119)]
    public ushort ServerPort { get; set; }

    public static ArgsOptions Parse(string[] args) {
        ArgsOptions options = null;
        Parser.Default.ParseArguments<ArgsOptions>(args).WithParsed(argOptions => options = argOptions);
        return options;
    }
}
