# MBOS Drone Hangar

## Working idea
Drone will be send out for a mission. Afterwards the drones came back to "reast&relax" ;)

## Installation
At moment can only one Hangar available in Radio Network.

## Future ideas
* Hangar Manager to allow multiple hangars.

## Pods
Pods are connector, on that a Drone can park and recharge.    
Connectors must just contain the word "pod" in his name.

## Commands
* `FlightIn <GPS>` - Registers Flight in Point

## Radio Transmissions
[B] == Broad cast    
[U] == Unicast

### Empty pods
* [B]< `DroneHangarHasPodsAvailable|<Hangar-GridID>`

### Drone asking for pods
* [B] Drone< `DroneNeedHome|<Drone-GridID>|<Drone Type>`
* [U] Hangar> `DroneRegisteredAt|<Hangar-GridID>|<FlightPath to Pod>`

### Register at FlightControl
* [B]> `RegisterHangar|<Station-ID>|<Hangar-GridID>|<FlightIn Waypoint>`

### Update Drone Stock
* [U]> `HangarUpdate|<Hangar-GridID>|<Drone Type>|<Amount>`

### Request transport
* [U]< `TransportRequest|<Mission-ID>|<FlightPath>`

### Complete mission
* [B]> `MissionCompleted|<Mission-ID>`