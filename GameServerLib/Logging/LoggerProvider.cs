using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Repository;

namespace LeagueSandbox.GameServer.Logging
{
    /// <summary>
    /// Class which creates logger instances.
    /// </summary>
    public static class LoggerProvider
    {
        /// <summary>
        /// Provider instance which configures log4net to prepare for getting a logger instance.
        /// </summary>
        static LoggerProvider()
        {
            try
            {
                string[] possibleConfigFiles = {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App.config"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.xml"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "log4net.config")
                };

                FileInfo? configFile = null;
                foreach (var path in possibleConfigFiles)
                {
                    if (File.Exists(path))
                    {
                        configFile = new FileInfo(path);
                        Console.WriteLine($"Found log4net config at: {path}");
                        break;
                    }
                }

                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                if (assembly != null)
                {
                    var rep = LogManager.GetRepository(assembly);

                    if (configFile != null && configFile.Exists)
                    {
                        log4net.Config.XmlConfigurator.Configure(rep, configFile);
                        Console.WriteLine("log4net configured successfully from file");
                    }
                    else
                    {
                        var layout = new log4net.Layout.PatternLayout("%-4timestamp [%thread] %-5level %logger - %message%newline");
                        var appender = new log4net.Appender.ConsoleAppender
                        {
                            Layout = layout
                        };
                        layout.ActivateOptions();
                        appender.ActivateOptions();

                        log4net.Config.BasicConfigurator.Configure(rep, appender);
                        Console.WriteLine("log4net configured with basic configuration (no config file found)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to configure log4net: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Gets a logger instance specific to the caller.
        /// </summary>
        /// <returns>Logger designated to the specific caller.</returns>
        public static ILog GetLogger()
        {
            try
            {
                var stackTrace = new StackTrace();
                var frame = stackTrace.GetFrame(1);
                var method = frame?.GetMethod();
                var declaringType = method?.DeclaringType;

                if (declaringType == null)
                {
                    declaringType = typeof(LoggerProvider);
                }

                return LogManager.GetLogger(declaringType);
            }
            catch (Exception)
            {
                return LogManager.GetLogger(typeof(LoggerProvider));
            }
        }

        /// <summary>
        /// Gets a logger instance for a specific type.
        /// </summary>
        /// <param name="type">The type to create a logger for.</param>
        /// <returns>Logger designated to the specific type.</returns>
        public static ILog GetLogger(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return LogManager.GetLogger(type);
        }

        /// <summary>
        /// Gets a logger instance for a specific name.
        /// </summary>
        /// <param name="name">The name to create a logger for.</param>
        /// <returns>Logger designated to the specific name.</returns>
        public static ILog GetLogger(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            return LogManager.GetLogger(name);
        }
    }
}