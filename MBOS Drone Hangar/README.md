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

## Radio Transmissions
### Empty pods
`DroneHangarHasPodsAvailable|<Hangar-GridID>`

### Drone asking for pods
* Drone(Brodcast): `DroneNeedHome|<Drone-GridID>`
* Hangar(Whisper): `DroneRegisteredAt|<Hangar-GridID>|<FlightPath to Pod>`