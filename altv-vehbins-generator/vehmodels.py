from genericpath import isfile
import json
import subprocess
from typing import List
import xml.etree.ElementTree as ET
import collections
import os
import colorama
import struct

def progress(s: str):
    print(colorama.Fore.RED+"> "+colorama.Fore.WHITE+s)

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

progress("Extracting necessary files from RPF files are done")
progress("Importing list from dlclist.xml")

outputPath = "./output_files/"
dlcList = parseDlcList(outputPath + "dlclist.xml")

progress("Extract base carcols.xml")
parseCarcol(outputPath + "carcols.xml", vehMods)

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