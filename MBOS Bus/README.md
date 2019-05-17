# MBOS Bus
The message bus module.

The bus allows to register a module for a special event. Once the event was
dispatched, will the registered modules will be executed with the data send
by the dispatcher.

## API Syntax
### Register a module for an event

    API://AddListener/<EventName>/<BlockId>

Register the `BlockId` module as observer for the `EventName`.
A registration will be answered with:

    API://ListenerAdded/<EventName>/<BlockId>

The `BlockId` of the answer is the bus module identifier.

### Remove a module from an event

    API://RemoveListener/<EventName>/<BlockId>

Remove the `BlockId` module from the observation list. Already removed modules
will be ignored.
The removal will be answered with:

    API://ListenerRemoved/<EventName>/<BlockId>

The `BlockId` of the answer is the bus module identifier.

### Dispatch an event

    API://Dispatch/<EventName>/<BlockId>/<Data>
    
Dispatches the event of `EventName`. The `BlockId` indecates the dispatcher.
The `Data` and `BlockId` will send to all observers. The `Data` can conatins
slashes(`/`).
The observers will be triggered with follow API call:

    API://Dispatched/<EventName>/<DispatcherId>/<BlockId>/<Data>
    
The `DispatcherId` is the block identifier which originally starts the
dispatching. The `BlockId` is the bus module identifier. The `Data` is the
data given by the `Dispatch` call.

## Configuration
All registered modules are been stored on the progress storage and available
from the first call(no booting sequence needed).
The bus module is searching automatically the core(s) on the first run. It
register itself on each found core module.

## Installation
* Build a Programmable Block.
* Load this script into.

It is not needed, to add this module to any Timer Block.

## Deinstallation
Execute the core once with argument `UNINSTALL`.
That will start a deregistration of all module. After that the core storage
is cleaned.
The registered modules receive the removal from core. They should uninstall
his own module relations. Finally should stay all module in fresh state for
reinstallation.