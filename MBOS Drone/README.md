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