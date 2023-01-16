using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RageKit.GameFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AltV.Generator
{
	public class VehicleMods : Generator
	{
        private static readonly ushort FILE_VERSION = 1;
        //enum ModType
        //{
        //    UNKNOWN = -1,
        //    VMT_SPOILER = 0,
        //    VMT_BUMPER_F = 1,
        //    VMT_BUMPER_R = 2,
        //    VMT_SKIRT = 3,
        //    VMT_EXHAUST = 4,
        //    VMT_CHASSIS = 5,
        //    VMT_GRILL = 6,
        //    VMT_BONNET = 7,
        //    VMT_WING_L = 8,
        //    VMT_WING_R = 9,
        //    VMT_ROOF = 10,
        //    VMT_PLTHOLDER = 11,
        //    VMT_PLTVANITY = 12,
        //    VMT_INTERIOR1 = 13,
        //    VMT_INTERIOR2 = 14,
        //    VMT_INTERIOR3 = 15,
        //    VMT_INTERIOR4 = 16,
        //    VMT_INTERIOR5 = 17,
        //    VMT_SEATS = 18,
        //    VMT_STEERING = 19,
        //    VMT_KNOB = 20,
        //    VMT_PLAQUE = 21,
        //    VMT_ICE = 22,
        //    VMT_TRUNK = 23,
        //    VMT_HYDRO = 24,
        //    VMT_ENGINEBAY1 = 25,
        //    VMT_ENGINEBAY2 = 26,
        //    VMT_ENGINEBAY3 = 27,
        //    VMT_CHASSIS2 = 28,
        //    VMT_CHASSIS3 = 29,
        //    VMT_CHASSIS4 = 30,
        //    VMT_CHASSIS5 = 31,
        //    VMT_DOOR_L = 32,
        //    VMT_DOOR_R = 33,
        //    VMT_LIVERY_MOD = 34,
        //    VMT_LIGHTBAR = 35,
        //    VMT_ENGINE = 36,
        //    VMT_BRAKES = 37,
        //    VMT_GEARBOX = 38,
        //    VMT_HORN = 39,
        //    VMT_SUSPENSION = 40,
        //    VMT_ARMOUR = 41,
        //    VMT_NITROUS = 42,
        //    VMT_TURBO = 43,
        //    VMT_SUBWOOFER = 44,
        //    VMT_TYRE_SMOKE = 45,
        //    VMT_HYDRAULICS = 46,
        //    VMT_XENON_LIGHTS = 47,
        //    VMT_WHEELS = 48,
        //    VMT_WHEELS_REAR_OR_HYDRAULICS = 49,
        //    VMT_HOOD = 50 //maybe?
        //};
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

        private RpfManager rpfManager = null;

        public VehicleMods(RpfManager rpfManager, string sourcePath) : base(sourcePath)
        {
            this.rpfManager = rpfManager;
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

                Utils.Log.Info($"Processing \"{kitName}\" kit");

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
                        if(m["type"] != null)
                        {
                            var typeId = Convert.ToInt32(Enum.Parse(typeof(ModType), m["type"].ToString()));
                            if (!kit.Kits.ContainsKey(typeId))
                                kit.Kits.Add(typeId, new List<int>());

                            kit.Kits[typeId].Add(index);
                            index++;
                        }
                    }

                    kit.Kits = kit.Kits.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                }
                tempList.Add(kit);
            }

            return tempList;
		}

        /// <summary>
        /// Processes all carcols.json files located in the given sourcePath parameter then compile it to vehmods.bin file
        /// </summary>
        /// <param name="sourcePath">Where carcols.json files located at</param>
        /// <param name="outputFilePath">Where the bin file should save to</param>
        public override void GenerateBin(string outputFilePath)
        {
            if(!Directory.Exists(sourcePath))
            {
                Utils.Log.Error($"Error while generating bin file: \"{sourcePath}\" was not found.");
                Utils.Log.Error("Aborting...");
                return;
			}

            List<string> dlcList = GetDLC();
            if(dlcList.Count == 0)
            {
                return;
			}

            Utils.Log.Info($"Process all \"carcols.json\" file inside \"{sourcePath}\"");

            List<ModKit> modKits = new List<ModKit>();
            foreach (var dlc in dlcList)
            {
                string dlcPath = dlc + Path.DirectorySeparatorChar;
                string filePath = sourcePath + dlcPath + "carcols.json";

                if (!File.Exists(filePath))
                    continue;

                var carcol = ParseCarcol(filePath);
                modKits = modKits.Concat(carcol)
                    .ToLookup(p => p.KitName)
                    .Select(g => g.Aggregate((p1, p2) => new ModKit
                    {
                        Id = p2.Id,
                        KitName = p2.KitName,
                        Kits = p2.Kits
                    })).ToList();
            }

            modKits = modKits.OrderBy(x => x.Id).ToList();

            var jsonOutput = Path.ChangeExtension(outputFilePath, ".json");
            Utils.Log.Info($"Save vehicle mods in json file format to \"{jsonOutput}\"");
            File.WriteAllText(jsonOutput, JsonConvert.SerializeObject(modKits, Formatting.Indented));

            Utils.Log.Info($"Generate bin file and save it to \"{outputFilePath}\"");
            using (BinaryWriter writer = new BinaryWriter(File.Open(outputFilePath, FileMode.Create)))
            {
                writer.Write("MO".ToCharArray());
                writer.Write(FILE_VERSION);
                
                foreach(var mod in modKits)
                {
                    writer.Write(Convert.ToUInt16(mod.Id));
                    writer.Write(Convert.ToUInt16(mod.KitName.Length));
                    writer.Write(mod.KitName.ToCharArray());
                    writer.Write((byte)mod.Kits.Count);

                    foreach(var kit in mod.Kits)
                    {
                        writer.Write((byte)kit.Key);
                        writer.Write((byte)kit.Value.Count);

                        foreach (var modId in kit.Value)
                        {
                            writer.Write((ushort)modId);
                        }
                    }
                }
            }
        }
    }
}
