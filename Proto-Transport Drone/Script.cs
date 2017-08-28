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
double TargetTolerance = 15.0;
Dictionary<string,string> nextPosition = new Dictionary<string,string>();
Dictionary<string,string> action = new Dictionary<string,string>();
int LoadActionCounter = 0;
double FlightDistance = 0.0;
int TimeAfterDock = 0;
int TimeAfterUndock = 0;
int TimeAfterFlight = 0;

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
    InitLightShow();
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
            Echo("  Syntax of Action:  POSITION:ACTION,POSITION:ACTION,...");
            Echo("     Actions available: LOAD, CHARGE");
            return;
        }
    }

    string[] args = argument.Split('|');
    Echo("INCOMING:"+args[0]);

    isLocked = connector.Status == MyShipConnectorStatus.Connected;

    Vector3D relationPosition = connector.CubeGrid.GridIntegerToWorld(connector.Position + new Vector3I(0, -2, 0));
    offsetFlight = relationPosition - ctrlFlight.GetPosition();
    offsetDock = relationPosition - ctrlDock.GetPosition();

    TimeAfterDock++;
    TimeAfterUndock++;
    TimeAfterFlight++;

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
                } else if(!isLocked && targetPosition == "NONE") {
                    // flight to first received position after restart the script
                    targetPosition = args[0];
                    need = "DOCK";
                    mode = "none";
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

    // Decider for next command
    if (mode == "done") {
        DisableAutoPilot();

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
                LoadActionCounter = 0;
                TimeAfterDock =  0;
                if(action.ContainsKey(targetPosition)) {
                    need = action[targetPosition];
                    if(need == "CHARGE") {
                        switchBatteries(false);
                    }
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
                TimeAfterUndock =  0;
                break;
            case "LOAD":
                need = "UNDOCK";
                mode = "none";
                TimeAfterUndock =  0;
                break;
        }
    }

    FlightDistance = Vector3D.Distance(ctrlFlight.GetPosition(), targetVector);
    LightShow();
    /* 
    Echo ("AfterFlight: " + TimeAfterFlight);
    Echo ("AfterDock: " + TimeAfterDock);
    Echo ("AfterUndock: " + TimeAfterUndock);
    */
    Echo("Distance: " + Math.Round(FlightDistance, 2));
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
                mode = "docking";
                TimeAfterFlight = 0;
            }
            break;
        case "docking":
            if (connector.Status == MyShipConnectorStatus.Connectable) {
                if(connector.Status != MyShipConnectorStatus.Connected) connector.GetActionWithName("SwitchLock").Apply(connector);
                mode = "goCharge";
            } else {
                Vector3D DockDifference = dockTarget - ctrlDock.GetPosition();
                double distance = Vector3D.Distance(ctrlDock.GetPosition(), dockTarget);
                ctrlDock.ClearWaypoints();
                targetVector = dockTarget;
                ctrlDock.AddWaypoint(targetVector, "Dock " + Math.Round(distance));
                dockOn = true;
            }
            break;
        case "goCharge":
            if (isLocked) {
                switchEngines(false);
                mode = "done";
            }
            break;
    }
    if(ctrlFlight.IsAutoPilotEnabled != flightOn) 
    {
        Echo("AutoPilot Flight "+(flightOn?"enabled":"disabled"));
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
                switchBatteries(true);
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

/**
 * Switch thruster on/off.
 */
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

/**
 * Turn batteries on/off.
 * Off means recharge and On means discharge.
 */
public void switchBatteries(bool enabled)
{
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
    for(var i = 0; i < batteries.Count; i++) {
        IMyBatteryBlock battery = batteries[i];
        if (battery.CubeGrid  == Me.CubeGrid) {
            battery.OnlyRecharge = !enabled;
            battery.OnlyDischarge = enabled;
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
            isEmpty = isEmpty && (double)inventory.GetItems().Count == 0;
            left +=  (double)inventory.CurrentVolume;
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
            isCharged = isCharged && battery.CurrentStoredPower ==  battery.MaxStoredPower;
            power += (double)battery.MaxStoredPower - (double)battery.CurrentStoredPower;
        }
    }
    Echo("Batteries are " + (isCharged ? "charged." : "not full (" + Math.Round(power,2) + " MWh left)."));

    mode = "wait";
    if (isEmpty && isCharged) {
        mode = "done";
    }
}

double LoadActionOldLeft = 0.0;
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
            isFull = isFull && (double)inventory.CurrentVolume >=  (double)inventory.MaxVolume * 0.8;
            left += (double)inventory.MaxVolume - (double)inventory.CurrentVolume;
        }
    }
    Echo("Cargos are " + (isFull ? "full." : "not full (" + Math.Round(left*1000.0,2) + " L left)."));

    LoadActionCounter++;
    if(left != LoadActionOldLeft) {
        LoadActionCounter = 0;
        LoadActionOldLeft = left;
    }

    Echo("Start countdown: " + (120 - LoadActionCounter));
    //(i) Also, limit to 120s to wait, after last cargo change
    mode = isFull || LoadActionCounter > 120 ? "done" : "wait";
}

