# MBOS Bus and Core Searcher
The template script which find all Core and Bus modules on the grid.

# Reference
## Module
The module class.

### Properties
#### Block
The reference to the `IMyProgrammableBlock`.
#### Type
The type of the Block: `Core` or `Bus`.
#### Cores
The list of cores in that the bus is registered.
#### ConfigLCD
The lcd panel if the core has one.

### Methods
#### Module(IMyProgrammableBlock block)
Constructor of `Module` object.

#### ToString()
Returns the `BlockId` of the module.

## FindCores()
Returns a list of all found cores as a module list.

Parameters: _none_
Return: `List<Module>`

## FindBuses(cores)
Returns a list of all found busses as a module list.
The bus needs to be registered in a core of the `cores` list, otherwise will it ignores the bus.

Parameters: 
* `List<Module>` cores List of existant cores.

Return: `List<Module>`