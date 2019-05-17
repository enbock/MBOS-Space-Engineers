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

		SET:<PORT NUMBER>:<CONNECTOR NAME>:<ACTION>:{<TYPE>,ANY}:<AMOUNT>:<X>,<Y>,<Z>

* Remove a connector:

		REMOVE:<PORT NUMBER>

* Actions
	* `LOAD` - Dock to get loaded with goods
	* `CHARGE` - Dock to reacharge the batteries.
	* `CARGO` - Dock to unload goods.
* `TYPE` - Accept(when `CARGO`) or Provide(when `LOAD`) the type of inventory item.
* `AMOUNT` - When `CARGO` accept the `TYPE` only of not `AMOUNT` in cargo containers(0 = Accept
  endless). When `LOAD` provide only when `AMOUNT` is in cargo containers (0 = Provide so long
  have items).
* `<X>,<Y>,<Z>` - Relative offset coordinate of flight to station point

Examples:

		SET:1:Port 1:CARGO:Ore:0:0,-20,0
		SET:2:Port 2:CHARGE:ANY:0:0,-20,0
		SET:3:Port 3:LOAD:Ore:0:0,-20,0
		SET:4:Port 4:CARGO:AmmoMagazine:1000:0,-20,0

## Stored data on port

	PORT:<PORT NUMBER>:<CONNECTOR NAME>:<ACTION>:{<TYPE>,ANY}:<AMOUNT>:<X>,<Y>,<Z>:<RESERVATION>

# Protocol with Drone

* `<X><Y><Z>` - Drone position
* `<X1><Y1><Z1>` - Flight to station point (usually 20 meter above connector)
* `<X1><Y2><Z2>` - Corrdinate of connector
* `<SHIP NAME>` - Grid name of the drone
* `<STATION NAME>` - Grid name of the station (port)

## Require an action

* Done: `NEED|<ACTION>|<X>|<Y>|<Z>|<SHIP NAME>|<CARGO TYPE>`
* Port: `<SHIP NAME>|<ACTION>|<PORT NUMBER>|<DISTANCE>|<STATION NAME>` (multiple port answers)
* Drone: `<STATION NAME>|REQUEST|<ACTION>|<PORT NUMBER>|<SHIP NAME>`
* Port: `<SHIP NAME>|<ACTION>|<PORT NUMBER>|RESERVED|<X1>|<Y1>|<Z1>|<X2>|<Y2>|<Z2>|<STATION NAME>`
* Port-Error: `<SHIP NAME>|<ACTION>|DENIED|<STATION NAME>` (drone restarts now)

## Dock info

* Drone: `<STATION NAME>|DOCKED|<PORT NUMBER>|<SHIP NAME>`

## Undocking

* Drone: `<STATION NAME>|RELEASED|<SHIP NAME>`

## Transmitter Extension
The MBOS transmitter needs a timestamp understand new and old messages.

Each Message get now a timestamo before: `<TIME>|<MESSAGE>`

# Configs
* See empty values in CustomData of the programmable block ;)