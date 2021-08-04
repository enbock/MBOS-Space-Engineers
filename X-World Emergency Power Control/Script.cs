const String NAME = "EmergencyPowerManager";
const String VERSION = "1.0.1";
const String DATA_FORMAT = "1";

public class EmergencyPowerManager
{
    private Sys sys;
    protected List<IMyBatteryBlock> BatteryList = new List<IMyBatteryBlock>();

    public EmergencyPowerManager(Sys system) {
        sys = system;

        long gridId = sys.Me.CubeGrid.EntityId;
        BatteryList.Clear();
        sys.GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(BatteryList, (IMyBatteryBlock block) => 
            block.CubeGrid.EntityId == gridId && block.CustomData == "Emergency Power"
        );
        if(BatteryList.Count < 2) {
            sys.Echo("Need at least 2 Batteries with 'Emergency Power' in CustomData.");
            return;
        }
        BatteryList[0].ChargeMode = ChargeMode.Auto;
        BatteryList[1].ChargeMode = ChargeMode.Auto;
    }

    public void Check() {
        if (BatteryList.Count < 2) return;
        IMyBatteryBlock lower = BatteryList[0].CurrentStoredPower > BatteryList[1].CurrentStoredPower ? BatteryList[1] : BatteryList[0];
        IMyBatteryBlock upper = BatteryList[0].CurrentStoredPower > BatteryList[1].CurrentStoredPower ? BatteryList[0] : BatteryList[1];
        upper.ChargeMode = ChargeMode.Auto;
        lower.ChargeMode = ChargeMode.Recharge;
    }
}

public class Sys 
{
    public IMyProgrammableBlock Me;
    public IMyGridTerminalSystem GridTerminalSystem;
    public Action<string> Echo;
    public IMyTextSurface ComputerDisplay;

    public Sys(IMyProgrammableBlock me, IMyGridTerminalSystem gridTerminalSystem,  Action<string> echo) {
        Me = me;
        GridTerminalSystem = gridTerminalSystem;
        Echo = echo;
        ComputerDisplay = Me.GetSurface(0);
        ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
        ComputerDisplay.ClearImagesFromSelection();
        ComputerDisplay.ChangeInterval = 0;
    }
}

EmergencyPowerManager EPM;
Sys sys;

public Program()
{
    sys = new Sys(Me, GridTerminalSystem, Echo);
    Runtime.UpdateFrequency = UpdateFrequency.Update100;

    InitProgram();
    UpdateInfo();
}

public void Save()
{
}

public void InitProgram() 
{
    EPM = new EmergencyPowerManager(sys);

    Echo("Program initialized.");
}

int counter = 0;
public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (EPM == null) {
        InitProgram();
    }

    if (EPM == null) {
        return;
    }

    if (counter == 0) EPM.Check();
    counter++;
    if(counter > 10) counter = 0;

    UpdateInfo();
}

public void UpdateInfo()
{
    String output = "[X-World] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
    ;
    sys.ComputerDisplay.WriteText(output, false);
}

public void ReadArgument(String args) 
{
    if (args == String.Empty) return;
     
    List<String> parts = new List<String>(args.Split(' ')); 
    String command = parts[0].Trim();
    parts.RemoveAt(0);
    String allArgs = String.Join(" ", parts.ToArray());
    switch (command) {
        default:
            Echo("Available Commands: none");
            break;
    }
}

