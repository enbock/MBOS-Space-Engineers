# MBOS Resource Manager

## Working idea
Manage a catalog of network available and needed resources.

### Process to request resource
1. Consumer request amount of resource
1. Manager reserve resource and order by producer
   1. Manager create "transport mission" for mission manager
1. Producer reserve and do stock update
   1. When mission took resources, Product update stock again and reduce Reservation Quantity
   1. Mission deliver resource to Consumer

### Resources 
Resource is a free text indentifier. The producer must provide slot to "fill" resource.

#### Resource Types
* `Single` - 1 - A element, which can transport only in one piece (eg. `EmptyEngeryCell`, `ChargedEneryCell`)
* `Container` - 2 - Classical resource like `SteelPlate` or `Ice` (have amount *Quantity* and *Volume* per unit)
* `Liquid`- 3 - Liquid like `H2` (*Volume* per unit is always `1`)

## Radio Transmissions
[B] == Broad cast    
[U] == Unicast
### Register producer
* [B] Station> `RegisterProducer|<Resource Name>|<Station-EntityID>|{Single|Conatiner|Liquid}|<Volume>|<Waypoint>`
* [U] Manager< `ProducerRegistered|<Resource Name>|<Manager-EntityId>`
### Register consumer
* [B] Station> `RegisterConsumer|<Resource Name>|<Station-EntityID>|<Waypoint>`
* [U] Manager< `ConsumerRegistered|<Resource Name>|<Manager-EntityId>`
### Repear registrations
* [B] Manager> `ReRegisterProducer`
* [B] Manager> `ReRegisterConsumer`
### Stock update
* [U] Station> `UpdateResourceStock|<Resource Name>|<Quantity>|<Reservation Quantity>|<Station-EntityID>|<Waypoint>`
### Order resource
* [U] Manager> `OrderResource|<Resource Name>|<Quantity>`
### Request resource
* [U] Station> `RequestResource|<Resource Name>|<Quantity>`