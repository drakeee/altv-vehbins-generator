using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RageKit.GameFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AltV.Generator
{
	class VehicleModels : Generator
	{
        private static readonly ushort FILE_VERSION = 2;

        enum VehicleType
        {
            INVALID,
		    PED,
		    CAR,
		    PLANE,
		    TRAILER,
		    QUADBIKE,
		    SUBMARINECAR,
		    AMPHIBIOUS_AUTOMOBILE,
		    AMPHIBIOUS_QUADBIKE,
		    HELI,
		    BLIMP,
		    AUTOGYRO,
		    BIKE,
		    BICYCLE,
		    BOAT,
		    TRAIN,
		    SUBMARINE,
		    OBJECT
        };

        class VehicleInfo
        {
            public UInt32 Hash;
            public VehicleType Type = VehicleType.INVALID;
            public int WheelsCount = -1;
            public bool HasArmoredWindows = false;
            public int PrimaryColor = -1;
            public int SecondaryColor = -1;
            public int PearlColor = -1;
            public int WheelsColor = -1;
            public int InteriorColor = -1;
            public int DashboardColor = -1;
            public int[] ModKits = new int[2]{ 0xFFFF, 0xFFFF };
            public int Extras = 0;
            public int DefaultExtras = 0;
        }

        private RpfManager rpfManager = null;
        private List<string> vehicleList = new List<string>();

        public VehicleModels(RpfManager rpfManager, string sourcePath) : base(sourcePath)
        {
            this.rpfManager = rpfManager;

            if(this.rpfManager != null)
            {
                this.vehicleList = this.GetVehicleList();
                this.vehicleList.Sort();

                File.WriteAllText(sourcePath + "vehicleList.json", JsonConvert.SerializeObject(this.vehicleList, Newtonsoft.Json.Formatting.Indented));
            }
        }

        private List<string> GetVehicleList()
        {
            //SortedDictionary<string, int> vehicleMap = new SortedDictionary<string, int>();
            List<string> tempList = new List<string>();
            Utils.Log.Info("Extracting all the vehicles name from vehicles.json files");
            {
                Regex modelRx = new Regex("\"modelName\": *\"(.*)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                DirectoryInfo outputDir = new DirectoryInfo(Environment.CurrentDirectory + this.sourcePath);
                foreach (var fileInfo in outputDir.GetFiles("vehicles.json", SearchOption.AllDirectories))
                {
                    string data = File.ReadAllText(fileInfo.FullName);
                    var matches = modelRx.Matches(data);

                    foreach (Match match in matches)
                    {
                        var groups = match.Groups;
                        //vehicles.Add(groups[1].ToString().ToLower());
                        tempList.Add(groups[1].ToString().ToLower());
                        //vehicleMap[groups[1].ToString().ToLower()] = -1;
                    }
                }
            }

            tempList = tempList.Distinct().ToList();
            Utils.Log.Info($"Collected {tempList.Count} vehicles");

            return tempList;
        }

        private SortedDictionary<string, int> GetVehicleWheels()
        {
            var result = new SortedDictionary<string,int>();
            Utils.Log.Info("Extracting vehicles wheels number from yft files");
            {
                var vehicles = rpfManager.AllRpfs.Where(x => x.Path.Contains("vehicles")).SelectMany(x => x.AllEntries).Where(x => vehicleList.Any(check => x.Name.StartsWith(check.ToLower() + ".yft"))).GroupBy(x => x.Name).Select(x => x.First()).ToList();
                if (vehicles.Count != vehicleList.Count)
                {
                    Utils.Log.Error("An error occured while looking for vehicles yft file, looks like didn't catch all of them");
                    Utils.Log.Error($"Collected {vehicleList.Count} vehicles from vehicles.meta but found only {vehicles.Count} models");
                    Utils.Log.Error("Aborting...");
                    return result;
                }

                foreach (RpfResourceFileEntry vehicle in vehicles)
                {
                    YftFile veh = new YftFile();
                    veh.Load(vehicle.File.ExtractFile(vehicle), vehicle);

                    result[vehicle.GetShortNameLower()] = veh.Fragment.Drawable.Skeleton.Bones.Items.Where(x => x.Name.Contains("wheel_")).ToList().Count;
                    veh = null;

                    var vehicleCount = this.vehicleList.Count;
                    var vehicleDone = result.Where(x => x.Value > -1).ToList().Count;
                    var percentage = (uint)(((vehicleDone * 1.0) / (vehicleCount * 1.0)) * 100.0);
                    Utils.Log.Status($"Processing vehicles wheels number: {vehicleDone}/{vehicleCount} - {percentage}% - {vehicle.GetShortNameLower()}");
                }

                //Utils.Log.Info("Saving vehicles list and wheels count to \"vehicleList.json\"");
                //File.WriteAllText(sourcePath + "vehicleList.json", JsonConvert.SerializeObject(vehicleMap, Newtonsoft.Json.Formatting.Indented));
            }

            return result;
        }

        private SortedDictionary<string, VehicleInfo> ParseCarVariation()
        {
            SortedDictionary<string, VehicleInfo> tempMap = new SortedDictionary<string, VehicleInfo>();
            SortedDictionary<string, int> modKits = ParseModKits();

            List<string> dlcList = GetDLC();
            if (dlcList.Count == 0)
            {
                return tempMap;
            }

            foreach (var dlc in dlcList)
            {
                string dlcPath = dlc + Path.DirectorySeparatorChar;
                string filePath = sourcePath + dlcPath + "carvariations.json";

                if (!File.Exists(filePath))
                    continue;

                Utils.Log.Info($"Extracting carvariation: {filePath}");
                JObject carvariation = (JObject)JsonConvert.DeserializeObject(File.ReadAllText(filePath));
                var carvariationList = carvariation["CVehicleModelInfoVariation"]["variationData"]["Item"];
                carvariationList = (carvariationList is JObject) ? new JArray((JObject)carvariationList) : carvariationList;

                Regex indicesRegex = new Regex(@"\b(\d+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                foreach (var variation in carvariationList)
                {
                    var modelName = variation["modelName"].ToString().ToLower();

                    var indicesList = variation["colors"];
                    indicesList = indicesList is JArray ? indicesList[0]["Item"] : indicesList["Item"];
                    indicesList = (indicesList is JObject) ? new JArray((JObject)indicesList) : indicesList;

                    MatchCollection indicesMatch = indicesRegex.Matches(indicesList[0]["indices"].ToString());
                    var list = indicesMatch.Cast<Match>().Select(match => Convert.ToInt32(match.Value)).ToList();

                    var kitList = variation["kits"];
                    kitList = (kitList.Type == JTokenType.Null) ? (JObject.Parse(@"{""Item"":""""}")) : kitList;
                    kitList = kitList["Item"].Type == JTokenType.String ? new JArray(kitList["Item"]) : kitList["Item"];

                    var paddingArray = new int[2] { 0xFFFF, 0xFFFF };
                    VehicleInfo tempInfo = new VehicleInfo
                    {
                        Hash = JenkHash.GenHash(modelName),
                        PrimaryColor = list.ElementAtOrDefault(0),
                        SecondaryColor = list.ElementAtOrDefault(1),
                        PearlColor = list.ElementAtOrDefault(2),
                        WheelsColor = list.ElementAtOrDefault(3),
                        InteriorColor = list.ElementAtOrDefault(4),
                        DashboardColor = list.ElementAtOrDefault(5),
                        ModKits = kitList.Select(x => {
                            if (!modKits.ContainsKey(x.ToString()))
                                return 0xFFFF;

                            return modKits[x.ToString()];
                        }).Concat(paddingArray).Take(2).ToArray()
                    };

                    tempMap[modelName] = tempInfo;
                }
            }

            return tempMap;
        }

        private SortedDictionary<string, VehicleInfo> ParseVehicleMeta()
        {
            SortedDictionary<string, VehicleInfo> tempMap = new SortedDictionary<string, VehicleInfo>();

            List<string> dlcList = GetDLC();
            if (dlcList.Count == 0)
            {
                return tempMap;
            }

            foreach (var dlc in dlcList)
            {
                string dlcPath = dlc + Path.DirectorySeparatorChar;
                string filePath = sourcePath + dlcPath + "vehicles.json";

                if (!File.Exists(filePath))
                    continue;

                Utils.Log.Info($"Extracting vehicles: {filePath}");
                JObject vehicles = (JObject)JsonConvert.DeserializeObject(File.ReadAllText(filePath));
                var vehiclesList = vehicles["CVehicleModelInfo__InitDataList"]["InitDatas"]["Item"];
                vehiclesList = (vehiclesList is JObject) ? new JArray((JObject)vehiclesList) : vehiclesList;

                foreach (var vehicle in vehiclesList)
                {
                    var modelName = vehicle["modelName"].ToString().ToLower();
                    var hasArmoredWindow = vehicle["flags"].ToString().Contains("FLAG_HAS_BULLETPROOF_GLASS");
                    VehicleType vehicleType = (VehicleType)Enum.Parse(typeof(VehicleType), vehicle["type"].ToString().Remove(0, "VEHICLE_TYPE_".Length));

                    tempMap[modelName] = new VehicleInfo
                    {
                        HasArmoredWindows = hasArmoredWindow,
                        Type = vehicleType
                    };
                }
            }

            return tempMap;
        }

        private SortedDictionary<string, int> ParseModKits()
        {
            SortedDictionary<string, int> tempMap = new SortedDictionary<string, int>();

            string filePath = sourcePath + "vehmods.json";

            if (!File.Exists(filePath))
                return tempMap;

            Utils.Log.Info($"Extracting modkits ids: {filePath}");
            JArray vehicleMods = (JArray)JsonConvert.DeserializeObject(File.ReadAllText(filePath));

            foreach(var vehicleMod in vehicleMods)
            {
                tempMap[vehicleMod["KitName"].ToString()] = Convert.ToInt32(vehicleMod["Id"]);
            }

            return tempMap;
        }

        public override void GenerateBin(string outputFilePath)
        {
            if (!Directory.Exists(sourcePath))
            {
                Utils.Log.Error($"Error while generating bin file: \"{sourcePath}\" was not found.");
                Utils.Log.Error("Aborting...");
                return;
            }

            SortedDictionary<string, VehicleInfo> vehicleMap = new SortedDictionary<string, VehicleInfo>();
            Utils.Log.Info($"Process all \"carvariations.json\" file inside \"{this.sourcePath}\"");
            {
                foreach (var vehicle in ParseCarVariation())
                    vehicleMap[vehicle.Key] = vehicle.Value;
            }

            Utils.Log.Info($"Process all \"vehicles.json\" file inside \"{this.sourcePath}\"");
            {
                foreach (var vehicle in ParseVehicleMeta())
                {
                    vehicleMap[vehicle.Key].HasArmoredWindows = vehicle.Value.HasArmoredWindows;
                    vehicleMap[vehicle.Key].Type = vehicle.Value.Type;
                }
            }

            foreach(var wheel in GetVehicleWheels())
            {
                vehicleMap[wheel.Key].WheelsCount = wheel.Value;
            }

            Utils.Log.Warning("MAKE SURE TO KEEP UP-TO-DATE \"vehicleExtras.json\" FILE!");
            Console.WriteLine("");
            Utils.Log.Info("Here's a little how-to create it:");
            Utils.Log.Info("1. Copy & paste the resource (\"dump-extras\") located in \"Scripts\" folder to your server's resources folder.");
            Utils.Log.Info("2. Open \"Client.js\" file inside \"dump-extras\" folder.");
            Utils.Log.Info($"3. Open \"{Path.GetFullPath(sourcePath) + "vehicleList.json"}\" file, copy the JSON array and overwrite \"vehicleList\" variable's value inside \"Client.js\" file.");
            Utils.Log.Info("4. Start the alt:V server with \"dump-extras\" resource, then join to the server.");
            Utils.Log.Info("5. Once you joined to the server, open your console then type in: \"dumpextras\" (without quotes) and wait until the script finishes.");
            Utils.Log.Warning("IT WILL CHEW UP A LOT OF MEMORY WHILE EXECUTING THE SCRIPT!");
            Utils.Log.Info($"6. Copy the result JSON array from the console into {Path.GetFullPath(sourcePath) + "vehicleExtras.json"} (if the file doesn't exist, create it).");
            Utils.Log.Info("7. Press Y to continue when prompted.");
            Console.WriteLine("");

            bool terminate = false;
            
            Utils.Log.Warning("IS \"vehicleExtras.json\" ARE UP-TO-DATE? (Y/N)");
            var s = Console.ReadLine();
            terminate = (s != null && s.Trim().Equals("N", StringComparison.InvariantCultureIgnoreCase));

            if (terminate)
            {
                Utils.Log.Error("Abort...");
                return;
            }

            Utils.Log.Info($"Extracting extras from \"vehicleExtras.json\" file");
            {
                string filePath = sourcePath + "vehicleExtras.json";

                if (!File.Exists(filePath))
                {
                    Utils.Log.Error($"Couldn't find \"{filePath}\"");
                    return;
                }

                Utils.Log.Info($"Extracting extras: {filePath}");
                JObject vehicleExtras = (JObject)JsonConvert.DeserializeObject(File.ReadAllText(filePath));

                foreach(var extra in vehicleExtras)
                {
                    vehicleMap[extra.Key].Extras = extra.Value["extras"].ToObject<int>();
                    vehicleMap[extra.Key].DefaultExtras = extra.Value["defaultExtras"].ToObject<int>();
                }
            }

            Utils.Log.Info("Save vehmodels data into \"vehmodels.json\"");
            File.WriteAllText(sourcePath + "vehmodels.json", JsonConvert.SerializeObject(vehicleMap, Formatting.Indented));

            Utils.Log.Info($"Generate bin file and save it to \"{outputFilePath}\"");
            using (BinaryWriter writer = new BinaryWriter(File.Open(outputFilePath, FileMode.Create)))
            {
                writer.Write("VE".ToCharArray());
                writer.Write(FILE_VERSION);

                foreach (var mod in vehicleMap)
                {
                    writer.Write(Convert.ToUInt32(mod.Value.Hash));
                    writer.Write((byte)mod.Key.Length);
                    writer.Write(mod.Key.ToCharArray());
                    writer.Write((int)mod.Value.Type);
                    writer.Write((byte)mod.Value.WheelsCount);
                    writer.Write(mod.Value.HasArmoredWindows);
                    writer.Write((byte)mod.Value.PrimaryColor);
                    writer.Write((byte)mod.Value.SecondaryColor);
                    writer.Write((byte)mod.Value.PearlColor);
                    writer.Write((byte)mod.Value.WheelsColor);
                    writer.Write((byte)mod.Value.InteriorColor);
                    writer.Write((byte)mod.Value.DashboardColor);
                    writer.Write(Convert.ToUInt16(mod.Value.ModKits[0]));
                    writer.Write(Convert.ToUInt16(mod.Value.ModKits[1]));
                    writer.Write(Convert.ToUInt16(mod.Value.Extras));
                    writer.Write(Convert.ToUInt16(mod.Value.DefaultExtras));
                }
            }
        }
	}
}
