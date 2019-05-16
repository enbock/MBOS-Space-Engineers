# MBOS Core
The core module which handles the execution rythm and stores the configuration.

## Installation
## Requirements
* Timer Block
 * Setup Programmable Block with `run` action and **empty** argument.
* Display Block (optional since v2.0)

## The first run
On the first run, set the `argument` with follow syntax

    <Name of Display>[, <Name of timer block>]
    
 Example:
 
    MBOS: Debug, MBOS: Timer

## Configuration
All existent configuration will are modifyable on the custom data.

One the configuration is set, the core store it on the process storage and is
imedialy available on cores first run after loading the level(no booting time
needed).

### Data format v0.3
Generic format syntax:

    FORMAT v0.3
    <key>=<value>
    
Lists inside of config:

    <key>=<value>[#<value>]
    
Is the `FORMAT` tag in other format, then the whole configuration will be
cleared.

### Config Values
#### Display 
since: v2.0.0 

`BlockId` of the config screen. 

#### MainTimer 
since: v0.2.0 

`BlockId` of the Timer Block over that the MBOS Core is running. 

#### RunMode 
since: v0.2.0 

Value Syntax: `{fast|normal|call|callFast}` 

Switch between fast and normal operation mode.    
(since v2.0.0) The call modes are stopping the timer if no call in stack.

#### RegisteredModules 
since: v0.3.0 

List of registered modules. 

#### CallStack
since: v1.1.0

The list of call which are scheduled for next round. 
All other module, which need to call a module, have to add his request to that config.

Value Syntax: `<BlockId>~<Data>[#<BlockId>~<Data>]...`

## API System
The whole MBOS system used API URN in follow syntax:

    API://<Action>[/<Parameter>...]
    
Syntax of `BlockId`:

    <Number in Grid>|<Type of Block>
    
The block identifier is independend from the `CustomName`. So the block
can be renamed without loosing the connection to the modules.

## Module Registration System
### Register a module

    API://RegisterModule/<BlockId> 

Sender: Module to Core 

Request a register of a module with `BlockId` on the Core. 

### Registration feedback

    API://Registered/<BlockId>/<LCDId>
     
Direction: Core to Module 

Response of register request. `BlockId` is the Core identifier. The
last value `LCDId` is the config LCD identifier. 

### Remove a module

    API://RemoveModule/<BlockId>
     
Direction: Module to Core
 
Request the removal of a registerd module with `BlockId` from the Core.
Non existant module will be ignored. 

### Feedback of removal

    API://Removed/<BlockId>
 
Direction: Core to Module 

Response after module was removed from Core with `BlockId`. 

## Time triggered execution
Once a module were registered, it will be executed by the core with follow
argument:

    API://ScheduleEvent/<BlockId>/<RunCount>
    
Direction: Core to Module 

Time based invoke of the module. 

* `BlockId` is the Core which invokes. 
* `RunCount` is the loop counter number of Core.
