const String VERSION = "1.4.4";
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

string Mode = "done";
string Action = "NONE";
string TargetPosition = "NONE";
string ConfirmedActionData = String.Empty;
string RequestedAction = String.Empty;
string PossibleActionData = String.Empty;
float BatteryEmergencyLevel = 0.5f;
float BatteryRechargeLevel = 0.75f;

IMyShipConnector connector;
IMyRemoteControl CtrlFlight;
IMyRemoteControl CtrlDock;
IMyRemoteControl CtrlUnDock;

Vector3D TargetVector = new Vector3D();
Vector3D OffsetFlight = new Vector3D();
Vector3D OffsetDock = new Vector3D();
Vector3D OffsetUnDock = new Vector3D();
Vector3D FlightTarget = new Vector3D();
Vector3D DockTarget = new Vector3D();

bool IsLocked = false;
double TargetTolerance = 10.0;
int LoadActionCounter = 0;
double FlightDistance = 0.0;
int TimeAfterDock = 0;
int TimeAfterUndock = 0;
int TimeAfterFlight = 0;
int TimeAfter = 0;
string LoadedGoods = "";
string ActionOfDocked = "";

long Timestamp = 0L;

public Program() 
{
    initProgram();
}

public void initProgram() 
{
    LoadConfigFromCustomData();

    connector = GetBlockByName(GetConfig("Connector").Value) as IMyShipConnector;
    CtrlFlight = GetBlockByName(GetConfig("FlightControl").Value) as IMyRemoteControl;
    CtrlDock = GetBlockByName(GetConfig("DockControl").Value) as IMyRemoteControl;
    CtrlUnDock = GetBlockByName(GetConfig("UnDockControl").Value) as IMyRemoteControl;

    string value = GetConfig("TimeOut").Value;
    int TimeOut = Int32.Parse(value == String.Empty ? "0" : value);
    if (TimeOut < 1) {
        GetConfig("TimeOut").Value = "30";
    }

    InitLightShow();
}

/**
* Store config memory.
*/
public void Save() 
{ 
    Me.CustomData = FormatConfig(Config); 
} 

/**
* Convert config to storable string.
*/
public String FormatConfig(List<ConfigValue> config) 
{ 
    List<String> store = new List<String>();  
    int i; 
     
    for(i = 0; i < config.Count; i++) { 
        store.Add(config[i].ToString()); 
    } 
     
    return "FORMAT v" + DATA_FORMAT + "\n" + String.Join("\n", store.ToArray()); 
}

public void Main(string argument) 
{
    Timestamp = System.DateTime.Now.ToBinary();
    LoadConfigFromCustomData();
    if(connector == null || CtrlFlight == null || CtrlDock == null) {
        initProgram();
        if(connector == null || CtrlFlight == null || CtrlDock == null) {
            Echo("Missing one of the configuration Values: 'Connector', 'FlightControl', 'DockControl'");
            Save();
            return;
        }
    }
    if(CtrlUnDock == null) {
        CtrlUnDock = GetBlockByName(GetConfig("UnDockControl").Value) as IMyRemoteControl;
    }

    string[] args = argument.Split('|');
    //Echo(argument);

    IsLocked = connector.Status == MyShipConnectorStatus.Connected;

    Vector3D relationPosition = connector.CubeGrid.GridIntegerToWorld(connector.Position + new Vector3I(0, -2, 0));
    OffsetFlight = relationPosition - CtrlFlight.GetPosition();
    OffsetDock = relationPosition - CtrlDock.GetPosition();
    if (CtrlUnDock != null) {
        OffsetUnDock = relationPosition - CtrlUnDock.GetPosition();
    }

    TimeAfterDock++;
    TimeAfterUndock++;
    TimeAfterFlight++;
    TimeAfter++;

    if (argument.Trim() != String.Empty) {
        switch(args[0])
        {
            // reset
            case "r":
                Mode = "none";
                Action = "NONE";
                TargetPosition = "NONE";
                DisableAutoPilot();
                break;

            default:
                TimeAfter--;
                if (args[1] == MyName()) {
                    ReceiveCom(args);
                }
                break;

        }
    }

    // Run commands
    switch(Action)
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
        case "CARGO":
            DoCargoAction();
            break;
        case "REQUEST":
            DoRequestHandling();
            break;
    }

    // Decider for next command
    if (Mode == "done") {
        DisableAutoPilot();

        switch(Action)
        {
            case "UNDOCK":
                Action = "NONE";
                Mode = "none";
                ApplyConfirmedAction();
                break;
            case "DOCK":
                LoadActionCounter = 0;
                TimeAfterDock =  0;
                ApplyActionNow();
                Mode = "none";
                break;
            case "REQUEST":
                Action = "UNDOCK";
                Mode = "none";
                TimeAfterUndock =  0;
                break;
            default:
                RequestAction();
                break;
        }
    }

    FlightDistance = Vector3D.Distance(CtrlFlight.GetPosition(), TargetVector);
    LightShow();
    /* 
    Echo ("AfterFlight: " + TimeAfterFlight);
    Echo ("AfterDock: " + TimeAfterDock);
    Echo ("AfterUndock: " + TimeAfterUndock);
    Echo("After Count: " + TimeAfter);
    */
    Echo("NetID: " + MyName());
    Echo("Distance: " + Math.Round(FlightDistance, 2));
    Echo("Action: " + Action); 
    Echo("Target: " + TargetPosition); 
    Echo("Mode: " + Mode);
    Echo("Locked: " + (IsLocked ? "yes" : "no"));
}

