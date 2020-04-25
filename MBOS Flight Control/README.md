# MBOS Flight Control

## Working idea
The Flight Control produce flight pathes and request hangar to send drone.

## Commands
* `SetFallback <GPS>` - Fallback flightpath point.
* `RegisterFlightPath <Start-GridID> <Target-GridID> <GPS>[...]` - Create/Update new flight path. GPS must be written without space.

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