# MBOS Core Searcher
The template script which find all Core modules on the grid.

# Reference
## Module
The module class.

### Properties
#### Block
The reference to the `IMyProgrammableBlock`.

### Methods
#### Module(IMyProgrammableBlock block)
Constructor of `Module` object.

#### ToString()
Returns the `BlockId` of the module.

## findCores()
Returns a list of all found cores as a module list.

Parameters: _none_
Return: `List<Module>`