public void RequestAction()
{
    RequestedAction = FindNextAction();
    Vector3D position = CtrlFlight.GetPosition() - OffsetFlight;
    Transmit("NEED|" + RequestedAction + "|" + position.X + "|" + position.Y + "|" + position.Z + "|" + MyName() + "|" + LoadedGoods);
    TimeAfter = 0;
    Action = "REQUEST";
    Mode = "requesting";
}

/**
 * Handle request processes.
 */
public void DoRequestHandling()
{
    string[] stack ;
    int TimeOut = Int32.Parse(GetConfig("TimeOut").Value);
    switch(Mode) {
        case "requesting":
            if (TimeAfter < TimeOut && GetBatteryChargeLevel() >= BatteryEmergencyLevel) break;

            // No data received...ask again.
            if (TimeAfter > TimeOut && PossibleActionData == String.Empty) {
                RequestAction();
                break;
            }

            if (PossibleActionData == String.Empty) break;

            stack = PossibleActionData.Split('|');

            Mode = "requirePort";
            TimeAfter = 0;
            Transmit(stack[4] + "|REQUEST|" + stack[1] + "|" + stack[2] + "|" + MyName());

            break;
        
        case "requirePort":
            if (TimeAfter > TimeOut) {
                // About after timeout
                PossibleActionData = String.Empty;
                RequestAction();
                break;
            }
            break;
    }

    Echo("Requested Action: " + RequestedAction);
    stack = PossibleActionData.Split('|');
    Echo("Selected: " +(stack.Length < 2 ? "Nothing" : "Port #" + stack[2] + " at " + stack[4]));
    stack = ConfirmedActionData.Split('|');
    Echo("Confirmed: " +(stack.Length < 2 ? "Nothing" : "Port #" + stack[2] + " at " + stack[4]));
    Echo("Counter:" +TimeAfter);
}

/**
 * Received intercom data.
 */
public void ReceiveCom(string[] stack)
{
    if (Action != "REQUEST" || Mode == "done") {
        /**
         * The transmitter duplicate the messages. If I received the 
         * "reserved" message  again, then I done the wrong action.
         */
        Echo("Duped message...");
        return;
    }
    stack = stack.Skip(1).ToArray(); // remove timestamp

    if (stack[2] == "DENIED") {
        Echo("[FAIL] Other one was faster.");
        PossibleActionData = String.Empty;
        ConfirmedActionData = String.Empty;
        RequestAction();
        return;
    }

    if (stack[3] == "RESERVED") {
        Echo("[OK] Reservation received.");
        ConfirmedActionData = String.Join("|", stack);
        PossibleActionData = String.Empty;
        Mode = "done";
        TimeAfter = 0;
        return;
    }

    if (Mode == "requesting" && stack[1] == RequestedAction) {
        if (PossibleActionData == String.Empty) {
            PossibleActionData = String.Join("|", stack);
            return;
        }
        string[] beforeAction = PossibleActionData.Split('|');
        if(Double.Parse(stack[3]) < Double.Parse(beforeAction[3])) {
            PossibleActionData = String.Join("|", stack);
            return;
        }
        return;
    } 
}

public void ApplyConfirmedAction()
{
    if (TargetPosition != "NONE") {
        Transmit(TargetPosition + "|RELEASED|" + MyName());
    }

    string[] args = ConfirmedActionData.Split('|');
    Action = "DOCK";
    TargetPosition = args[10];
    FlightTarget = new Vector3D(Double.Parse(args[4]), Double.Parse(args[5]), Double.Parse(args[6]));
    DockTarget = new Vector3D(Double.Parse(args[7]), Double.Parse(args[8]), Double.Parse(args[9]));
}

public void ApplyActionNow()
{
    LoadFindTry = 0;
    string[] args = ConfirmedActionData.Split('|');
    Action = args[1];
    ActionOfDocked = Action;
    switch(Action) {
        case "CHARGE":
            switchBatteries(false);
            break;
    }

    ConfirmedActionData = String.Empty;
}

