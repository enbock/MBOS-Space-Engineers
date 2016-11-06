# MBOS Structure Observer
This module observes the amount of cubes of the grid and connected grids.
If a change detectected, it will send follow events:
* `GridChanged` - The grid was modified.

# Events
## GridChanged
Data: `global` or `local`.
`global` means, that the full grid, also with connected grids, was changed. (Example case: Ship has connected or removed.)
`local` indicates, that only the local/own grid was changes. (Example case: Block was added or removed.) 

# Requirements
Follow modules a need to be installed on grid.
* MBOS Core
* MBOS Bus

# Install
Run module once without arguments.

# Uninstall
Run module once with argument `UNINSTALL`.