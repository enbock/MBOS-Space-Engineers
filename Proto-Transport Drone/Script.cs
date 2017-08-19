const String VERSION = "1.0.0";
const String DATA_FORMAT = "1.0";

/**
* Key value memory.
*/
public class ConfigValue
{ 
    public String Key; 
    public String Value; 
     
    public ConfigValue(String data)  
    { 
        var parts = data.Split('='); 
        Key = parts[0]; 
        Value = parts[1]; 
    } 
    public ConfigValue(String key, string value)  
    { 
        Key = key; 
        Value = value; 
    } 
     
    public override String ToString()
    { 
        return Key + '=' + Value; 
    } 
}
/**
* Load storage into config memory.
*/
public void LoadConfigFromCustomData()
{ 
    
    string data = Me.CustomData;
    
    if (data.Length > 0) { 
        String[] configs = data.Split('\n'); 
        
        if(configs[0] != "FORMAT v" + DATA_FORMAT) {
            Echo("Error: Config is not in Format: FORMAT v" + DATA_FORMAT);
            return;
        }
        
        for(int i = 1; i < configs.Length; i++) {
            String line = configs[i]; 
            if (line.Length > 0) {
                string[] parts = line.Split('=');
                if(parts.Length != 2) continue;
                GetConfig(parts[0].Trim()).Value = parts[1].Trim();
            }
        } 
    } 
} 
 
/**
* Search/Create a config memory block.
*/
public ConfigValue GetConfig(String key) {
    ConfigValue config = Config.Find(x => x.Key == key);
    if(config != null) return config;
     
    ConfigValue newValue = new ConfigValue(key, String.Empty); 
    Config.Add(newValue); 
    return newValue; 
} 

// The central configuration.
List<ConfigValue> Config = new List<ConfigValue>(); 

string mode = "done";
IMyShipConnector connector;
IMyRemoteControl ctrlFlight;
IMyRemoteControl ctrlDock;
string need = "NONE";
string targetPosition = "NONE";
Vector3D targetVector = new Vector3D();
Vector3D offsetFlight = new Vector3D();
Vector3D offsetDock = new Vector3D();
bool isLocked = false;
double TargetTolerance = 4.0;
Dictionary<string,string> nextPosition = new Dictionary<string,string>();
Dictionary<string,string> action = new Dictionary<string,string>();

// Received positions
Dictionary<string, List<Vector3D>> receivedPosition = new Dictionary<string, List<Vector3D>>();

public Program() {
}

public void initProgram() {
    LoadConfigFromCustomData();

    connector = GetBlockByName(GetConfig("Connector").Value) as IMyShipConnector;
    ctrlFlight = GetBlockByName(GetConfig("FlightControl").Value) as IMyRemoteControl;
    ctrlDock = GetBlockByName(GetConfig("DockControl").Value) as IMyRemoteControl;

    LoadMap(nextPosition, GetConfig("NextPosition").Value);
    LoadMap(action, GetConfig("Action").Value);
}

public void LoadMap(Dictionary<string, string> list, string data) {
    list.Clear();
    string[] mapList = data.Split(',');
    foreach(string map in mapList ) {
        string[] values = map.Split(':');
        if(values.Length == 2) {
            list.Remove(values[0]); // ignore double values and overwrite
            list.Add(values[0], values[1]);
        }
    }
}

public void Save() {
    // Nothing yet
}

