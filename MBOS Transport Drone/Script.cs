const String NAME = "Transport Drone";
const String VERSION = "2.0.0";
const String DATA_FORMAT = "2.0";

/**
DEMO data:
SendMessage 97692146812461032|AddFlightPath|GPS:Gandur #1:48066.95:30412.84:23038.05:GPS:Gandur #2:48066.88:30588.4:22908.97:GPS:Gandur #3:48192.22:30520.91:22778.19:
SendMessage 97692146812461032|AddFlightPath|GPS:Gandur #3:48192.22:30520.91:22778.19:GPS:Gandur #4:48040.41:30641.55:22847.58:>GPS:Gandur #5:47962.97:30626.48:22888.47:
SendMessage 97692146812461032|AddFlightPath|GPS:Gandur #4:48040.41:30641.55:22847.58:GPS:Gandur #2:48066.88:30588.4:22908.97:GPS:Gandur #1:48066.95:30412.84:23038.05:>GPS:Gandur #6:48061.14:30392.56:23044.62:
SendMessage 97692146812461032|Start
 */

public class ConfigValue
{ 
    public String Key; 
    public String Value; 

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

public class Transceiver {
    
    public string Channel;
    public IMyBroadcastListener BroadcastListener = null;
    public string MyId = "unknown";
    
    protected string LastSendData = "";
    protected List<String> Traffic = new List<String>();
    protected IMyIntergridCommunicationSystem IGC;

    public Transceiver(IMyIntergridCommunicationSystem igc, string channel = "default") {
        IGC = igc;
        SetChannel(channel);
    }

    public void SetChannel(string channel)
    {
        Channel = channel;

        List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
        IGC.GetBroadcastListeners(listeners);
        BroadcastListener = IGC.RegisterBroadcastListener(Channel);
        BroadcastListener.SetMessageCallback("ReadMessages");
    }

    public string ReceiveMessage()
    {
        if (BroadcastListener == null) return String.Empty;

        while(BroadcastListener.HasPendingMessage) {
            MyIGCMessage message = BroadcastListener.AcceptMessage();
            string incoming = message.Data.ToString();

            if (incoming == LastSendData) 
            {
                return String.Empty; // ignore own echoed data
            }
                
            string[] stack = incoming.Trim().Split('|');

            if(stack[1] != MyId) {
                return String.Empty;
            }

            stack = stack.Skip(2).ToArray(); // remove timestamp and id

            string messageText = String.Join("|", stack);
            Traffic.Add("< " + messageText);

            return messageText;
        }

        return String.Empty;
    }

    public void SendMessage(String data) 
    {
        String message = System.DateTime.Now.ToBinary() + "|" + MyId + "|" + data;
        if(BroadcastListener != null) {
            IGC.SendBroadcastMessage(Channel, message, TransmissionDistance.ConnectedConstructs);
        }
        LastSendData = message;
        Traffic.Add("> " + data);
    }

    public String DebugTraffic()
    {
        if(Traffic.Count > 20) {
            Traffic.RemoveRange(0, Traffic.Count - 20);
        }
        
        String output = "";

        int i;
        for(i=Traffic.Count - 1; i >= 0; i--) {
            output += Traffic[i]+"\n";
        }

        return output;
    }
}

public class DroneProgram
{
    public class FlightPath {
        public List<MyWaypointInfo> Waypoints = new List<MyWaypointInfo>();
        public MyWaypointInfo DockingAt = MyWaypointInfo.Empty;
        public bool HasToDock = false;

        public void SetDockingAt(string dockingPoint) 
        {
            HasToDock = false;
            if (dockingPoint != String.Empty) {
                MyWaypointInfo gps = MyWaypointInfo.Empty;
                if (MyWaypointInfo.TryParse(dockingPoint, out gps)) {
                    DockingAt = gps;
                    HasToDock = true;
                }
            }
        }

        public override string ToString() {
            string pathExport = "";
            foreach(MyWaypointInfo waypoint in Waypoints) {
                pathExport += waypoint.ToString();
            }

            return pathExport + ">" + (HasToDock ? DockingAt.ToString() : String.Empty);
        }

