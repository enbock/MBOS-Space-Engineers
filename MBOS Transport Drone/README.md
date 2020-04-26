# MBOS Transport Drone
The drone control script.

It communicates with the Drone Hangar to find his "home".

The drone charges ar home only.

Tip: Use the stabilization script on planets.

## Komplex Flight Pathes
    GPS::47755.46:31254.76:22744.02:GPS::47988.64:30528.99:23203.6:>GPS:Start Point:48050.79:30398.66:23052.6:<GPS::48058.54:30421.23:23013.6:GPS::48026.37:30577.35:22880.5:GPS::48089.26:30621.37:22682.89:GPS::48122.93:30572.5:22677.53:GPS::48178.75:30611.43:22521.67:>GPS:Target Point:48218.89:30616.29:22363.15:<GPS:Factory FlightIn:48227.4:30659.35:22414.81:GPS:Fallback FlightPath:48202.94:31232.9:22671.63:GPS:Drone Hangar FlightIn:47770.88:31417.6:22590.87:

*Control chars*:
* `<` Mark a new path segment
* `>` Mark GPS to dock(precision) mode (real docking is doing home only). Dock Mode GPS must be the last of a path segment

## Radio Transmission
### Add flight path
* [U]< `AddFlightPath|<FlightPath>`
### Start drone
* [U]< `StartDrone`
### Register at Drone Hangar by Hangar action
* [B]< `DroneHangarHasPodsAvailable|<Hangard-EntityID>`
* [U]> `DroneNeedHome|<Drone-EntityID|transport`
* [U]< `DroneRegisteredAt|<Hangar-EntityID>|<FlightPath to Home>`
### Register at Drone Hangar by Drone action (start after produced)
* [B]> `DroneNeedHome|<Drone-EntityID|transport`
* [U]< `DroneRegisteredAt|<Hangar-EntityID>|<FlightPath to Home>`