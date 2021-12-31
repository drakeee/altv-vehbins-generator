using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using RageKit;
using RageKit.GameFiles;

namespace AltV.Generator
{
    class Program
    {
		private static readonly string outputPath = "./output_files/";

        static void Main(string[] args)
        {
            string gtaPath = "";
            if (args.Length == 0 || args.Length != 2)
            {
                Console.WriteLine("Arguments: --gta-path [Path to GTA V]");
                return;
            }

            if (args[0] == "--gta-path")
                gtaPath = args[1];
            else return;

            Utils.Log.Info("Check if GTA5.exe exists in path");

            if (!gtaPath.EndsWith(Path.DirectorySeparatorChar))
                gtaPath += Path.DirectorySeparatorChar;

            if (!File.Exists(gtaPath + "GTA5.exe"))
            {
                Utils.Log.Error("GTA5.exe was not found! Make sure you passed the correct path.");
                Utils.Log.Info("If the path contains whitespaces use quotes i.e: --gta-path \"D:\\Games\\Grand Theft Auto V\"");
                return;
            }

            Utils.Log.Info("GTA5.exe was found");

            Utils.Log.Info("Setup decryption keys");
            GTA5Keys.PC_AES_KEY = Keys.gtav_aes_key;
            GTA5Keys.PC_NG_KEYS = CryptoIO.ReadNgKeys(Keys.gtav_ng_key);
            GTA5Keys.PC_NG_DECRYPT_TABLES = CryptoIO.ReadNgTables(Keys.gtav_ng_decrypt_tables);
            GTA5Keys.PC_NG_ENCRYPT_TABLES = CryptoIO.ReadNgTables(Keys.gtav_ng_encrypt_tables);
            GTA5Keys.PC_NG_ENCRYPT_LUTs = CryptoIO.ReadNgLuts(Keys.gtav_ng_encrypt_luts);
            GTA5Keys.PC_LUT = Keys.gtav_hash_lut;

            Utils.Log.Info("Create output directory and copy base json files to it");
            {
                Directory.CreateDirectory(outputPath);
                File.WriteAllBytes(outputPath + "carcols.json", Base.carcols);
                File.WriteAllBytes(outputPath + "carvariations.json", Base.carvariations);
            }

            DirectoryInfo gtaDir = new DirectoryInfo(gtaPath);
            RpfManager rpfManager = new RpfManager();

            Utils.Log.Info("Scanning GTA path for file infos");
            rpfManager.Init(gtaPath, (string status) => { Utils.Log.Status(status); }, (string error) => { }, false, false);

            Utils.Log.Info("Extracting dlclist.xml from RPF file");
            {
                var dlcListPath = gtaPath + "update\\update.rpf\\common\\data\\dlclist.xml";
                var dlcEntry = rpfManager.GetEntry(dlcListPath) as RpfBinaryFileEntry;
                if(dlcEntry == null)
                {
                    Utils.Log.Error($"dlclist.xml not found in path: {dlcListPath}");
                    Utils.Log.Error("Aborting...");
                    return;
				}

                Utils.XmlToJSON(outputPath + "dlclist.json", Encoding.UTF8.GetString(dlcEntry.File.ExtractFile(dlcEntry)).Replace("item", "Item"));
            }

            List<string> filesToSearch = new List<string>{
                "carcols.meta",
                "vehicles.meta",
                "carvariations.meta"
            };

            Regex rx = new Regex(@"\b(dlc_patch|dlcpacks)\\(\w+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            foreach (var fileSearch in filesToSearch)
            {
                Utils.Log.Info($"Extracting \"{fileSearch}\" files for further processing");
                {
                    foreach(var entry in rpfManager.SearchFile(fileSearch))
                    {
                        MatchCollection matches = rx.Matches(entry.Path);
                        string additional = "";

                        if (matches.Count > 0)
                        {
                            GroupCollection gC = matches[0].Groups;
                            additional = gC[0].ToString() + Path.DirectorySeparatorChar;
                        }

                        string fullPath = outputPath + additional + entry.Name;

                        if (entry.Name.EndsWith(".ymt"))
                        {
                            YmtFile ymtFile = rpfManager.GetFile<YmtFile>(entry);
                            var xmlFile = MetaXml.GetXml(ymtFile, out _);

                            Utils.XmlToJSON(fullPath, xmlFile, false);
						} else {
                            //var data = Encoding.UTF8.GetString(entry.File.ExtractFile(entry as RpfBinaryFileEntry));
                            //XDocument doc = XDocument.Parse(data);

                            //hashes.AddRange(doc.DescendantNodes().OfType<XText>().Select(x => x.Value).Distinct().ToList());

                            Utils.XmlToJSON(fullPath, entry.File.ExtractFile(entry as RpfBinaryFileEntry));
						}
					}
                }
            }

            VehicleMods vehicleMods = new VehicleMods(rpfManager, outputPath);
            vehicleMods.GenerateBin(outputPath + "vehmods.bin");

            VehicleModels vehicleModels = new VehicleModels(rpfManager, outputPath);
            vehicleModels.GenerateBin(outputPath + "vehmodels.bin");
        }
    }
}