public void Main(string argument) {
    if(connector == null || ctrlFlight == null || ctrlDock == null || nextPosition.Count == 0 ||  action.Count == 0) {
        initProgram();
        if(connector == null || ctrlFlight == null || ctrlDock == null || nextPosition.Count == 0 ||  action.Count == 0) {
            Echo("Missing one of the configuration Values: 'Connector', 'FlightControl', 'DockControl', 'NextPosition', 'Action");
            Echo("  Syntax of NextPosition:  FROM:TO,FROM:TO,...");
            Echo("  Syntax of NextactionPosition:  POSITION:ACTION,POSITION:ACTION,...");
            Echo("     Actions available: LOAD, CHARGE");
            return;
        }
    }

    string[] args = argument.Split('|');
    Echo("INCOMING:"+args[0]);

    isLocked = connector.Status == MyShipConnectorStatus.Connected;

    offsetFlight = connector.GetPosition() - ctrlFlight.GetPosition();
    offsetDock = connector.GetPosition() - ctrlDock.GetPosition();

    switch(args[0])
    {
        case "r":
            mode = "none";
            need = "NONE";
            targetPosition = "NONE";
            DisableAutoPilot();
            break;

        // Undock test
        case "u":
            mode = "none";
            need = "UNDOCK";
            if(args.Length > 1) targetPosition = args[1];
            DisableAutoPilot();
            break;
        // Port dock test
        case "d":
            mode = "none";
            need = "DOCK";
            if(args.Length > 1) targetPosition = args[1];
            DisableAutoPilot();
            break;

        default:
            // Store received corrdinates
            if(args.Length == 7) { 
                Vector3D flightTarget = new Vector3D(Double.Parse(args[1]), Double.Parse(args[2]), Double.Parse(args[3]));
                Vector3D dockTarget = new Vector3D(Double.Parse(args[4]), Double.Parse(args[5]), Double.Parse(args[6]));
                List<Vector3D> list = new List<Vector3D>();
                list.Add(flightTarget);
                list.Add(dockTarget);
                receivedPosition.Remove(args[0]);
                receivedPosition.Add(args[0], list);

                // detect if it current position.
                if(isLocked && targetPosition == "NONE" && Vector3D.Distance(ctrlFlight.GetPosition(), dockTarget - offsetDock) < TargetTolerance) {
                    targetPosition = args[0];
                }
            }
            break;

    }

    // Run commands
    switch(need)
    {
        case "DOCK":
            DoFlightAndDock();
            break;
        case "UNDOCK":
            DoUndock();
            break;
        case "CHARGE":
            DoChargeAction();
            break;
        case "LOAD":
            DoLoadAction();
            break;
    }

    // Decider
    if (mode == "done") {
        DisableAutoPilot();

        // Nächster befehl
        switch(need)
        {
            case "UNDOCK":
                need = "NONE";
                if(nextPosition.ContainsKey(targetPosition)) {
                    need = "DOCK";
                    mode = "none";
                    targetPosition = nextPosition[targetPosition];
                }
                break;
            case "DOCK":
                if(action.ContainsKey(targetPosition)) {
                    need = action[targetPosition];
                    mode = "none";
                }
                break;
            case "NONE":
                if(action.ContainsKey(targetPosition)) {
                    need = action[targetPosition];
                    mode = "none";
                }
                break;
            case "CHARGE":
                need = "UNDOCK";
                mode = "none";
                break;
            case "LOAD":
                need = "UNDOCK";
                mode = "none";
                break;
        }
    }

    Echo("Distance: " + Math.Round(Vector3D.Distance(ctrlFlight.GetPosition(), targetVector), 2));
    Echo("Need: " + need); 
    Echo("Target: " + targetPosition); 
    Echo("Mode: " + mode);
    Echo("Locked: " + (isLocked ? "yes" : "no"));
    Echo("Recieved: ");
    foreach( KeyValuePair<string, List<Vector3D>> pair in receivedPosition ) {
        string next = "";
        if (nextPosition.ContainsKey(pair.Key)) {
            next = " -> " +nextPosition[pair.Key];
        }
        Echo ("    > " + pair.Key + next);
    }

}

