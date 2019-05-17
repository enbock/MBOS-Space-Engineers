# MBOS Drone
The Drone Controller for MBOS Stations.
This program controlls a drone and communicate with the station(s).

# Requirements
On grid installed:
* a Timer
* a Programmable Block (for this script)
* 2 Remote Control Blocks
* Optional light animations
* A Station with a CHARGE port (see [MBOS Drone Port])
* a Antenna which triggers the Programmable Block
* Batteries
* COntainers
* Optional for Space: A 3rd Remote Control Block to un-dock.

# Installation
* A programmable block to timer block
* A start of timer block also to timer block actions
* Set the timer block to 1sec
* Set the antenna to trigger the programmable block
* Load this MBOS Drone script
* Let run the programmable block and see that somethings is missing
* Edit the Cutom Data and the names of the connector and remote block
* Set the remote block for flight to colision avoidance and precision mode.
* Set the docking remote controller(s) without collition avoidance and without precision mode.
* Decrese the docking controller(s) speed to 2 in space or 10 on planet.
* Change the direction of the docking controller in space so, that the connector flight in direction of port
* Set the optional undock controller opposite direction of docking controller
* Set on planet all controllers to forward.

Now the drone is ready to recive commands from station.

# Drone to Station Commnuication
The drone understand the communication protocol from MBOS controlled stations.
See [MBOS Drone Port] for more details.

# How it works
The drone can do 3 actions:
* `LOAD` - Load cargo to drone.
* `CARGO` - Unload cargo to station.
* `CHARGE` - Recharge drone battieries.

If the battery charge level ciritial(50% of charge) the flight the drone to a 
`CHARGE` port only. Otherwise it request a `CARGO` is goods in Containers. Are 
the containers empty, then it searches for a `LOAD` port. If no `LOAD` port free,
it flights to the next `CHARGE` port.

The drone starts only, if the target port reserved and confirmed by station. Thats important for Planet usage.

*Hint:* Be sure that always a free `CHARGE` port is available. Best to build for each drone a dedicated `CHARGE` port.


[MBOS Drone Port]: (https://steamcommunity.com/sharedfiles/filedetails/?id=1125864825)