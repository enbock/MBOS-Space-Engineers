/*
The X-World Energy Cell Controller.

This controlling script is made of a "Mobil Battery", named "Energy Cell".
Expected is / Setup:
* One Merge Block with CustomData: 'Loader'
* One Connector with CustomData: 'Power' (with default or up to 0.3% magnetic streng)
* Batteries
* One Programmable Block for this script
* Depend of Energy Cell weight adjust magnetic streng and `PullStreng` variable (0.3% is similar to 0.003f `PullStreng`)

The definitions:
* Any connector, connected on the Power-Connector, with empty CustomData are Energy Consuming Stations.
* Any connector, connected on the Power-Connector, with any CustomData set are Energy Charging Stations.
* Any merge block, connected on the Loader-Merge-Block, are Transporters

How it works:
The Energy Cell automatic disconnected from Consuming Stations, when battery charge under `MinCharge` percent.
The Engery Cell automatic connects to the transporter, when they empty or full charged.
Also the Energy Cell automatic connects to the Charging and Consumer Stations.
The script set batteries automatic to Recharge or Discharge, depend on case.

Workflow:
* Battery connect on consumer station and goes empty.
* Transporter take the Energy Cell and bring it to an charger station.
* After Battery is full charged, the transport takes the Energy Cell and transport it to an Consumer Station.

Attantion: The batteries need thrusters. Otherwise drone can not transport them safely.
*/
const String VERSION = "2.0.0";

IMyTextSurface textSurface;
List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
IMyShipMergeBlock Loader;
IMyShipConnector Power;
float MinCharge = 10f;
float PullStrength = 0.003f;
bool LastWasConsumerConnected = false;
bool WaitForAway = false;
int ActionCounter = 0;
int MaxCounter = 2;

public Program()
{
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);

    foreach (IMyBatteryBlock battery in batteries) {
        if (battery.CubeGrid != Me.CubeGrid) continue;
        Batteries.Add(battery);
    }

    List<IMyShipConnector> connectors = new List<IMyShipConnector>();
    GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(
        connectors,
        (IMyShipConnector connector) => connector.CubeGrid == Me.CubeGrid && connector.CustomData == "Power"
    );
    if (connectors.Count > 0) Power = connectors[0];
    else Echo("'Power' connector not found.");

    List<IMyShipMergeBlock> mergeBlocks = new List<IMyShipMergeBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(
        mergeBlocks,
        (IMyShipMergeBlock mergeBlock) => mergeBlock.CubeGrid == Me.CubeGrid && mergeBlock.CustomData == "Loader"
    );
    if (mergeBlocks.Count > 0) Loader = mergeBlocks[0];
    else Echo("'Loader' merge block not found.");

    textSurface = Me.GetSurface(0);

    if(Loader != null && Power != null && Batteries.Count > 0) {
        textSurface.ContentType = ContentType.TEXT_AND_IMAGE;
        textSurface.ClearImagesFromSelection();
        textSurface.ChangeInterval = 0;
        Loader.Enabled = true;
        Power.Enabled = true;

        textSurface.WriteText("*****", false);

        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    } else {
        Echo("Program start failed.");
    }
}

public void Save()
{
}

public void EnableThrusters(bool enabled)
{
    List<IMyThrust> thrusters = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters, (IMyThrust trhuster) => trhuster.CubeGrid.EntityId == Me.CubeGrid.EntityId);

    thrusters.ForEach((IMyThrust thruster) => thruster.Enabled = enabled);
}

String modeDisplay = "---";
DateTime Mark = DateTime.Now;
DateTime LoadEnableAt = DateTime.Now;
DateTime DisableThrustersAt = DateTime.Now;
bool powerDisconnectNext = false;


public void DisconnectLoader()
{
    LoadEnableAt = DateTime.Now.AddSeconds(10);
    DisableThrustersAt = DateTime.Now.AddSeconds(8);
    Mark = DateTime.Now.AddSeconds(5);
    Loader.Enabled = false;
}