public void DoFlightAndDock()
{
    if(! receivedPosition.ContainsKey(targetPosition)) {
        Echo("Wait for position " + targetPosition);
        return;
    }
    List<Vector3D> targets = receivedPosition[targetPosition];

    Vector3D flightTarget = new Vector3D(targets[0].X, targets[0].Y, targets[0].Z);
    Vector3D dockTarget = new Vector3D(targets[1].X, targets[1].Y, targets[1].Z);

    flightTarget -= offsetFlight;
    dockTarget -= offsetDock;

    bool flightOn = false;
    bool dockOn   = false;
    bool isReached;
    switch(mode) 
    {
        case "none":
            if (!isLocked) {
                Echo ("Go in flight..."); 
                targetVector = flightTarget;
                ctrlFlight.ClearWaypoints();
                ctrlFlight.AddWaypoint(targetVector, "Flight to");
                flightOn = true;
                mode = "flight";
            }
            break;
        case "flight":
            isReached = Vector3D.Distance(ctrlFlight.GetPosition(), targetVector) <= TargetTolerance;
            flightOn = !isReached;

            if (isReached) {
                Echo ("Go in docking..."); 
                
                targetVector = dockTarget;
                dockOn = true;
                mode = "docking";
                ctrlDock.ClearWaypoints();
                ctrlDock.AddWaypoint(targetVector, "Dock");
            }
            break;
        case "docking":
            Vector3D DockDifference = targetVector - ctrlDock.GetPosition();
            Echo("D: "+DockDifference);
            if (connector.Status == MyShipConnectorStatus.Connectable) {
                Echo ("Locking"); 
                if(connector.Status != MyShipConnectorStatus.Connected) connector.GetActionWithName("SwitchLock").Apply(connector);
                mode = "goCharge";
            } else {
                if (!ctrlDock.IsAutoPilotEnabled) {
                    ctrlDock.ClearWaypoints();
                    Vector3D newPos = dockTarget - DockDifference * 5;
                    if(Vector3D.Distance(ctrlFlight.GetPosition(), newPos) < 20) {
                        targetVector = newPos;
                    } else {
                        // ups...to much accumulated...back to target.
                        targetVector = dockTarget;
                    }
                    Echo("Correct dock position...");
                    ctrlDock.AddWaypoint(targetVector, "Dock(Corrected)");
                    dockOn = true;
                }
                dockOn = true;
            }
            break;
        case "goCharge":
            if (isLocked) {
                switchEngines(false);
                switchBatteries();
                mode = "done";
            }
            break;
    }
    if(ctrlFlight.IsAutoPilotEnabled != flightOn) 
    {
        Echo("AutoPilot Flight "+(dockOn?"enabled":"disabled"));
        ctrlFlight.SetAutoPilotEnabled(flightOn);
    }
    // Don't trrn dock on, if flight already on
    dockOn = (flightOn && dockOn) ? false : dockOn;
    if(ctrlDock.IsAutoPilotEnabled != dockOn) {
        Echo("AutoPilot Docking "+(dockOn?"enabled":"disabled"));
        ctrlDock.SetAutoPilotEnabled(dockOn);
    }
}

public void DoUndock()
{
    if(! receivedPosition.ContainsKey(targetPosition)) {
        Echo("Wait for position " + targetPosition);
        return;
    }
    List<Vector3D> targets = receivedPosition[targetPosition];

    Vector3D flightTarget = new Vector3D(targets[0].X, targets[0].Y, targets[0].Z);
    Vector3D offsetFlight = connector.GetPosition() - ctrlFlight.GetPosition();

    bool isReached;
    bool flightOn = false;
    switch(mode)
    {
        case "none":
            if(isLocked) {
                switchEngines(true);
                switchBatteries();
                connector.GetActionWithName("SwitchLock").Apply(connector);
                flightOn = false;
            } else {
                ctrlFlight.ClearWaypoints();
                targetVector = flightTarget - offsetFlight;
                ctrlFlight.AddWaypoint(targetVector, "Undock");
                flightOn = true;
                mode="flight";
            }
            break;

        case "flight":
            isReached = Vector3D.Distance(ctrlFlight.GetPosition(), targetVector) <= TargetTolerance * 2;;
            flightOn = !isReached;
            if(isReached) {
                mode = "done";
            }
            break;
    }
    ctrlFlight.SetAutoPilotEnabled(flightOn);
}