        public static FlightPath ParseString(string pathData)
        {
            List<String> gpsList = new List<String>(pathData.Split('>'));
            FlightPath path = new FlightPath();

            MyWaypointInfo.FindAll(gpsList[0], path.Waypoints);
            if(gpsList.Count > 1) {
                path.SetDockingAt(gpsList[1]);
            }

            return path;
        }
    }

    public IMyRemoteControl RemoteControl;
    public IMyGridTerminalSystem GridTerminalSystem;
    public Transceiver Transmitter;
    public IMyShipConnector Connector;
    public List<FlightPath> FlightPaths = new List<FlightPath>();
    public string Mode = "Init";
    public Vector3D Target = Vector3D.Zero;
    public FlightPath CurrentPath;
    public double Distance;

    public DroneProgram(IMyRemoteControl remoteControl) 
    {
        RemoteControl = remoteControl;
        RemoteControl.SetAutoPilotEnabled(false);
    }

    public void Run() 
    {
        string receivedMessage = Transmitter.ReceiveMessage();

        if(receivedMessage != String.Empty) ExecuteMessage(receivedMessage);

        CheckFlight();
    }

    public void ExecuteMessage(string message) 
    {
        List<String> stack = new List<String>(message.Split('|'));
        string command = stack[0];
        stack.RemoveAt(0);

        switch(command) {
            case "AddFlightPath": 
                AddFlightPath(stack[0]);
                break;
            case "Start":
                StartFlight();
                break;
        }
    }

    public void AddFlightPath(string gpsList) 
    {
        FlightPath path = FlightPath.ParseString(gpsList);

        if (path.Waypoints.Count > 0) {
            FlightPaths.Add(path);
        }
    }

    protected void StartFlight() 
    {
        if (FlightPaths.Count == 0) return;
        
        DirectFlight(FlightPaths[0].Waypoints[0]);
    }

    protected void CheckFlight()
    {
        if (TargetReached() == false && RemoteControl.IsAutoPilotEnabled) return;

        switch(Mode) {
            case "Direct":
                Dock();
                if (FlightPaths.Count > 0) {
                    if (Connector.Status == MyShipConnectorStatus.Connected) {
                        StartFlight();
                        break;
                    } 
                    FlightNextPath();
                    break;
                }
                break;
            case "Flight":
                if (CurrentPath.HasToDock) {
                    DirectFlight(CurrentPath.DockingAt);
                    break;
                }
                if (FlightPaths.Count > 0) {
                    FlightNextPath();
                    break;
                }
                Dock();
                break;
            case "Init":
                Dock();
                break;
            case "None":
                break;
            default:
                if (FlightPaths.Count > 0) {
                    StartFlight();
                }
                break;
        }
    }

    protected void DirectFlight(MyWaypointInfo waypoint)
    {
        Mode = "Direct";
        Target = waypoint.Coords;
        
        Connector.Disconnect();

        RemoteControl.FlightMode = FlightMode.OneWay;
        RemoteControl.SpeedLimit = 3f;
        RemoteControl.SetCollisionAvoidance(false);
        RemoteControl.SetDockingMode(true);
        RemoteControl.ClearWaypoints();
        RemoteControl.AddWaypoint(waypoint);

        RemoteControl.SetAutoPilotEnabled(true);
    }

    protected void FlightNextPath() {
        CurrentPath = FlightPaths[0];
        FlightPaths.RemoveAt(0);
        Mode = "Flight";
        
        Target = CurrentPath.Waypoints[CurrentPath.Waypoints.Count - 1].Coords;
        
        RemoteControl.FlightMode = FlightMode.OneWay;
        RemoteControl.SpeedLimit = 100f;
        RemoteControl.SetCollisionAvoidance(true);
        RemoteControl.SetDockingMode(false);
        RemoteControl.ClearWaypoints();

        foreach(MyWaypointInfo waypoint in CurrentPath.Waypoints) {
            RemoteControl.AddWaypoint(waypoint);
        }
        
        RemoteControl.SetAutoPilotEnabled(true);
    }