public void Main(string argument, UpdateType updateSource)
{
    if (Mark >= DateTime.Now) return;
    if (LoadEnableAt < DateTime.Now && Loader.Enabled == false) Loader.Enabled = true;
    if (DisableThrustersAt < DateTime.Now && !Loader.IsConnected) EnableThrusters(false);
    if(powerDisconnectNext) {
        powerDisconnectNext = false;
        Power.Disconnect();
    }

    float charge = 0f;
    float max = 0f;
    float current = 0f;

    ActionCounter++;
    
    foreach (IMyBatteryBlock battery in Batteries) {
        max += battery.MaxStoredPower;
        current += battery.CurrentStoredPower;
    }

    charge = (float)Math.Round(100f / max * current, 2);

    bool isPowerConnected = Power.Status == MyShipConnectorStatus.Connected;
    bool isPowerInRange = Power.Status == MyShipConnectorStatus.Connectable;
    bool isLoaderConnected = Loader.IsConnected;

    if (isPowerConnected) { 
        LastWasConsumerConnected = Power.OtherConnector.CustomData == "";
    }

    textSurface.WriteText(charge.ToString() + "\n", false);

    if (ActionCounter > MaxCounter) {
        if (isPowerConnected && LastWasConsumerConnected) {
            modeDisplay = ">>>";
            SetBatteryMode(ChargeMode.Discharge);
        } 
        if (isPowerConnected && !LastWasConsumerConnected) {
            modeDisplay = "<<<";
            SetBatteryMode(ChargeMode.Recharge);
        }

        if (isLoaderConnected && !isPowerInRange && !isPowerConnected) {
            WaitForAway = false;
        }
        
        if (isPowerConnected && charge <= MinCharge && LastWasConsumerConnected) {
            modeDisplay = "-==";
            Loader.Enabled = true;
            SetBatteryMode(ChargeMode.Discharge);
            Power.PullStrength = PullStrength;
            powerDisconnectNext = true;
            Mark = DateTime.Now.AddSeconds(1);
            ActionCounter = 0;
            WaitForAway = true;
        }
        
        if (isLoaderConnected && isPowerInRange && charge <= 100f && LastWasConsumerConnected && WaitForAway) {
            modeDisplay = "-<=";
            Power.PullStrength = 0f;
            ActionCounter = 0;
        } else if (!isLoaderConnected && isPowerInRange && !isPowerConnected && !WaitForAway) {
            modeDisplay = "+==";
            DisconnectLoader();
            Power.Connect();
            ActionCounter = 0;
        } else if (isLoaderConnected && isPowerInRange && !isPowerConnected && charge <= MinCharge && LastWasConsumerConnected && !WaitForAway) {
            modeDisplay = "+<=";
            Power.PullStrength = PullStrength / 10f;
            DisconnectLoader();
            ActionCounter = 0;
        } else if (isLoaderConnected && isPowerConnected && charge >= 95f && !LastWasConsumerConnected) {
            modeDisplay = "+>=";
            Power.PullStrength = 0f;
            SetBatteryMode(ChargeMode.Discharge);
            powerDisconnectNext = true;
            Mark = DateTime.Now.AddSeconds(1);
            WaitForAway = true;
            ActionCounter = 0;
        } else if (isLoaderConnected && isPowerInRange && !LastWasConsumerConnected && charge > MinCharge && WaitForAway) {
            modeDisplay = "==+";
            ActionCounter = 0;
        } else if (isLoaderConnected && !isPowerInRange && !isPowerConnected) {
            modeDisplay = "==*";
            ActionCounter = 0;
        }

        if (charge <= MinCharge) {
            Power.CustomName = "Connector: Power (empty)";
        } else {
            Power.CustomName = "Connector: Power (charged)";
        }
    }

    textSurface.WriteText(modeDisplay + "\n", true);

    if (isLoaderConnected) {
        textSurface.WriteText("*", true);
    //} else if (isLoaderInRange) {
    //    textSurface.WriteText("+", true);
    } else {
        textSurface.WriteText("-", true);
    }

    if (isPowerConnected) {
        textSurface.WriteText("*", true);
    } else if (isPowerInRange) {
        textSurface.WriteText("+", true);
    } else {
        textSurface.WriteText("-", true);
    }

    if (WaitForAway) {
        textSurface.WriteText("W", true);
    } else {
        textSurface.WriteText("-", true);
    }
    if (LastWasConsumerConnected) {
        textSurface.WriteText("v", true);
    } else {
        textSurface.WriteText("^", true);
    }

    Echo("Battery charge by " + charge + "%.");
}

public void SetBatteryMode(ChargeMode mode) 
{
    foreach (IMyBatteryBlock battery in Batteries) {
        battery.ChargeMode = mode;
    }
}