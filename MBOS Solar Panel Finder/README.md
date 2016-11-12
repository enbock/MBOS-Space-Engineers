# MBOS Module Template
The solar panel finder.

Searches in the grid and connected grids for solar panel fields.

## How it Searches
First its index all solar pannel which are in one grid and group them.
Searches for the Yaw and Pitch motors by name.

        +-------+ +------+ +-------+   <-- Panels
          ##########/\###########   <-- Solar Grid
                    ()   <-- Rotor (Pitch Axis(Y)) must have suffix [SolarPitch#<NUMBER>]
                    /\   <-- Head
                    ()   <-- Rotor (Yaw Axis(Z)) must have suffix [SolarYaw#<NUMBER>]
                   ####
                  ###### <-- Base Grid

The solar fields construction accepted when `[SolarPitch#<NUMBER>]` and
`[SolarYaw#<NUMBER>]` found. Be sure that you named the motors with the
correct number. 

# Requirements
On grid installed:
* Grid Observer
* Yaw and Pitch motors correct labeled.

# Installation
* Run it in a program block without arguments.

TIP: To know the correct numbers, let run the script once, correct the
     numbers for the motors and run the script once again.

# Events
## Dispatching
### SolarGrids
The solar grids was updated. The data is numbers of the solar fields.