# MBOS Resource Manager

## Working idea
Produce resources, like `EmptyEnergyCell`, `IronIngot`, `SteelPlate`, `H2`, etc.

A typical factory has Consumer and Producer.

## Radio Transmissions
See description of Rsource Manager.

## Setup through connectors (Version 3.*)
Configuration of the Consumption will now pass through the CustomData of the connector.

Format:
```
Produce=<Name of product> <Type of cargo [Single|Battery|Container]> <Amount of product to load>
```

Limit-Format:
```
LimitBy=<Amount of product> <Name of product>[, ...]
```
Notice: Limits are global and need only once to be defined. (One connector with limit, will limit other connector with same resource too.)

Examples:
```
Produce=ChargedEnergyCell Battery 1
Produce=EmptyEnergyCell Single 1
Produce=Ore/Iron Container 8000
Produce=EmptyContainer Single 1
```