# MBOS-Space-Engineers
Space Engineers: Message Bus Operating System

A modularized message controlled system through multiple programable blocks. 
http://steamcommunity.com/workshop/filedetails/?id=686991041

## Idea 
The programmable block are very limtied for complex system, e.g. automatic navigation and cargo loading systems. 
The MBOS allows to develop multiple small modules which interacts together through a message bus. 

The system is inspired by $IOS: 
http://steamcommunity.com/workshop/filedetails/?id=535981104 

## Working on it... 
The system used MBOS as prefix. Anyone in the workshop are welcome to add modules.   

### Methodology
The `Core` module is the central interaction program. That handles the concurrency calls between
the modules and synchronize it. That is needed, because in one frame can a 
script(programable block) only run one time. Multiple calls seems be ignored by the engine.

The second element build the `Bus` module. That allows to send events across the module collection.

To add a call to the call list, the LCD panel of the `Core` is in use. Here is a config line with
the next calls. Each module can simple add more call here and the `Core` execute it when possible.

Here a short ACSII art as overview:
```
Example: Module 2 send data to Module 1.

  +---------------+         +------+
  | LCD as Config | <-----> | Core | ----------------------------------+
  +---------------+         +------+                                   |
         ^                                                             |
         |                                                 4. Execute with event data
3. Add call for Module 1                                               |
         |                                                             v
         |                 +------+                              +----------+
         +---------------- | Bus  | <--- 1. Register event ----  | Module 1 |
                           +------+                              +----------+
                              ^            
                              |                                  +----------+
                              +-- 2. Dispatch event with data -- | Module 2 |
                                                                 +----------+
```

Modules should use primary events to communicate.    
If a module is needed to observe automatically, then it should register on the `Core`. The 
`Core` triggers a special event to each registed module on each iteration.

### Starting a new module
The start of a module could a bit complicated, if we start from scratch. The `Module Template`
has some code for a start. Just duplicate that module and start to fill with new functionaly.

Also feel free to check the other modules in this repository and take some code from there to
build your own module more easy.

## Add-Ons 
The MBOS will work on full vanilla and don't need any Add-Ons or extensions in Space Engineers.
