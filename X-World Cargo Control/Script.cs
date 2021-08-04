/*
The X-World Cargo Controller.

This controlling script is made of a "Cargo Unit".
Expected is / Setup:
* One Connector with CustomData: 'Loader' (with 0.3% magnetic streng)
* One Connector with CustomData: 'Cargo' (with default or up to 0.3% magnetic streng)
* Medium Cargo Container
* One Programmable Block for this script
* A small battery to keep script alive
* Some Thrusters to help moving the cargo

The stations:
* Stations has to fill or empty the cargo.

Attantion: The cargo need thrusters. Otherwise drone can not transport them safely.
*/
const String VERSION = "1.0.0";

IMyTextSurface textSurface;
List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
IMyShipConnector Loader;
IMyShipConnector Cargo;
float MinCharge = 10f;
float PullStrength = 0.003f;
int ActionCounter = 0;
int MaxCounter = 2;
string LastConnected = "";

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
        if (connector.CustomData == "Cargo") Cargo = connector;
    }

    textSurface = Me.GetSurface(0);

    if(Loader != null && Cargo != null && Batteries.Count > 0) {
        textSurface.ContentType = ContentType.TEXT_AND_IMAGE;
        textSurface.ClearImagesFromSelection();
        textSurface.ChangeInterval = 0;
        Loader.Enabled = true;
        Cargo.Enabled = true;

        textSurface.WriteText("*****", false);

        Runtime.UpdateFrequency = UpdateFrequency.Update100;
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

String modeDispaly = "---";

public void Main(string argument, UpdateType updateSource)
{
    ActionCounter++;

    bool isCargoConnected = Cargo.Status == MyShipConnectorStatus.Connected;
    bool isCargoInRange = Cargo.Status == MyShipConnectorStatus.Connectable;
    bool isLoaderConnected = Loader.Status == MyShipConnectorStatus.Connected;
    bool isLoaderInRange = Loader.Status == MyShipConnectorStatus.Connectable;

    textSurface.WriteText(charge.ToString() + "\n", false);

    if (ActionCounter > MaxCounter) {
        if (isLoaderConnected) {
           modeDispaly = "=-O";
        }
        if (isCargoConnected) {
            modeDispaly = "<=>";
        }

        if (isLoaderConnected && isCargoInRange && !isCargoConnected && LastConnected == "loader") {
            modeDispaly = "=>+";
            Loader.PullStrength = 0f;
            Cargo.PullStrength = PullStrength;
            SetBatteryMode(ChargeMode.Auto);
            Loader.Disconnect();
            ActionCounter = 0;
        }
        if (!isLoaderConnected && isLoaderInRange && isCargoInRange && !isCargoConnected && LastConnected == "loader") {
            modeDispaly = "==+";
            Cargo.Connect();
            ActionCounter = 0;
        }
        if(isCargoConnected && isLoaderInRange && !isLoaderConnected && LastConnected == "cargo") {
            modeDispaly = "+<=";
            Loader.PullStrength = PullStrength;
            Cargo.PullStrength = 0f;
            SetBatteryMode(ChargeMode.Auto);
            Cargo.Disconnect();
            ActionCounter = 0;
        }
        if (!isCargoConnected && isCargoInRange && isLoaderInRange && !isLoaderConnected && LastConnected == "cargo") {
            modeDispaly = "+==";
            Loader.Connect();
            ActionCounter = 0;
        }

        if (isLoaderConnected || isCargoConnected) {
            SetBatteryMode(ChargeMode.Recharge);
        } else {
            SetBatteryMode(ChargeMode.Auto);
        }
        EnableThrusters(Loader.Status == MyShipConnectorStatus.Connected);
    }

    if(isCargoConnected && (!isLoaderInRange || LastConnected == "")) LastConnected = "cargo";
    if(isLoaderConnected && !isCargoInRange) LastConnected = "loader";

    textSurface.WriteText(modeDispaly + "\n", true);

    if (isLoaderConnected) {
        textSurface.WriteText("*", true);
    } else if (isLoaderInRange) {
        textSurface.WriteText("+", true);
    } else {
        textSurface.WriteText("-", true);
    }

    if (isCargoConnected) {
        textSurface.WriteText("*", true);
    } else if (isCargoInRange) {
        textSurface.WriteText("+", true);
    } else {
        textSurface.WriteText("-", true);
    }

    textSurface.WriteText("-", true);

    if (LastConnected == "cargo") {
        textSurface.WriteText(">", true);
    } else {
        textSurface.WriteText("<", true);
    }

    Echo("Controller is running.");
}

public void SetBatteryMode(ChargeMode mode) 
{
    foreach (IMyBatteryBlock battery in Batteries) {
        battery.ChargeMode = mode;
    }
}