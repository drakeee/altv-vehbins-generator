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
            //List<string> hashes = new List<string>();
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

            SortedDictionary<string, int> vehicleMap = new SortedDictionary<string, int>();
            Utils.Log.Info("Extracting all the vehicles name from vehicles.json files");
            {
                Regex modelRx = new Regex("\"modelName\": *\"(.*)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                DirectoryInfo outputDir = new DirectoryInfo(Environment.CurrentDirectory + outputPath);
                foreach (var fileInfo in outputDir.GetFiles("vehicles.json", SearchOption.AllDirectories))
                {
                    string data = File.ReadAllText(fileInfo.FullName);
                    var matches = modelRx.Matches(data);

                    foreach (Match match in matches)
                    {
                        var groups = match.Groups;
                        vehicleMap[groups[1].ToString().ToLower()] = -1;
                    }
                }
            }
            Utils.Log.Info($"Collected {vehicleMap.Count} vehicles");

            Utils.Log.Info("Extracting vehicles wheels number from yft files");
            {
                var vehicles = rpfManager.AllRpfs.Where(x => x.Path.Contains("vehicles")).SelectMany(x => x.AllEntries).Where(x => vehicleMap.Any(check => x.Name.StartsWith(check.Key.ToLower() + ".yft"))).GroupBy(x => x.Name).Select(x => x.First()).ToList();
                if(vehicles.Count != vehicleMap.Count)
                {
                    Utils.Log.Error("An error occured while looking for vehicles yft file, looks like didn't catch all of them");
                    Utils.Log.Error($"Collected {vehicleMap.Count} vehicles from vehicles.meta but found only {vehicles.Count} models");
                    Utils.Log.Error("Aborting...");
                    return;
                }

                foreach (RpfResourceFileEntry vehicle in vehicles)
                {
                    YftFile veh = new YftFile();
                    veh.Load(vehicle.File.ExtractFile(vehicle), vehicle);

                    vehicleMap[vehicle.GetShortNameLower()] = veh.Fragment.Drawable.Skeleton.Bones.Items.Where(x => x.Name.Contains("wheel_")).ToList().Count;
                    veh = null;

                    var vehicleCount = vehicleMap.Count;
                    var vehicleDone = vehicleMap.Where(x => x.Value > -1).ToList().Count;
                    var percentage = (uint)(((vehicleDone * 1.0) / (vehicleCount * 1.0)) * 100.0);
                    Utils.Log.Status($"Processing vehicles wheels number: {vehicleDone}/{vehicleCount} - {percentage}% - {vehicle.GetShortNameLower()}");
                }

                Utils.Log.Info("Saving vehicles list and wheels count to \"vehicleList.json\"");
                File.WriteAllText(outputPath + "vehicleList.json", JsonConvert.SerializeObject(vehicleMap, Newtonsoft.Json.Formatting.Indented));
			}

            VehicleMods.GenerateBin(outputPath, outputPath + "vehmods.bin");
        }
    }
}
