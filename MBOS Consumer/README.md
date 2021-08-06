# MBOS Resource Consumer

## Working idea
Consumes resources, like `ChargedEnergyCell`, `IronIngot`, `SteelPlate`, `H2`, etc.

A typical factory has Consumer and Producer.

## Radio Transmissions
See description of Resource Manager.

## Limitat request of resources
Idea: A ie. refinary refines ores to ingot. So it has never ore on stock.
      But any day, the ingot container will be full and the refinary still request ores...

To avoid overloading of produced items, we limit the request, by stock of produced items:

    Limit Ore/Iron 4000 Ingot/Iron

This will avoid Iron Ore request, when more(or equal) than 4000 pieces of
Iron Ingot are on stock.