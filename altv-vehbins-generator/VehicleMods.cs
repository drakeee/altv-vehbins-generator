using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AltV.Generator
{
	static class VehicleMods
	{
        enum ModType
        {
            UNKNOWN = -1,
            VMT_SPOILER = 0,
            VMT_BUMPER_F = 1,
            VMT_BUMPER_R = 2,
            VMT_SKIRT = 3,
            VMT_EXHAUST = 4,
            VMT_CHASSIS = 5,
            VMT_GRILL = 6,
            VMT_BONNET = 7,
            VMT_WING_L = 8,
            VMT_WING_R = 9,
            VMT_ROOF = 10,
            VMT_ENGINE = 11,
            VMT_BRAKES = 12,
            VMT_GEARBOX = 13,
            VMT_HORN = 14,
            VMT_SUSPENSION = 15,
            VMT_ARMOUR = 16,
            VMT_TURBO = 18,
            VMT_XENON_LIGHTS = 22,
            //FrontWheels = 23,
            //BackWheels = 24,
            VMT_PLTHOLDER = 25,
            VMT_PLTVANITY = 26,
            VMT_INTERIOR1 = 27,
            VMT_INTERIOR2 = 28,
            VMT_INTERIOR3 = 29,
            VMT_INTERIOR4 = 30,
            VMT_INTERIOR5 = 31,
            VMT_SEATS = 32,
            VMT_STEERING = 33,
            VMT_KNOB = 34,
            VMT_PLAQUE = 35,
            VMT_ICE = 36,
            VMT_TRUNK = 37,
            VMT_HYDRO = 38,
            VMT_ENGINEBAY1 = 39,
            VMT_ENGINEBAY2 = 40,
            VMT_ENGINEBAY3 = 41,
            VMT_CHASSIS2 = 42,
            VMT_CHASSIS3 = 43,
            VMT_CHASSIS4 = 44,
            VMT_CHASSIS5 = 45,
            VMT_DOOR_L = 46,
            VMT_DOOR_R = 47,
            VMT_LIVERY_MOD = 48,
            VMT_WHEELS_REAR_OR_HYDRAULICS = 49

            //VMT_LIGHTBAR = 35,
            //VMT_NITROUS = 42,
            //VMT_SUBWOOFER = 44,
            //VMT_TYRE_SMOKE = 45,
            //VMT_HYDRAULICS = 46,
            //VMT_WHEELS = 48,
        };

        class ModKit
        {
            public int Id;
            public string KitName;
            public Dictionary<int, List<int>> Kits;
        }

        private static List<ModKit> ParseCarcol(string filePath)
        {
            List<ModKit> tempList = new List<ModKit>();

            if (!File.Exists(filePath))
                return tempList;

            Utils.Log.Info($"Extracting carcol: {filePath}");
            JObject carcol = (JObject)JsonConvert.DeserializeObject(File.ReadAllText(filePath));
            var carcolList = carcol["CVehicleModelInfoVarGlobal"]["Kits"]["Item"];
            carcolList = (carcolList is JObject) ? new JArray((JObject)carcolList) : carcolList;

            foreach (var kitData in carcolList)
            {
                int kitId = Convert.ToInt32(kitData["id"]["@value"]);
                string kitName = kitData["kitName"].ToString();

                if (tempList.Where(p => p.KitName == kitName).ToList().Count > 0)
                    continue;

                ModKit kit = new ModKit
                {
                    Id = kitId,
                    KitName = kitName
                };
                kit.Kits = new Dictionary<int, List<int>>();


                int index = 0;
                foreach (var modItem in new string[] { "visibleMods", "statMods" })
                {
                    var mod = kitData[modItem];
                    if (
                        mod.Type == JTokenType.Null ||
                        mod == null ||
                        String.IsNullOrEmpty(mod.ToString())
                    )
                        continue;

                    mod = mod["Item"];
                    mod = (mod is JObject) ? new JArray((JObject)mod) : mod;

                    foreach (var m in mod)
                    {
                        var typeId = Convert.ToInt32(Enum.Parse(typeof(ModType), m["type"].ToString()));
                        if (!kit.Kits.ContainsKey(typeId))
                            kit.Kits.Add(typeId, new List<int>());

                        kit.Kits[typeId].Add(index);
                        index++;
                    }

                    kit.Kits = kit.Kits.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                }
                tempList.Add(kit);

                Console.WriteLine($"Kit: {kitData["kitName"]} - {kit.Kits.Count}");

                //Console.WriteLine(JsonConvert.SerializeObject(kit));
            }

            return tempList;
		}

        /// <summary>
        /// Processes all carcols.json files located in the given sourcePath parameter then compile it to vehmods.bin file
        /// </summary>
        /// <param name="sourcePath">Where carcols.json files located at</param>
        /// <param name="outputFilePath">Where the bin file should save to</param>
        public static void GenerateBin(string sourcePath, string outputFilePath)
        {
            if(!Directory.Exists(sourcePath))
            {
                Utils.Log.Error($"Error while generating bin file: \"{sourcePath}\" was not found.");
                Utils.Log.Error("Aborting...");
                return;
			}

            List<string> dlcList = GetDLC(sourcePath);
            if(dlcList.Count == 0)
            {
                return;
			}
            dlcList.Insert(0, ".");

            List<string> searchPath = new List<string>()
            {
                "./",
                "./dlcpacks/",
                "./dlc_patch/"
            };

            List<ModKit> modKits = new List<ModKit>();
            foreach(var search in searchPath)
            {
                foreach (var dlc in dlcList)
                {
                    string dlcPath = Path.DirectorySeparatorChar + dlc + Path.DirectorySeparatorChar;
                    string filePath = sourcePath + search + dlcPath + "carcols.json";

                    if (!File.Exists(filePath))
                        continue;

                    var carcol = ParseCarcol(filePath);
                    //modKits = modKits.Concat(carcol).ToList();
                    modKits = modKits.Concat(carcol)
                        .ToLookup(p => p.KitName)
                        .Select(g => g.Aggregate((p1, p2) => new ModKit
                        {
                            Id = p2.Id,
                            KitName = p2.KitName,
                            Kits = p2.Kits
                        })).ToList();

                    //modKits = modKits.Union(ParseCarcol(filePath), new ModKitsComparer()).ToList();
                }
			}

            modKits = modKits.OrderBy(x => x.Id).ToList();
            File.WriteAllText("vehicles_test.json", JsonConvert.SerializeObject(modKits, Formatting.Indented));
		}

        private static List<string> GetDLC(string sourcePath)
        {
            List<string> tempList = new List<string>();

            if (!File.Exists(sourcePath + "dlclist.json"))
            {
                Utils.Log.Error($"\"dlclist.json\" was not found in path.");
                Utils.Log.Error("Aborting...");
                return tempList;
			}

            dynamic dlcList = JsonConvert.DeserializeObject(File.ReadAllText(sourcePath + "dlclist.json"));
            var dlcOrder = ((JArray)dlcList["SMandatoryPacksData"]["Paths"]["Item"]).Select(x => x.ToString().Split('/').Reverse().Skip(1).FirstOrDefault().ToLower()).ToList();

            return dlcOrder;
		}
    }
}
