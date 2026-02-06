using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LeagueSandbox.GameServer
{
    /// <summary>
    /// Class which houses the build information of the currently running build of the Server.
    /// </summary>
    public static class ServerContext
    {
        public static string ExecutingDirectory => Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location
        );

        public static string BuildDateString
        {
            get
            {
                // Get the location of the currently running assembly (your .dll or .exe)
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;

                // Get the last time that file was written to (i.e., the build time)
                DateTime buildDate = File.GetLastWriteTimeUtc(assemblyLocation);

                // Return it as a nicely formatted string (ISO 8601 format)
                return buildDate.ToString("O");
            }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public class BuildDateTimeAttribute : Attribute
    {
        public string Date { get; private set; }
        public BuildDateTimeAttribute(string date)
        {
            Date = date;
        }
    }
}
