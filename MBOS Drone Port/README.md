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

		SET:<PORT NUMBER>:{CHARGE|LOAD}:<CONNECTOR NAME>

* Remove a connector:

		REMOVE:<PORT NUMBER>

# Protocol with Drone

* `<X1><Y1><Z1>` - Flight to station point (usually 20 meter above connector)
* `<X1><Y2><Z2>` - Corrdinate of connector

## Require charging point

* Done: `NEED:CHARGE|<SHIP NAME>`
* Port: `<SHIP NAME>|CHARGE|<PORT NUMBER>|RESERVED|<X1>|<Y1>|<Z1>|<X2>|<Y2>|<Z2>`

## Require new action

* Drone: `NEED:ACTION|<SHIP NAME>`
* Port: `<SHIP NAME>|LOAD|<STATION NAME>`
* Drone: `<STATION NAME>|REQUEST|LOAD|<SHIP NAME>`
* Port: `<SHIP NAME>|LOAD|<PORT NUMBER>|RESERVED|<X1>|<Y1>|<Z1>|<X2>|<Y2>|<Z2>`
* Port-Error: `<SHIPNAME>|LOAD|DENNIED` (drone restarts now)

## Undocking

* Drone: `<STATION NAME>|RELEASE|<PORT NUMBER>`