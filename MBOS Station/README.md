# MBOS Station

## Working idea
This is the station representer. It do "standard stuff", like register grid on Flight Control.

### Needed for
* Register Grid(Station) in Flight Control

## Commands
* `FlightIn <GPS>` - Registers Flight in Point

## Radio Transmissions
[B] == Broad cast    
[U] == Unicast

### Register at FlightControl, Resource Manager, etc
* [B]> `RegisterStation|<Station-ID>|<Station-GridID>|<FlightIn Waypoint>`

### Erneute Registrierung 
* [B]< `RequestRenewStationRegister`
* [B]> `RegisterStation|<Station-ID>|<Station-GridID>|<FlightIn Waypoint>`
