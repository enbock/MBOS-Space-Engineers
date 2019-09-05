# MBOS Transmitter
IGC-Grid to IGC-Antenna relay.    
Can also be used as replay between different IGC networks (defined by channel name).

# Transmitter Format
The message data will be prefixed with an timestamp:

		<TIMESTAMP>|<MESSAGE DATA>

The timestamp will be removed, before it passthrough local grid.