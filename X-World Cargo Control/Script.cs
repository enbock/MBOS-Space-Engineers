/*
The X-World Cargo Controller.

This controlling script is made of a "Cargo Unit".
Expected is / Setup:
* One Merge Block with CustomData: 'Loader'
* One Connector with CustomData: 'Cargo' (with default or up to 0.3% magnetic streng)
* Medium Cargo Container
* One Programmable Block for this script
* A small battery to keep script alive
* Some Thrusters to help moving the cargo

The stations:
* Stations has to fill or empty the cargo.

*/
const String VERSION = "2.0.3";

IMyTextSurface textSurface;
IMyShipMergeBlock Loader;
IMyShipConnector Cargo;
float PullStrength = 0.003f;
string LastConnected = "";
ChargeMode lastBatteryMode = ChargeMode.Recharge;

public Program()
{
    List<IMyShipConnector> connectors = new List<IMyShipConnector>();
    GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(
        connectors,
        (IMyShipConnector connector) => connector.CubeGrid == Me.CubeGrid && connector.CustomData == "Cargo"
    );
    if (connectors.Count > 0) Cargo = connectors[0];
    else Echo("'Cargo' connector not found.");

    List<IMyShipMergeBlock> mergeBlocks = new List<IMyShipMergeBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(
        mergeBlocks,
        (IMyShipMergeBlock mergeBlock) => mergeBlock.CubeGrid == Me.CubeGrid && mergeBlock.CustomData == "Loader"
    );
    if (mergeBlocks.Count > 0) Loader = mergeBlocks[0];
    else Echo("'Loader' merge block not found.");

    textSurface = Me.GetSurface(0);

    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries, (IMyBatteryBlock battery) => battery.CubeGrid == Me.CubeGrid);
    if(Loader != null && Cargo != null && batteries.Count > 0) {
        textSurface.ContentType = ContentType.TEXT_AND_IMAGE;
        textSurface.ClearImagesFromSelection();
        textSurface.ChangeInterval = 0;
        Loader.Enabled = true;
        Cargo.Enabled = true;

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

public void DisconnectLoader()
{
    LoadEnableAt = DateTime.Now.AddSeconds(10);
    DisableThrustersAt = DateTime.Now.AddSeconds(8);
    Loader.Enabled = false;
}

public void Main(string argument, UpdateType updateSource)
{
    if (Mark >= DateTime.Now) return;
    if (LoadEnableAt < DateTime.Now && Loader.Enabled == false) Loader.Enabled = true;
    if (DisableThrustersAt < DateTime.Now && !Loader.IsConnected) EnableThrusters(false);

    bool isCargoConnected = Cargo.Status == MyShipConnectorStatus.Connected;
    bool isCargoInRange = Cargo.Status == MyShipConnectorStatus.Connectable;
    bool isLoaderConnected = Loader.IsConnected;

    textSurface.WriteText("SCU\n", false);

    if (isCargoInRange || isLoaderConnected) {
        if (lastBatteryMode == ChargeMode.Recharge) {
            SetBatteryMode(ChargeMode.Auto);
            // need way some frames for battery switch
            return;
        }
    } else if (isLoaderConnected || isCargoConnected) {
        SetBatteryMode(ChargeMode.Recharge);
    } else {
        SetBatteryMode(ChargeMode.Auto);
    }

    if (isLoaderConnected) {
        modeDisplay = "=-O";
    }
    if (isCargoConnected) {
        modeDisplay = "<=>";
    }

    if (isLoaderConnected && isCargoInRange && !isCargoConnected && LastConnected == "loader") {
        modeDisplay = "=>+";
        Cargo.PullStrength = PullStrength;
        SetBatteryMode(ChargeMode.Auto);
        DisconnectLoader();
    }
    if (!isLoaderConnected && isCargoInRange && !isCargoConnected && LastConnected == "loader") {
        modeDisplay = "==+";
        Cargo.Connect();
        DisconnectLoader();
    }
    if(isCargoConnected && isLoaderConnected && LastConnected == "cargo") {
        modeDisplay = "+<=";
        Cargo.PullStrength = 0f;
        Cargo.Disconnect();
    }
    if (!isCargoConnected && !isLoaderConnected && LastConnected == "cargo") {
        modeDisplay = "+==";
    }
    if(LastConnected != "cargo" && LastConnected != "loader") { // init
        SetBatteryMode(ChargeMode.Auto);
        if(isCargoInRange) Cargo.Connect();
    }

    if(isCargoConnected && (!isLoaderConnected || LastConnected == "")) LastConnected = "cargo";
    if(isLoaderConnected && !isCargoInRange && !isCargoConnected) LastConnected = "loader";

    textSurface.WriteText(modeDisplay + "\n", true);

    if (isLoaderConnected) {
        textSurface.WriteText("*", true);
    //} else if (isLoaderInRange) {
    //    textSurface.WriteText("+", true);
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
    } else if (LastConnected == "loader") {
        textSurface.WriteText("<", true);
    } else {
        textSurface.WriteText("?", true);
    }

    Echo("Controller is running.");
}

public void SetBatteryMode(ChargeMode mode) 
{
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries, (IMyBatteryBlock battery) => battery.CubeGrid == Me.CubeGrid);
    batteries.ForEach(
        delegate (IMyBatteryBlock battery) {
            battery.ChargeMode = mode;
        }
    );
    lastBatteryMode = mode;
}