bool FlightAndDockFlip = false;
double FlightAndDockOldDistance = 0.0;
public void DoFlightAndDock()
{
    bool flightOn = false;
    bool dockOn   = false;
    bool isReached;
    FlightAndDockFlip = !FlightAndDockFlip;
    switch(Mode) 
    {
        case "none":
            if (!IsLocked) {
                TargetVector = FlightTarget - OffsetFlight;
                CtrlFlight.ClearWaypoints();
                CtrlFlight.AddWaypoint(TargetVector, "Flight to");
                flightOn = true;
                Mode = "flight";
            }
            break;
        case "flight":
            isReached = Vector3D.Distance(CtrlFlight.GetPosition(), TargetVector) <= TargetTolerance;
            flightOn = !isReached;

            if (isReached) {
                Mode = "docking";
                TimeAfterFlight = 0;
            }
            break;
        case "docking":
            if (connector.Status == MyShipConnectorStatus.Connectable) {
                if(connector.Status != MyShipConnectorStatus.Connected) 
                    connector.GetActionWithName("SwitchLock").Apply(connector);
            } else if (IsLocked) {
                Mode = "locking";                
            } else {
                double distance = Vector3D.Distance(CtrlDock.GetPosition(), (DockTarget - OffsetDock));

                if (FlightAndDockOldDistance < distance) {
                    // Abort dock and try again
                    Mode = "none";
                }

                CtrlDock.ClearWaypoints();
                TargetVector = DockTarget - OffsetDock;
                CtrlDock.AddWaypoint(TargetVector, "Dock " + Math.Round(distance));
                dockOn = distance < 7.0 || FlightAndDockOldDistance == distance ? FlightAndDockFlip : true;
                FlightAndDockOldDistance = distance;
            }
            break;
        case "locking":
            if (IsLocked) {
                switchEngines(false);

                string[] args = ConfirmedActionData.Split('|');
                Transmit(TargetPosition + "|DOCKED|" + args[2] + "|" + MyName());

                Mode = "done";
            } else {
                Mode = "docking";
            }
            break;
    }
    if(CtrlFlight.IsAutoPilotEnabled != flightOn) 
    {
        Echo("AutoPilot Flight "+(flightOn?"enabled":"disabled"));
        CtrlFlight.SetAutoPilotEnabled(flightOn);
    }
    // Don't trrn dock on, if flight already on
    dockOn = (flightOn && dockOn) ? false : dockOn;
    if(CtrlDock.IsAutoPilotEnabled != dockOn) {
        Echo("AutoPilot Docking "+(dockOn?"enabled":"disabled"));
        CtrlDock.SetAutoPilotEnabled(dockOn);
    }
}

public void DoUndock()
{
    bool isReached;
    bool dockOn = false;

    IMyRemoteControl ctrlDock = CtrlDock;

    if(CtrlUnDock != null) {
        ctrlDock = CtrlUnDock;
    }

    switch(Mode)
    {
        case "none":
            if(IsLocked) {
                switchEngines(true);
                switchBatteries(true);
                connector.GetActionWithName("SwitchLock").Apply(connector);
                dockOn = false;
            } else {
                if (TargetPosition  == "NONE") {
                    Mode = "done";
                    break;
                }
                ctrlDock.ClearWaypoints();
                TargetVector = FlightTarget - OffsetFlight;
                ctrlDock.AddWaypoint(TargetVector, "Undock");
                dockOn = true;
                Mode="flight";
            }
            break;

        case "flight":
            isReached = Vector3D.Distance(ctrlDock.GetPosition(), TargetVector) <= TargetTolerance;
            dockOn = !isReached;
            if(isReached) {
                Mode = "done";
            }
            break;
    }
    ctrlDock.SetAutoPilotEnabled(dockOn);
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
    if(CtrlFlight.IsAutoPilotEnabled) CtrlFlight.SetAutoPilotEnabled(false);
    if(CtrlDock.IsAutoPilotEnabled) CtrlDock.SetAutoPilotEnabled(false);
    if(CtrlUnDock != null && CtrlUnDock.IsAutoPilotEnabled) CtrlUnDock.SetAutoPilotEnabled(false);
}


public void DoChargeAction()
{
    // Check battery
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
    bool isCharged = true;
    double power = 0;
    foreach(IMyBatteryBlock battery in batteries) {
        if(battery.CubeGrid == Me.CubeGrid) {
            isCharged = isCharged && battery.CurrentStoredPower == battery.MaxStoredPower;
            power += (double)battery.MaxStoredPower - (double)battery.CurrentStoredPower;
        }
    }
    Echo("Batteries are " + (isCharged ? "charged." : "not full (" + Math.Round(power,2) + " MWh left)."));

    Mode = "wait";
    if (isCharged) {
        Mode = "done";
    }
}

