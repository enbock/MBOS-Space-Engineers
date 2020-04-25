# MBOS Flight Control

## Working idea
The Flight Control produce flight pathes and request hangar to send drone.

## Commands
* `RegisterPath

## Radio Transmissions
[B] == Broad cast    
[U] == Unicast
### Ask for Station Register
* [B]> `FlightControlReady|<Station-ID>`
### Register at FlightControl
* [B]< `RegisterStation|<Station-ID>|<Hangar-GridID>|<FlightIn Waypoint>`
### Register Hanger at FlightControl
* [B]< `RegisterHangar|<Station-ID>|<Station-GridID>|<FlightIn Waypoint>`
### Hangar Update Drone Stock
* [B]< `HangarUpdate|<Hangar-GridID>|<Drone Type>|<Amount>`
### Request flight path
* [B]< `RequestFlight|<Station-ID>|<Mission-ID>|<Drone Type>|<Start Waypoint>|<Start Station-GridID>|<Target Waypoint>|<Target Station-GridID>`
* [U]> `FlightPlan|<Mission-ID>|<FlightPath>`
### Complete mission
* [B]< `MissionCompleted|<Mission-ID>`