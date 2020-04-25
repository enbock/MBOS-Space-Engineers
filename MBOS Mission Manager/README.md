# MBOS Mission Manager

## Working idea
The Mission Manager coordinate the missions for drone inside the broad cast network.

### Process to deliver resource
1. Resource Manager create "transport mission" and the Mission Manager
1. Mission Manager ask Flight Control for flight path of mission
1. After receive flight path, the Resource Manager request a (transport) drone for flight path
1. Drone Manager send mission done update
   * in meantime consumer update stock receiving 
1. Resource Manager removes mission

## Radio Transmissions
[B] == Broad cast    
[U] == Unicast
### Request mission
* [B] Manager< `RequestMission|<Requester Station-ID>|<Mission-ID>|<Drone Type>|<Producer Waypoint>|<Producer Station-GridID>|<Consumer Waypoint>|<Consumer Station-GridID>`
### Request flight path
* [U] Manager to FlightControl> `RequestFlight|<Mission-ID>|<Drone Type>|<Start Waypoint>|<Start Station-GridID>|<Target Waypoint>|<Target Station-GridID>`
* [U] FlightControl to Manager> `FlightPlan|<Mission-ID>|<FlightPath>`
### Complete mission
* [B] Manager< `MissionCompleted|<Mission-ID>`