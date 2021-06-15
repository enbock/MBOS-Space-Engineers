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

### Register at FlightControl
* [B]< `RegisterStation|<Station-ID>|<Hangar-GridID>|<FlightIn Waypoint>`