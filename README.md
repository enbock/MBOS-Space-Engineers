# MBOS-Space-Engineers
Space Engineers: Message Bus Operating System

A modularized message controlled system through multiple programable blocks. 
http://steamcommunity.com/workshop/filedetails/?id=686991041

## History
While to creation of the modules code over the years, Space Engineers began to provide similar functionality.      
The **Message Bus** was now integrate as `IGC` (Inter Grid Communication). So is it comming, that the `Bus` module is not needed anymore.     
Also the integration of the `CustomData` made the `Core` module obsolete. The integration of the LCD display in the
`Programmable Block` made the usage of a LCD-Panel obsolate.

Currently(2020) the modules now more independend (like mirco service architecture), but a small MBOS-Framework is crystalized, the is
copied from module to module. Be aware, that "older" modules has possibly also an older MBOS system class inside.

## Idea 
The programmable block are very limtied for complex system, e.g. automatic navigation and cargo loading systems. 
The MBOS allows to develop multiple small modules which interacts together through a message bus. 

The system is inspired by $IOS: 
http://steamcommunity.com/workshop/filedetails/?id=535981104 

## Working on it... 
The system used MBOS as prefix. Anyone in the workshop are welcome to add modules.   

### Methodology
Each Module represent one resposibility.

The IGC is used as data **bus** to transport message between multiple modules.    
The architecture of the antenna network define the size and domain of a MBOS network.

The LCD Display of the `Programmable Block` will be used for status info.     
The `CustomData` contains the config data of the module.

Here a short ACSII art as overview:
```
Example: Module 2 send data to Module 1.

                                   +----------------+
                                   | SE Game Engine | -----------------+
                                   +----------------+                  |
                                                                       |
                                                             Execute with time event
                                                                       |
                                                                       v
                           +------+                              +----------+       +----------------------+
                           | IGC  | <------ 1. Send data ------  | Module 1 | <---> | CustonData as Config | 
                           +------+                              +----------+       +----------------------+
                              |            
                              |                                  +----------+       +----------------------+
                              +-------- 2.Receive data --------> | Module 2 | <---> | CustonData as Config | 
                                                                 +----------+       +----------------------+
```

### Starting a new module
The start of a module could a bit complicated, may just take the latest modified module, remove all special classes and reuse
the SE functions and the MBOS class.

The MBOS class contains functionality to save, load and read/write config entries for `String` and `Block`.

## Add-Ons 
The MBOS will work on full vanilla and don't need any Add-Ons or extensions in Space Engineers.