List<IMyInteriorLight> AnimatedLights = new List<IMyInteriorLight>();
List<IMyReflectorLight> SpotLights = new List<IMyReflectorLight>();
List<IMyLightingBlock> ConnectorLights = new List<IMyLightingBlock>();
public void InitLightShow()
{
    IMyLightingBlock light;
    String lightPrefix = GetConfig("AnitmatedLightPrefix").Value;
    if(lightPrefix == "") {
        Echo("No 'AnitmatedLightPrefix' found.");
        return;
    }
    AnimatedLights.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyInteriorLight>(AnimatedLights);
    if(AnimatedLights.Count == 0) {
        Echo("No Anitmated Lights found.");
        return;
    }
    for(int i = AnimatedLights.Count -1; i>= 0; i--) {
        light = AnimatedLights[i];
        if(light.CubeGrid != Me.CubeGrid || light.CustomName.IndexOf(lightPrefix, StringComparison.Ordinal) != 0) {
            AnimatedLights.Remove(light as IMyInteriorLight);
        }
    }
    
    GridTerminalSystem.GetBlocksOfType<IMyReflectorLight>(SpotLights);
    for(int i = SpotLights.Count -1; i>= 0; i--) {
        light = SpotLights[i];
        if(light.CubeGrid != Me.CubeGrid) {
            SpotLights.Remove(light as IMyReflectorLight);
        }
    }

    String connectorLightName = GetConfig("ConnectorLight").Value;
    GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(ConnectorLights);
    for(int i = ConnectorLights.Count -1; i>= 0; i--) {
        light = ConnectorLights[i];
        if(light.CubeGrid != Me.CubeGrid || light.CustomName != connectorLightName) {
            ConnectorLights.Remove(light);
        }
    }
}


public void LightShow()
{
    int i;

    Color color = new Color(255, 255, 255);
    // colo settings
    if(TimeAfterUndock == 0 || isLocked) {
        color = new Color(0,255,0);
    }  else if (!(mode == "flight" && need == "DOCK")) {
        color = new Color(255, 255, 0);
    }

    // Interval settings
    float newInterval = (float)FlightDistance / 20f;
    newInterval = newInterval > 2f ? 2f : newInterval;
    newInterval = newInterval <  0.2f ?  0.2f : newInterval;
    if(TimeAfterDock > 0 && isLocked) newInterval = 0f;

    // Apply
    for(i = 0; i< AnimatedLights.Count; i++) {
        IMyInteriorLight light = AnimatedLights[i];
        light.SetValue("Blink Interval", newInterval);
        light.SetValue("Color", color);
    }

    // Turn on/off spot lights
    bool isON = !isLocked && ((mode == "docking" && FlightDistance > 15 ) || (need == "DOCK" && mode == "none") || mode == "flight" || need == "UNDOCK");
    for(i = 0; i < SpotLights.Count; i++) {
        IMyReflectorLight spotLight = SpotLights[i];
        spotLight.GetActionWithName(isON ? "OnOff_On" : "OnOff_Off").Apply(spotLight);
    }

    // Turn on/off connector lights
    isON = !isLocked  && mode == "docking";
    for(i = 0; i < ConnectorLights.Count; i++) {
        IMyLightingBlock connectorLight = ConnectorLights[i];
        connectorLight.GetActionWithName(isON ? "OnOff_On" : "OnOff_Off").Apply(connectorLight);
    }
}