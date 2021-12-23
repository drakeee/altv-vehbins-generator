from genericpath import isfile
import json
import subprocess
from typing import List
import collections
import os
import struct

#p = subprocess.Popen("demo.exe", shell=False)
#p.communicate()

vehicleModType = {
    "UNKNOWN": -1,
    "VMT_SPOILER": 0,
    "VMT_BUMPER_F": 1,
    "VMT_BUMPER_R": 2,
    "VMT_SKIRT": 3,
    "VMT_EXHAUST": 4,
    "VMT_CHASSIS": 5,
    "VMT_GRILL": 6,
    "VMT_BONNET": 7,
    "VMT_WING_L": 8,
    "VMT_WING_R": 9,
    "VMT_ROOF": 10,
    "VMT_ENGINE": 11,
    "VMT_BRAKES": 12,
    "VMT_GEARBOX": 13,
    "VMT_HORN": 14,
    "VMT_SUSPENSION": 15,
    "VMT_ARMOUR": 16,
    "VMT_TURBO": 18,
    "VMT_XENON_LIGHTS": 22,
    #//FrontWheels: 23
    #//BackWheels: 24
    "VMT_PLTHOLDER": 25,
    "VMT_PLTVANITY": 26,
    "VMT_INTERIOR1": 27,
    "VMT_INTERIOR2": 28,
    "VMT_INTERIOR3": 29,
    "VMT_INTERIOR4": 30,
    "VMT_INTERIOR5": 31,
    "VMT_SEATS": 32,
    "VMT_STEERING": 33,
    "VMT_KNOB": 34,
    "VMT_PLAQUE": 35,
    "VMT_ICE": 36,
    "VMT_TRUNK": 37,
    "VMT_HYDRO": 38,
    "VMT_ENGINEBAY1": 39,
    "VMT_ENGINEBAY2": 40,
    "VMT_ENGINEBAY3": 41,
    "VMT_CHASSIS2": 42,
    "VMT_CHASSIS3": 43,
    "VMT_CHASSIS4": 44,
    "VMT_CHASSIS5": 45,
    "VMT_DOOR_L": 46,
    "VMT_DOOR_R": 47,
    "VMT_LIVERY_MOD": 48,
    "VMT_WHEELS_REAR_OR_HYDRAULICS": 49

    #VMT_LIGHTBAR = 35,
    #VMT_NITROUS = 42,
    #VMT_SUBWOOFER = 44,
    #VMT_TYRE_SMOKE = 45,
    #VMT_HYDRAULICS = 46,
    #VMT_WHEELS = 48,
}

class bcolors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    UNDERLINE = '\033[4m'

def progress(s: str):
    print(bcolors.FAIL+"> "+bcolors.ENDC+s)

def search_modkit(input: list, key: str, value: any):
    for index, obj in enumerate(input):
        if obj[key] == value:
            return index

    return -1

def parseCarcol(filePath: str, output: list):
    root = json.load(open(filePath))
    print(root)

    #Remove duplicated kitName in carcols file
    for kit in root["CVehicleModelInfoVarGlobal"]["Kits"]["Item"]:
        kitName = kit["kitName"]

        elements = root.findall('Kits/Item[kitName="'+kitName+'"]')
        del elements[0] #delete the first occurence because we are going to merge data to this node

        for node in elements:
            visibleItems = node.findall("visibleMods/Item")
            for item in visibleItems:
                kit.find("visibleMods").extend(node)
            
            root.find("Kits").remove(node)

    #Start to extract the data from the files
    for kit in root.findall("Kits/Item"):
        kitName = kit.find("kitName").text

        progress("Extracting \""+colorama.Fore.LIGHTBLUE_EX+kitName+colorama.Fore.WHITE+"\" mods")

        kitId = kit.find("id").attrib["value"]

        index = search_modkit(output, "Name", kitName)
        temp = {
            "Id": int(kitId),
            "Name": kitName,
            "Mods": {}
        }
        if index == -1:
            output.append(temp)
        else:
            output[index] = temp

        lastItem = output[index]
        modIndex = 0
        for item in ("visibleMods", "statMods"):
            for mod in kit.findall(item + "/Item"):

                modType = vehicleModType[mod.find("type").text]

                if not modType in lastItem["Mods"]:
                    lastItem["Mods"][modType] = []
                
                lastItem["Mods"][modType].append(modIndex)
                modIndex += 1

        lastItem["Mods"] = collections.OrderedDict(sorted(lastItem["Mods"].items()))

def parseDlcList(filePath: str):
    root = json.load(open(filePath))

    tempList = list()

    for dlc in root["SMandatoryPacksData"]["Paths"]["Item"]:
        dlcName = dlc.split("/")
        dlcName = dlcName[len(dlcName) - 2]

        tempList.append(dlcName)

    print(tempList)
    return tempList

def generateBin(vehMods: dict):
    with open('vehmods.bin', 'wb') as outfile:

        outfile.write(struct.pack('<2c', *[char.encode("UTF-8") for char in "MO"]))
        outfile.write(struct.pack('<H', 1))

        for modkit in vehMods:
            outfile.write(struct.pack('<H', modkit["Id"]))
            outfile.write(struct.pack('<H', len(modkit["Name"])))
            outfile.write(struct.pack(f'<{len(modkit["Name"])}c', *[char.encode("UTF-8") for char in modkit["Name"]]))
            outfile.write(struct.pack('<B', len(modkit["Mods"].keys())))

            for modKey in modkit["Mods"]:
                mod = modkit["Mods"][modKey]

                outfile.write(struct.pack('<B', int(modKey)))
                outfile.write(struct.pack('<B', len(mod)))

                for modID in mod:
                    outfile.write(struct.pack('<H', int(modID)))

        outfile.close()


vehMods = []

progress("Importing list from dlclist.json")

outputPath = "./output_files/"
dlcList = parseDlcList(outputPath + "dlclist.json")

progress("Extract base carcols.ymt")
parseCarcol(outputPath + "carcols.ymt", vehMods)

progress("Loop through all the DLC list")
for dlc in dlcList:
    for l in ("dlcpacks", "dlc_patch"):
        fileName = (outputPath + l + os.path.sep + dlc + os.path.sep + "carcols.meta")
        if(os.path.isfile(fileName)):
            parseCarcol(fileName, vehMods)

progress("Sort the vehicle mods by Id")
vehMods = sorted(vehMods, key=lambda k: k['Id'])

progress("Extracted "+colorama.Fore.LIGHTBLUE_EX + str(len(vehMods))+colorama.Fore.WHITE+" vehicle mods")

progress("Generating vehmods.bin")
generateBin(vehMods)

#pprint.PrettyPrinter(indent=4).pprint(vehMods)
with open("./vehmods_drake.json", "w+") as output:
    output.write(json.dumps(vehMods, indent=4))
    output.close()