    protected void Dock() 
    {
        Mode = "None";
        RemoteControl.FlightMode = FlightMode.OneWay;
        RemoteControl.SpeedLimit = 5f;
        RemoteControl.SetAutoPilotEnabled(false);
        RemoteControl.SetCollisionAvoidance(true);
        RemoteControl.SetDockingMode(true);
        RemoteControl.ClearWaypoints();
        
        FindConnector();
        Connector.Connect();
    }

    protected void FindConnector() 
    {
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors);

        foreach (IMyShipConnector connector in connectors) {
            if (connector.CubeGrid.EntityId != RemoteControl.CubeGrid.EntityId) {
                continue;
            }
            Connector = connector;
        }
    }

    protected bool TargetReached() 
    {
        Distance = Vector3D.Distance(RemoteControl.GetPosition(), Target);
        return (bool)(Distance < (Mode == "Direct" ? 1.0 : 5.0));
    }
}

List<ConfigValue> Config = new List<ConfigValue>();
DroneProgram Drone;
IMyTextSurface ComputerDisplay;

public Program()
{
    ComputerDisplay = Me.GetSurface(0);
    ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
    ComputerDisplay.ClearImagesFromSelection();
    ComputerDisplay.ChangeInterval = 0;

    Runtime.UpdateFrequency = UpdateFrequency.None;

    InitProgram();
}

public void Save()
{
    List<String> store = new List<String>();  
    int i; 

    if(Drone != null) {
        GetConfig("RemoteControl").Value = GetId(Drone.RemoteControl);
        GetConfig("Channel").Value = Drone.Transmitter.Channel;

        List<string> pathConfig = new List<string>();
        foreach(DroneProgram.FlightPath path in Drone.FlightPaths) {
            pathConfig.Add(path.ToString());
        }
        GetConfig("Paths").Value = String.Join("*", pathConfig.ToArray());
    }
     
    for(i = 0; i < Config.Count; i++) { 
        store.Add(Config[i].ToString()); 
    } 
     
    Me.CustomData = "FORMAT v" + DATA_FORMAT + "\n" + String.Join("\n", store.ToArray()); 
}

public void InitProgram() 
{
    bool missingConditions = false;
    LoadConfig();
    IMyRemoteControl remoteControl = GetBlock(GetConfig("RemoteControl").Value) as IMyRemoteControl;
    
    Save(); // create default data
    if(remoteControl == null) {
        missingConditions = true;
        Echo("Remote Controll missing. Run `SetRemoteControl <name>`");
    }

    if(missingConditions) return;

    ConfigValue channel = GetConfig("Channel");

    Drone = new DroneProgram(remoteControl) {
        GridTerminalSystem = GridTerminalSystem,
        Transmitter = new Transceiver(IGC, channel.Value != String.Empty ? channel.Value : "default") {
            MyId = Me.CubeGrid.EntityId.ToString()
        }
    };

    Drone.FlightPaths.Clear();
    List<String> pathList = new List<String>(GetConfig("Paths").Value.Split('*'));
    foreach(String pathData in pathList) {
        if (pathData == String.Empty) continue;
        Drone.AddFlightPath(pathData);
    }

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    Echo("Program initialized.");
}

public void LoadConfig()
{ 
    string data = Me.CustomData;
    
    if (data.Length > 0) { 
        String[] configs = data.Split('\n'); 
        
        if(configs[0] != "FORMAT v" + DATA_FORMAT) return;
        
        for(int i = 1; i < configs.Length; i++) {
            String line = configs[i]; 
            if (line.Length > 0) {
                string[] parts = line.Split('=');
                if(parts.Length != 2) continue;
                ConfigValue config = GetConfig(parts[0].Trim());
                config.Value = config.Value != String.Empty ? config.Value : parts[1].Trim();
            }
        } 
    } 
} 

public void Main(string argument, UpdateType updateSource)
{
    ReadArgument(argument);
    
    if (Drone == null) {
        InitProgram();
        if (Drone == null) return;
    }

    Drone.Run();
    Save();
    UpdateInfo();
}

