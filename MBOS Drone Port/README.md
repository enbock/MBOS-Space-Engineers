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

		SET:<PORT NUMBER>:<ACTION>:<CONNECTOR NAME>

* Remove a connector:

		REMOVE:<PORT NUMBER>

## Actions
* `LOAD` - Dock to get loaded with goods
* `CHARGE` - Dock to reacharge the batteries.

# Protocol with Drone

* `<X1><Y1><Z1>` - Flight to station point (usually 20 meter above connector)
* `<X1><Y2><Z2>` - Corrdinate of connector
* `<SHIP NAME>` - Grid name of the drone
* `<STATION NAME>` - Grid name of the station (port)

## Require an action

* Done: `NEED:<ACTION>|<SHIP NAME>`
* Port: `<SHIP NAME>|<ACTION>|<STATION NAME>` (multiple port answers)
* Drone: `<STATION NAME>|REQUEST|<ACTION>|<SHIP NAME>`
* Port: `<SHIP NAME>|<ACTION>|<PORT NUMBER>|RESERVED|<X1>|<Y1>|<Z1>|<X2>|<Y2>|<Z2>`
* Port-Error: `<SHIP NAME>|<ACTION>|DENIED` (drone restarts now)

## Undocking

* Drone: `<STATION NAME>|RELEASED|<PORT NUMBER>`