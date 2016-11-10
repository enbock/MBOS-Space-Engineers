# MBOS Module Template
The solar panel finder.

Searches in the grid and connected grids for solar panel fields.

## How it Searches
First its index all solar pannel which are in one grid and group them.
As second it searches for rotor head which having a other rotor head in the
group of the rotor.

        +-------+ +------+ +-------+   <-- Panels
          ##########/\###########   <-- Solar Grid
                    ()   <-- Rotor (Pitch Axis(Y))
                    /\   <-- Head
                    ()   <-- Rotor (Yaw Axis(Z))
                   ####
                  ###### <-- Base Grid

When the 2nd rotor is in the same grid as the core, then is the solar fields
construction accepted.

# Requirements
On grid installed:
* Grid Observer

# Installation
* Run it once in a program block without arguments.