public void switchEngines(bool enabled)
{
    List<IMyThrust> thrusters = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters);
    for(var i = 0; i < thrusters.Count; i++) {
        IMyThrust thuster = thrusters[i];
        if (thuster.CubeGrid  == Me.CubeGrid) {
            thuster.GetActionWithName(enabled ? "OnOff_On" : "OnOff_Off").Apply(thuster);
            thuster.CustomName = "Thruster [" + (enabled ? "On" : "Off") + "]";
        }
    }
}

public void switchBatteries()
{
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
    for(var i = 0; i < batteries.Count; i++) {
        IMyBatteryBlock battery = batteries[i];
        if (battery.CubeGrid  == Me.CubeGrid) {
            battery.GetActionWithName("Recharge").Apply(battery);
        }
    }
}

/**
* Get specific block.
* <param name="name">Name of block.</param>
*/
public IMyTerminalBlock GetBlockByName(string name)
{
    // The Block inventory.
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(name, blocks);
    
    for(int i = 0; i < blocks.Count; i++) {
        IMyTerminalBlock block = blocks[i];
        if (block.CubeGrid  == Me.CubeGrid && block.CustomName == name) {
            return block;
        }
    }
    
    return null;
}

/**
 * Turn off auto pilot
 */
public void DisableAutoPilot()
{
    if(ctrlFlight.IsAutoPilotEnabled) ctrlFlight.SetAutoPilotEnabled(false);
    if(ctrlDock.IsAutoPilotEnabled) ctrlDock.SetAutoPilotEnabled(false);
}


public void DoChargeAction()
{
    // Check cargo
    List<IMyCargoContainer> cargo = new List<IMyCargoContainer>();
    GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargo);
    bool isEmpty = true;
    double left = 0.0;
    foreach(IMyCargoContainer container in cargo) {
        if(container.CubeGrid == Me.CubeGrid) {
            IMyInventory inventory = container.GetInventory(0);
            isEmpty = inventory.GetItems().Count ==  0;
            if(!isEmpty) {
                left += (double)inventory.CurrentVolume;
            }
        }
    }
    Echo("Cargos are " + (isEmpty ? "empty." : "not empty (" + Math.Round(left*1000.0,2) + " L left)."));


    // Check battery
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
    bool isCharged = true;
    double power = 0;
    foreach(IMyBatteryBlock battery in batteries) {
        if(battery.CubeGrid == Me.CubeGrid) {
            isCharged = battery.CurrentStoredPower ==  battery.MaxStoredPower;
            if(!isCharged) {
                power += (double)battery.MaxStoredPower - (double)battery.CurrentStoredPower;
            }
        }
    }
    Echo("Batteries are " + (isCharged ? "charged." : "not full (" + Math.Round(power,2) + " MWh left)."));

    mode = "wait";
    if (isEmpty && isCharged) {
        mode = "done";
    }
}

public void DoLoadAction() 
{
    // Check cargo
    List<IMyCargoContainer> cargo = new List<IMyCargoContainer>();
    GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargo);
    bool isFull = true;
    double left = 0.0;
    foreach(IMyCargoContainer container in cargo) {
        if(container.CubeGrid == Me.CubeGrid) {
            IMyInventory inventory = container.GetInventory(0);
            isFull = (double)inventory.CurrentVolume >=  (double)inventory.MaxVolume * 0.8;
            if(!isFull) {
                left += (double)inventory.MaxVolume - (double)inventory.CurrentVolume;
            }
        }
    }
    Echo("Cargos are " + (isFull ? "full." : "not full (" + Math.Round(left*1000.0,2) + " L left)."));

    mode = isFull ? "done" : "wait";
}
