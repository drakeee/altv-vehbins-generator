import * as alt from 'alt-client';
import * as native from 'natives';

let vehicleList = ["EDIT ME"];

async function hasModelLoaded(model) {
    return new Promise(resolve => {
        if(native.hasModelLoaded(model))
            return resolve(true);

        native.requestModel(model);

        let interval = alt.setInterval(() => {
            if(native.hasModelLoaded(model)) {
                resolve(true);
                alt.clearInterval(interval);
            }
        }, 5);
    });
}

function bit_test(num, bit){
    return ((num>>bit) % 2 != 0)
}

function bit_set(num, bit){
    return num | 1<<bit;
}

function bit_clear(num, bit){
    return num & ~(1<<bit);
}

function bit_toggle(num, bit){
    return bit_test(num, bit) ? bit_clear(num, bit) : bit_set(num, bit);
}

alt.on("consoleCommand", async function(cmd, vehicleName)
{
    if(cmd == "dumpextras")
    {
        alt.log("Dumping vehicles extras...");
        let temp = {};

        for (const element of vehicleList) {
            let elementHash = alt.hash(element);
            let modelLoaded = await hasModelLoaded(elementHash);
            let vehicle = native.createVehicle(elementHash, 0.0, 0.0, 0.0, 0.0, false, false, false);

            if(vehicle != 0)
            {
                temp[element] = {
                    extras: 0,
                    defaultExtras: 0
                }

                for (let index = 0; index < 16; index++) {
                    if(native.doesExtraExist(vehicle, index))
                        temp[element].extras = bit_set(temp[element].extras, index);

                    if(native.isVehicleExtraTurnedOn(vehicle, index))
                        temp[element].defaultExtras = bit_set(temp[element].defaultExtras, index);
                }
            } else 
                alt.log("Failed to create vehicle: " + element)

            native.setEntityAsNoLongerNeeded(vehicle);
            native.setModelAsNoLongerNeeded(elementHash);
            
            //native.deleteVehicle(vehicle);
            native.deleteEntity(vehicle);

            alt.log(`Extracted vehicle: ${element}`);
        }

        alt.log(JSON.stringify(temp));
    }

    if(cmd == "spawn")
    {
        if(vehicleName == null)
            return;

        alt.log(`args: ${vehicleName}`);
        
        let playerPos = alt.Player.local.pos;

        let hash = alt.hash(vehicleName);
        native.requestModel(hash);
        alt.log(native.hasModelLoaded(hash));
        let vehicle = native.createVehicle(hash, playerPos.x, playerPos.y + 3.0, playerPos.z, 0.0, false, false, false);
        alt.log(vehicle);
    }
});