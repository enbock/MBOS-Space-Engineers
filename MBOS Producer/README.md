# MBOS Resource Manager

## Working idea
Produce resources, like `EmptyEnergyCell`, `IronIngot`, `SteelPlate`, `H2`, etc.

A typical factory has Consumer and Producer.

## Radio Transmissions
See description of Rsource Manager.

## Single Unit Behavior
Comman: `SingleUpdateStockWhen {Connected|Connectable|Unconnected}`
The check of unit with type `Single` can be modified by this command. Its define the state when "produced" is happend.     
* `Connected` - Produced is when the port connected is
* `Connectable` - Produced when port is connectable. (eg. for `EmptyEnergyCells` or all movable and self disconnecting elements whcih need to be lifted)
* `Unconnected` - Produced when nothing in range of connector ... 

Info: Be aware that misstyping of these settings let crash the script (press just the _recompile_ button in case).