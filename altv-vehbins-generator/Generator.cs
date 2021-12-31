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
    public class Generator
    {
        public string sourcePath = "";
        public Generator(string sourcePath)
        {
            this.sourcePath = sourcePath;
        }

        public virtual void GenerateBin(string outputPath) { }
        protected List<string> GetDLC()
        {
            List<string> tempList = new List<string>();

            if (!File.Exists(this.sourcePath + "dlclist.json"))
            {
                Utils.Log.Error($"\"dlclist.json\" was not found in path.");
                Utils.Log.Error("Aborting...");
                return tempList;
            }

            tempList.Insert(0, ".");
            dynamic dlcList = JsonConvert.DeserializeObject(File.ReadAllText(sourcePath + "dlclist.json"));
            var dlcOrder = ((JArray)dlcList["SMandatoryPacksData"]["Paths"]["Item"]).Select(x => x.ToString().Split('/').Reverse().Skip(1).FirstOrDefault().ToLower()).ToList();

            List<string> searchPath = new List<string>()
            {
                "./dlcpacks/",
                "./dlc_patch/"
            };

            dlcOrder = searchPath.Where(x => x != null).SelectMany(g => dlcOrder.Where(c => c != null).Select(c => g + c)).ToList();
            dlcOrder.Insert(0, "./");

            return dlcOrder;
        }
    }
}
