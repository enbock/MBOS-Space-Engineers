# MBOS Resource Manager

## Working idea
Manage a catalog of network available and needed resources.

### Process to request resource
1. Consumer request amount of resource
1. Manager reserve resource and order by producer
   1. Manager create "transport mission(s)" for mission manager
1. Producer reserve and do stock update
   1. When mission took resources, Product update stock again and reduce Reservation Quantity
   1. When mission delivers resources, Consumer send.
1. Mission Manager send mission done. Resource Manager remove mission. (TBD: Do we need a recheck of resource requirement?)

### Resources 
Resource is a free text indentifier. The producer must provide slot to "fill" resource.

#### Resource Types
* `Single`    - 1 - A element, which can transport only in one piece (eg. `EmptyEngeryCell`)
* `Container` - 2 - Classical resource like `SteelPlate` or `Ice` (have amount *Quantity* and *Volume* per unit)
* `Liquid`    - 3 - Liquid like `H2` (*Volume* per unit is always `1`)
* `Battery`   - 4 - An element similar to `Single`, but there battery state will be checked. "Produced" is true, when battery is
                    fill charged. (eg. `ChargedEneryCell`)

## Radio Transmissions
[B] == Broad cast    
[U] == Unicast
### Register producer
* [B] P-Station> `RegisterProducer|<Resource Name>|<Station-EntityID>|<Station-GridID>|{Single|Conatiner|Liquid}|<Volume>|<Waypoint>`
* [U] Manager< `ProducerRegistered|<Resource Name>|<Manager-EntityID>`
### Register consumer
* [B] C-Station> `RegisterConsumer|<Resource Name>|<Station-EntityID>|<Station-GridID>|<Waypoint>`
* [U] Manager< `ConsumerRegistered|<Resource Name>|<Manager-EntityID>`
### Repeat registrations
* [B] Manager> `ReRegisterProducer`
* [B] Manager> `ReRegisterConsumer`
### Stock update
* [U] P-Station> `UpdateResourceStock|<Resource Name>|<Quantity>|<Reservation Quantity>|<Station-EntityID>|<Waypoint>`
### Request and Order resource
* [U] C-Station> `RequestResource|<Resource Name>|<Quantity>|<Waypoint>`
* [U] Manager to Producer> `OrderResource|<Resource Name>|<Quantity>`
### Request mission
* [B]> `RequestMission|<Requester Station-ID>|<Mission-ID>|<Drone Type>|<Producer Waypoint>|<Producer Station-GridID>|<Consumer Waypoint>|<Consumer Station-GridID>`
### Delivery update
* [U] C-Station> `ResourceDelivered|<Resource Name>|<Quantity>|<Waypoint>`
### Complete mission
* [B]< `MissionCompleted|<Mission-ID>`