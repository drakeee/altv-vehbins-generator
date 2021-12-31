using Newtonsoft.Json;
using RageKit.GameFiles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;

namespace AltV.Generator
{
	static class Utils
	{
        public static class Log
        {
            private static void ClearCurrentConsoleLine()
            {
                int currentLineCursor = Console.CursorTop;
                Console.SetCursorPosition(0, Console.CursorTop);
                for (int i = 0; i < Console.WindowWidth; i++)
                    Console.Write(" ");
                Console.SetCursorPosition(0, currentLineCursor);
            }

            private static void Print(string message, ConsoleColor color = ConsoleColor.Blue, bool newLine = true)
            {
                if(!newLine)
                {
                    Console.Title = message;
                    return;
                }

                StackTrace stackTrace = (new System.Diagnostics.StackTrace());
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"{stackTrace.GetFrame(2).GetMethod().DeclaringType}");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("] ");
                Console.ForegroundColor = color;
                Console.Write($"{stackTrace.GetFrame(1).GetMethod().Name.ToUpper()} ");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.White;

                Console.WriteLine(message);

                Console.Title = message;
                Console.ResetColor();
            }

            public static void Info(string message) => Print(message);
            public static void Error(string message) => Print(message, ConsoleColor.Red);
            public static void Debug(string message) => Print(message, ConsoleColor.Green);
            public static void Warning(string message) => Print(message, ConsoleColor.Yellow);
            public static void Status(string message) => Print(message, ConsoleColor.Gray, false);
        }

        public static List<RpfEntry> SearchFile(this RpfManager manager, string search)
        {
            return manager.EntryDict.Where(x => x.Value.Name.StartsWith(search)).Select(x => x.Value).ToList();
        }

        public static void XmlToJSON(string path, byte[] data, bool changeExtension = true)
        {
            XmlToJSON(path, Encoding.UTF8.GetString(data));
		}

        public static void XmlToJSON(string path, string data, bool changeExtension = true)
        {
            string directory = Path.GetDirectoryName(path);
            
            if(changeExtension)
                path = Path.ChangeExtension(path, ".json");

            Directory.CreateDirectory(directory);

            File.WriteAllText(path, XmlToJSON(data));
		}

        public static string XmlToJSON(string data)
        {
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(data);

            return JsonConvert.SerializeXmlNode(xml, Newtonsoft.Json.Formatting.Indented);
		}

        public static List<RpfEntry> SearchFiles(RpfFile rpfFile, List<string> filesToSearch)
        {
            List<RpfEntry> filesList = new List<RpfEntry>();
            foreach (var child in rpfFile.Children)
            {
                filesList.AddRange(SearchFiles(child, filesToSearch));
            }

            foreach (var entry in rpfFile.AllEntries)
            {
                foreach (var search in filesToSearch)
                    if (entry.Name.StartsWith(search))
                        filesList.Add(entry);
            }

            return filesList;
        }

        public static List<RpfEntry> SearchFiles(RpfFile rpfFile, string fileName)
        {
            List<RpfEntry> filesList = new List<RpfEntry>();
            foreach (var child in rpfFile.Children)
            {
                filesList.AddRange(SearchFiles(child, fileName));
            }

            foreach (var entry in rpfFile.AllEntries)
            {
                if (entry.Name.StartsWith(fileName))
                    filesList.Add(entry);
            }

            return filesList;
        }
    }
}
