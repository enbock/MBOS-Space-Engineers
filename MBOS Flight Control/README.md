# MBOS Flight Control

## Working idea
The Flight Control produce flight pathes and request hangar to send drone.

Is is a self learning algorithm. Just create MBOS Station terminals. The Drone will measure the flight time and report to this FlightControl.
The algorithm will find a better way, after the drones were flying some rounds.

The Flight Control received missions from the Resource Manager and passing the found flight pathes to the Drone Hangar.
The Drone Hangar will load the missions into the drones.

## Commands
* `SetFallback <GPS>` - Fallback flightpath point.

## Radio Transmissions
[B] == Broad cast    
[U] == Unicast
### Ask for Station Register
* [B]> `FlightControlReady|<Station-ID>`
### Register at FlightControl
* [B]< `RegisterStation|<Station-ID>|<Hangar-GridID>|<FlightIn Waypoint>`
### Register Hanger at FlightControl
* [B]< `RegisterHangar|<Station-ID>|<Station-GridID>|<FlightIn Waypoint>`
### Request flight path
* [B]< `RequestFlight|<Mission-ID>|<Drone Type>|<Start Waypoint>|<Start Station-GridID>|<Target Waypoint>|<Target Station-GridID>`
* [U] to Hangar> `RequestTransport|<Mission-ID>|<Drone Type>|<FlightPath>`
### Complete mission
* [B] from Hangar< `MissionCompleted|<Mission-ID>`
### Flight times
* [B]< `FlightTime|<GPS from>|<GPS to>|<time>`
