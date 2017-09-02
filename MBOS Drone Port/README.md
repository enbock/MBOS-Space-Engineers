# MBOS Drone Port
Station drone port manager.

Communicate with the drones and transmit commands, landing allowance and coordinated of the connector ports.


# Requirements
On grid installed:
* Core
* Bus
* Transmitter
* Connector(s) without magentic power.

# Usage
In follow run the script with

* Set a connector:

		SET:<PORT NUMBER>:<ACTION>:<CONNECTOR NAME>:<X>,<Y>,<Z>

* Remove a connector:

		REMOVE:<PORT NUMBER>

* Actions
	* `LOAD` - Dock to get loaded with goods
	* `CHARGE` - Dock to reacharge the batteries.
* `<X>,<Y>,<Z>` - Relative offset coordinate of flight to station point

# Protocol with Drone

* `<X><Y><Z>` - Drone position
* `<X1><Y1><Z1>` - Flight to station point (usually 20 meter above connector)
* `<X1><Y2><Z2>` - Corrdinate of connector
* `<SHIP NAME>` - Grid name of the drone
* `<STATION NAME>` - Grid name of the station (port)

## Require an action

* Done: `NEED|<ACTION>|<X>|<Y>|<Z>|<SHIP NAME>[|<CARGO TYPE>]`
* Port: `<SHIP NAME>|<ACTION>|<PORT NUMBER>|<DISTANCE>|<STATION NAME>` (multiple port answers)
* Drone: `<STATION NAME>|REQUEST|<ACTION>|<PORT NUMBER>|<SHIP NAME>`
* Port: `<SHIP NAME>|<ACTION>|<PORT NUMBER>|RESERVED|<X1>|<Y1>|<Z1>|<X2>|<Y2>|<Z2>|<STATION NAME>`
* Port-Error: `<SHIP NAME>|<ACTION>|DENIED|<STATION NAME>` (drone restarts now)

## Dock info

* Drone: `<STATION NAME>|DOCKED|<PORT NUMBER>|<SHIP NAME>`

## Undocking

* Drone: `<STATION NAME>|RELEASED|<SHIP NAME>`

# Configs
* `ProvideCargo` - comma separated list of provided cargo items to transport. eg. `Ore,Ammo`
* `AcceptCargo` - Configure which types of cargo can be delivered. eg. `Ore,Ammo`