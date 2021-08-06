/*
The X-World Energy Cell Controller.

This controlling script is made of a "Mobil Battery", named "Energy Cell".
Expected is / Setup:
* One Connector with CustomData: 'Loader' (with 0.3% magnetic streng)
* One Connector with CustomData: 'Power' (with default or up to 0.3% magnetic streng)
* Batteries
* One Programmable Block for this script
* Depend of Energy Cell weight adjust magnetic streng and `PullStreng` variable (0.3% is similar to 0.003f `PullStreng`)

The stations:
* Any connector, connected on the Power-Connector, with empty CustomData are Energy Consuming Stations.
* Any connector, connected on the Power-Connector, with non empty CustomData are Energy Charging Stations.
* Any connector, connected on the Loader-Connector, are Transporters

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
const String VERSION = "1.2.1";

IMyTextSurface textSurface;
List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
IMyShipConnector Loader;
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

    List<IMyShipConnector> Connectors = new List<IMyShipConnector>();
    GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(Connectors);

    foreach (IMyShipConnector connector in Connectors) {
        if (connector.CubeGrid != Me.CubeGrid) continue;
        if (connector.CustomData == "Loader") Loader = connector;
        if (connector.CustomData == "Power") Power = connector;
    }

    textSurface = Me.GetSurface(0);
    //Echo("Have:" + Me.SurfaceCount);

    if(Loader != null && Power != null && Batteries.Count > 0) {
        textSurface.ContentType = ContentType.TEXT_AND_IMAGE;
        textSurface.ClearImagesFromSelection();
        //textSurface.AddImageToSelection("Grid");
        textSurface.ChangeInterval = 0;
        Loader.Enabled = true;
        Power.Enabled = true;

        textSurface.WriteText("*****", false);

        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    }
    //List<string> sprites = new List<string>();    textSurface.GetSprites(sprites);    Echo("Co: " +string.Join("\n", sprites.ToArray()) );
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

String modeDispaly = "---";

public void Main(string argument, UpdateType updateSource)
{
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
    bool isLoaderConnected = Loader.Status == MyShipConnectorStatus.Connected;
    bool isLoaderInRange = Loader.Status == MyShipConnectorStatus.Connectable;
    if (isPowerConnected) { 
        LastWasConsumerConnected = Power.OtherConnector.CustomData == "";
    }

    textSurface.WriteText(charge.ToString() + "\n", false);

    if (ActionCounter > MaxCounter) {
        /*if (isLoaderConnected) {
           modeDispaly = "=>=";
        }*/
        if (isPowerConnected && LastWasConsumerConnected) {
            modeDispaly = ">>>";
            SetBatteryMode(ChargeMode.Discharge);
        } 
        if (isPowerConnected && !LastWasConsumerConnected) {
            modeDispaly = "<<<";
            SetBatteryMode(ChargeMode.Recharge);
        } 
        /*if (isPowerInRange && charge <= MinCharge) {
            modeDispaly = "===";
        }*/

        if (isLoaderConnected && !isPowerInRange && !isPowerConnected) {
            WaitForAway = false;
        }
        
        if (isLoaderConnected && ((isPowerInRange && !WaitForAway) || isPowerConnected)) {
            modeDispaly = "=>+";
            Loader.PullStrength = 0f;
            Power.PullStrength = PullStrength;
            Loader.Disconnect();
            ActionCounter = 0;
        }
        
        if (isPowerConnected && charge <= MinCharge && LastWasConsumerConnected) {
            modeDispaly = "-==";
            Loader.PullStrength = PullStrength;
            Power.PullStrength = PullStrength;
            Power.Disconnect();
            ActionCounter = 0;
            WaitForAway = true;
        }
        
        if (isLoaderInRange && isPowerInRange && charge <= 100f && LastWasConsumerConnected && WaitForAway) {
            modeDispaly = "-<=";
            Power.PullStrength = 0f;
            Loader.PullStrength = 0f;
            Loader.Connect();
            ActionCounter = 0;
        } else if (!isLoaderConnected && isPowerInRange && !isPowerConnected && !WaitForAway) {
            modeDispaly = "+==";
            Loader.PullStrength = 0f;
            Power.Connect();
            ActionCounter = 0;
        } else if (isLoaderConnected && isPowerInRange && charge <= MinCharge && LastWasConsumerConnected && !WaitForAway) {
            modeDispaly = "+<=";
            Loader.PullStrength = 0f;
            Power.PullStrength = PullStrength / 10f;
            Loader.Disconnect();
            ActionCounter = 0;
        } else if (isLoaderInRange && isPowerConnected && charge >= 95f && !LastWasConsumerConnected) {
            modeDispaly = "+>=";
            Power.PullStrength = 0f;
            Loader.PullStrength = PullStrength;
            SetBatteryMode(ChargeMode.Discharge);
            Power.Disconnect();
            WaitForAway = true;
            ActionCounter = 0;
        } else if (isLoaderInRange && isPowerInRange && !LastWasConsumerConnected && charge > MinCharge && WaitForAway) {
            modeDispaly = "==+";
            Loader.PullStrength = 0f;
            Loader.PullStrength = 0f;
            Loader.Connect();
            ActionCounter = 0;
        } else if (isLoaderInRange && !isPowerInRange && !isPowerConnected) {
            modeDispaly = "==*";
            Loader.PullStrength = 0f;
            Loader.PullStrength = 0f;
            Loader.Connect();
            ActionCounter = 0;
        }

        if (charge <= MinCharge) {
            Power.CustomName = "Connector: Power (empty)";
        } else {
            Power.CustomName = "Connector: Power (charged)";
        }

        EnableThrusters(Loader.Status == MyShipConnectorStatus.Connected);
    }

    textSurface.WriteText(modeDispaly + "\n", true);

    if (isLoaderConnected) {
        textSurface.WriteText("*", true);
    } else if (isLoaderInRange) {
        textSurface.WriteText("+", true);
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