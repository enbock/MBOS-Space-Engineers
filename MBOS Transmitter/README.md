# MBOS Transmitter
Send and receive message from the antenna.

# Requirements
On grid installed:
* Core
* Bus

# Installation
Run the script with the name of the antenna.
Setup the antenna to the program block of this script.

# Transmitter Format
The message data will be prefixed with an timestamp:

		<TIMESTAMP>|<MESSAGE DATA>

The timestamp will be removed, before it passthrough to Bus.