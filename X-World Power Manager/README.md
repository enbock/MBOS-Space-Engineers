# X-World Power Manager

## Working idea
Station don't generate his own power(They have Emergency Batter or Power Generation for run Computer and Ports).    
So the Station uses external "Energy Cell". This Energy Cell will be connected to station.

If a battery empty, its disconnect itself. That will detect the Power Manager. He will Broadcast
the last known Energy Cell connector with Replace request.

### Replace process
1. Request to cell removal.
1. Wait that connector is freed (nothing in range)
1. Request a new battery

## Installation
Add the script and register the Charge Connectors.

## Energy Slots
Connector for Battery. 

## Radio Transmissions
### Remove Enegery Cell
`Resource|RemoveEnergyCell|<GridID>`
### Add Enegery Cell
`Resource|NeedEnergyCell|<GridID>`