public ConfigValue GetConfig(String key) {
    ConfigValue config = Config.Find(x => x.Key == key);
    if(config != null) return config;
     
    ConfigValue newValue = new ConfigValue(key, String.Empty); 
    Config.Add(newValue); 
    return newValue; 
} 

public IMyTerminalBlock GetBlock(string id)
{   
    long cubeId = 0L;
    try
    {
        cubeId = Int64.Parse(id);
    }
    catch (FormatException)
    {
        return null;
    }
    IMyTerminalBlock block = GridTerminalSystem.GetBlockWithId(cubeId);
    
    if(block != null && block.CubeGrid == Me.CubeGrid) {
        return block;
    }
    
    return null;
}

public string GetId(IMyTerminalBlock block)
{
    return block.EntityId.ToString();
}

public void UpdateInfo()
{
    string currentPath = "";
    List<MyWaypointInfo> waypoints = new List<MyWaypointInfo>();
    Drone.RemoteControl.GetWaypointInfo(waypoints);
    foreach(MyWaypointInfo waypoint in waypoints) {
        currentPath += waypoint.ToString();
    }

    string output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "RemoteControl: " + Drone.RemoteControl.CustomName + "\n"
        + "Connector: " + (Drone.Connector != null ? Drone.Connector.CustomName : "not found") + "\n"
        + "Mode: " + Drone.Mode + "\n"
        + "CurrentPath: " + currentPath + "\n"
        + "Next Target: " + Drone.Target.ToString() + "\n"
        + "Distance: " + Drone.Distance.ToString() + "\n"
        + "Pathes to flight: " + Drone.FlightPaths.Count + "\n"
        + "Traffic (channel:'" + Drone.Transmitter.Channel +"' id:'" + Drone.Transmitter.MyId + "'):\n\n"
        + Drone.Transmitter.DebugTraffic()
    ;
    ComputerDisplay.WriteText(output, false);
}

public void ReadArgument(String args) 
{
    if (args == String.Empty) return;
     
    IMyTerminalBlock block;
    List<String> parts = new List<String>(args.Split(' ')); 
    String command = parts[0].Trim();
    parts.RemoveAt(0);
    String allArgs = String.Join(" ", parts.ToArray());
    switch (command) {
        case "SetRemoteControl":
            block = GridTerminalSystem.GetBlockWithName(allArgs);
            if (Drone != null && block != null) {
                Drone.RemoteControl = block as IMyRemoteControl;
            } else {
                GetConfig("RemoteControl").Value = block != null ? GetId(block) : "";
            }
            Echo("RemoteControl '" + allArgs + "' configured.");
            break;

        case "SetChannel":
            if (Drone != null) {
                Drone.Transmitter.SetChannel(allArgs);
            } else {
                GetConfig("Channel").Value = allArgs;
            }
            Echo("Transmitter Channel " + allArgs + "configured.");
            break;

        case "Demo":
            Drone.FlightPaths.Clear();
            Drone.ExecuteMessage("AddFlightPath|GPS:Gandur #1:48066.95:30412.84:23038.05:GPS:Gandur #2:48066.88:30588.4:22908.97:GPS:Gandur #3:48192.22:30520.91:22778.19:");
            Drone.ExecuteMessage("AddFlightPath|GPS:Gandur #3:48192.22:30520.91:22778.19:GPS:Gandur #4:48040.41:30641.55:22847.58:>GPS:Gandur #5:47962.97:30626.48:22888.47:");
            Drone.ExecuteMessage("AddFlightPath|GPS:Gandur #4:48040.41:30641.55:22847.58:GPS:Gandur #2:48066.88:30588.4:22908.97:GPS:Gandur #1:48066.95:30412.84:23038.05:>GPS:Gandur #6:48061.14:30392.56:23044.62:");
            Drone.ExecuteMessage("Start");
            break;

        default:
            Echo("Available Commands: SetRemoteControl");
            break;
    }
}
