using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;

namespace MfilesObjectImporter
{
    public class Logger
    {
        String LogFIlePath = "";
        String SkippedFileLogPath = "";
        Dictionary<string, int> logLevel;
        int ConfigLevel = 1;
        public Logger()
        {
            string tempPath = ConfigurationManager.AppSettings["LogFIlePath"];
            string datetimeStrEnding = (@"Log_" + DateTime.Now).Replace("/", "-").Replace(":", ".") + ".txt";
            this.LogFIlePath = tempPath.TrimEnd(new[] { '\\' }) + @"\Process"+datetimeStrEnding;
            this.SkippedFileLogPath = tempPath.TrimEnd(new[] { '\\' }) + @"\SkippedFiles" + datetimeStrEnding;
            try
            {
                this.ConfigLevel = getLogLevel(ConfigurationManager.AppSettings["LogLevel"]);
            }
            catch (Exception ex)
            {
                this.ConfigLevel = 1;
            }
            checkDirectory(LogFIlePath);
        }

        public void LogToFile(String text, string level)
        {
            if (getLogLevel(level) <= ConfigLevel)
            {
                Console.WriteLine(text);
                using (var tw = new StreamWriter(LogFIlePath, true))
                {
                    tw.WriteLine(text);
                }
            }
        }

        public void LogToSkippedFile(String text, string level)
        {
            if (getLogLevel(level) <= ConfigLevel)
            {
                Console.WriteLine("Adding file to skipped files logs" + text);
            }
                using (var tw = new StreamWriter(SkippedFileLogPath, true))
                {
                    tw.WriteLine(text);
                }
            
        }

        private void checkDirectory(string directoryToCheck)
        {
            LogToFile("Creating Log directory if it doesn't Exists", "Debug");
            if (!Directory.Exists(Path.GetDirectoryName(directoryToCheck)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(directoryToCheck));
            }
        }

        private int getLogLevel(string level)
        {
            int lev = 1;
            if (level.ToUpper() == "DEBUG")
            {
                lev = 2;
            }
            return lev;        
        }


    }
}
