# MBOS Structure Observer
This module observes the amount of cubes of the grid and connected grids.
If a change detectected, it will send follow events:
* `GridChanged` - The grid was modified.

# Events
## Dispatching
### GridChanged
Data: `<global count>|<local count>`
Inform about the current state after a grid change.
The first number is the global amount of cubes. The second one is the local(on my grid) amount.

## Listening
### GridRefresh
Send the current grid state again (GridChanged event).

# Requirements
Follow modules a need to be installed on grid.
* MBOS Core
* MBOS Bus

# Install
Run module once without arguments.

# Uninstall
Run module once with argument `UNINSTALL`.