# MBOS Resource Consumer

## Working idea
Consumes resources, like `ChargedEnergyCell`, `Ingot/Iron`, `SteelPlate`, `H2`, etc.

A typical factory has Consumer and Producer.

## Radio Transmissions
See description of Resource Manager.

## Limit request of resources
Idea: A ie. refinary refines ores to ingot. So it has never ore on stock.
      But any day, the ingot container will be full and the refinary still request ores...

To avoid overloading of produced items, we limit the request, by stock of produced items.

This will avoid Iron Ore request, when more(or equal) than 4000 pieces of
Iron Ingot are on stock.


## Setup through connectors (Version 2.*)
Configuration of the Consumption will now pass through the CustomData of the connector.

Format:
```
Consume=<Name of product> <Type of cargo [Single|Battery|Container]> <Amount of product>
```

Limit-Format:
```
LimitBy=<Amount of product> <Name of product>
```
Notice: Limits are global and need only once to be defined. (One connector with limit, will limit other connector with same resource too.)

Examples:
```
Consume=EmptyEnergyCell Single 1
Consume=ChargedEnergyCell Battery 1
Consume=Ore/Iron Container 8000
Consume=EmptyContainer Single 1

LimitBy=4000 Ingot/Iron
```