public void DoCargoAction()
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

    float batteryLevel = GetBatteryChargeLevel();

    Mode = "wait";
    if (isEmpty || batteryLevel < BatteryEmergencyLevel) {
        Mode = "done";
    }
}

public float GetBatteryChargeLevel()
{
    float max = 0f;
    float current = 0f;
    // Check battery
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
    foreach(IMyBatteryBlock battery in batteries) {
            if(battery.CubeGrid != Me.CubeGrid) continue;
            max += battery.MaxStoredPower;
            current += battery.CurrentStoredPower;
    }

    float batteryLevel = (1f / max) * current;

    Echo("Battery Charge:" + Math.Round(batteryLevel*100f, 2) +"% (min: " + Math.Round(BatteryEmergencyLevel * 100f, 2) + "%)");

    return batteryLevel;
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

    string value = GetConfig("LoadStartCountDown").Value;
    int countDown = Int32.Parse(value == String.Empty ? "0" : value);
    if (countDown < 1) {
        GetConfig("LoadStartCountDown").Value = "120";
        countDown = 120;
    }

    
    float batteryLevel = GetBatteryChargeLevel();

    Echo("Start countdown: " + (countDown - LoadActionCounter));
    //(i) Also, limit to 120s to wait, after last cargo change
    Mode = isFull || LoadActionCounter > countDown || batteryLevel < BatteryEmergencyLevel ? "done" : "wait";
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
    if(TimeAfterUndock == 0 || IsLocked) {
        color = new Color(0,255,0);
    }  else if (!(Mode == "flight" && Action == "DOCK")) {
        color = new Color(255, 255, 0);
    }

    // Interval settings
    float newInterval = (float)FlightDistance / 20f;
    newInterval = newInterval > 2f ? 2f : newInterval;
    newInterval = newInterval <  0.2f ?  0.2f : newInterval;
    if(TimeAfterDock > 0 && IsLocked) newInterval = 0f;

    // Apply
    for(i = 0; i< AnimatedLights.Count; i++) {
        IMyInteriorLight light = AnimatedLights[i];
        light.SetValue("Blink Interval", newInterval);
        light.SetValue("Color", color);
    }

    // Turn on/off spot lights
    bool isON = !IsLocked && ((Mode == "docking" && FlightDistance > 15) || Mode != "docking") ;
    for(i = 0; i < SpotLights.Count; i++) {
        IMyReflectorLight spotLight = SpotLights[i];
        spotLight.GetActionWithName(isON ? "OnOff_On" : "OnOff_Off").Apply(spotLight);
    }

    // Turn on/off connector lights
    isON = !IsLocked  && Mode == "docking";
    for(i = 0; i < ConnectorLights.Count; i++) {
        IMyLightingBlock connectorLight = ConnectorLights[i];
        connectorLight.GetActionWithName(isON ? "OnOff_On" : "OnOff_Off").Apply(connectorLight);
    }
}

/**
 * Send data to first radio antenna.
 */
public void Transmit(String data)
{
    List<IMyRadioAntenna> antennaList = new List<IMyRadioAntenna>();
    GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(antennaList);
    foreach(IMyRadioAntenna antenna in antennaList) {
        if (antenna.CubeGrid == Me.CubeGrid && antenna.IsBroadcasting) {
            antenna.TransmitMessage(Timestamp + "|" + data);
            Echo("Transmit: " + data);
            return;
        }
    }
    Echo("No active antenna to transmit found.");
}

/**
 * Get my name (station grid id).
 */
public string MyName()
{
    return "" + Me.CubeGrid.EntityId;
}

int LoadFindTry = 0;
/**
 * Next action finder.
 */
public string FindNextAction()
{
    if (GetBatteryChargeLevel() < BatteryRechargeLevel) {
        return "CHARGE";
    }

    // Find first loaded good.
    LoadedGoods = GetConfig("LimitedToCargoType").Value;
    List<IMyCargoContainer> cargo = new List<IMyCargoContainer>();
    GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargo);
    foreach(IMyCargoContainer container in cargo) {
        if(container.CubeGrid == Me.CubeGrid) {
            IMyInventory inventory = container.GetInventory(0);
            if((double)inventory.CurrentVolume > 0) {
                List<IMyInventoryItem> itemList = inventory.GetItems();
                IMyInventoryItem item = itemList[0];
                
                // https://forum.keenswh.com/threads/getting-display-names-of-imyinventoryitem-cleanly.7391442/
                string[] rawData = item.ToString().Split(new string[] { "er_" }, StringSplitOptions.RemoveEmptyEntries);
                string[] itemData = rawData[1].Split('/');

                LoadedGoods = itemData[0];
                return "CARGO";
            }
        }
    }

    string action = ActionOfDocked != "CHARGE" && LoadFindTry > 1 ? "CHARGE" : "LOAD";
    if (action == "LOAD") LoadFindTry++;

    return action;
}