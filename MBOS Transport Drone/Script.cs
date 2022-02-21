const String NAME = "Transport Drone";
const String VERSION = "4.0.1";
const String DATA_FORMAT = "3";
const String TRANSPORT_TYPE = "transport";

/**
DEMO radio data:
SendMessage 97692146812461032|AddFlightPath|GPS:Gandur #1:48066.95:30412.84:23038.05:GPS:Gandur #2:48066.88:30588.4:22908.97:GPS:Gandur #3:48192.22:30520.91:22778.19:
SendMessage 97692146812461032|AddFlightPath|GPS:Gandur #3:48192.22:30520.91:22778.19:GPS:Gandur #4:48040.41:30641.55:22847.58:>GPS:Gandur #5:47962.97:30626.48:22888.47:
SendMessage 97692146812461032|AddFlightPath|GPS:Gandur #4:48040.41:30641.55:22847.58:GPS:Gandur #2:48066.88:30588.4:22908.97:GPS:Gandur #1:48066.95:30412.84:23038.05:>GPS:Gandur #6:48061.14:30392.56:23044.62:
SendMessage 97692146812461032|Start
*/

public class TransportDrone
{
    public class FlightPath {
        public List<MyWaypointInfo> Waypoints = new List<MyWaypointInfo>();
        public MyWaypointInfo DockingAt = MyWaypointInfo.Empty;
        public bool HasToDock = false;

        public void SetDockingAt(String dockingPoint) 
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

        public override String ToString() {
            String pathExport = "";
            foreach(MyWaypointInfo waypoint in Waypoints) {
                pathExport += waypoint.ToString();
            }

            return pathExport + (HasToDock ? ">" + DockingAt.ToString() : String.Empty);
        }

        public static FlightPath ParseString(String pathData)
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
    protected IMyGridTerminalSystem GridTerminalSystem;
    public MBOS Sys;
    public Batteries Batteries;
    public Lights Lights;
    public IMyShipMergeBlock Connector;
    public List<FlightPath> FlightPaths { get; } = new List<FlightPath>();
    public String Mode = "Init";
    public Vector3D Target = Vector3D.Zero;
    public string TargetInfo = string.Empty;
    public Vector3D StartPoint = Vector3D.Zero;
    public FlightPath CurrentPath = new FlightPath();
    public double Distance = 0.0;
    public long Hangar = 0L;
    public String Homepath = String.Empty;
    public bool HasToDeliverCargo = false;

    protected DateTime Mark = DateTime.Now;
    protected DateTime EnableConnectorAt = DateTime.Now;

    protected double LastDistance = 0f;
    public int NoFlightDetectCount = 0;
    protected bool LastRunWasConnected = false;
    protected int countMissingCargo = 0;

    public TransportDrone(
        IMyRemoteControl remoteControl, 
        MBOS sys,
        Batteries batteries, 
        Lights lights,
        IMyShipMergeBlock connector,
        long hangar,
        string homepath,
        bool hasToDeliverCargo
    )
    {
        RemoteControl = remoteControl;
        GridTerminalSystem = sys.GridTerminalSystem;
        Batteries = batteries;
        Lights = lights;
        Connector = connector;
        Sys = sys;
        Hangar = hangar;
        Homepath = homepath;
        LastRunWasConnected = Connector.IsConnected;
        HasToDeliverCargo = hasToDeliverCargo;
    }

    public void Run() 
    {
        String receivedMessage;
        while((receivedMessage = Sys.BroadCastTransceiver.ReceiveMessage()) != String.Empty) {
            ExecuteMessage(receivedMessage);
        }
        while((receivedMessage = Sys.Transceiver.ReceiveMessage()) != String.Empty) {
            ExecuteMessage(receivedMessage);
        }

        CheckFlight();
    }

    public void ExecuteMessage(String message) 
    {
        List<String> stack = new List<String>(message.Split('|'));
        String command = stack[0];
        stack.RemoveAt(0);

        switch(command) {
            case "AddFlightPath":
                AddFlightPath(stack[0]);
                break;
            case "StartDrone":
                Start();
                break;
            case "DroneHangarHasPodsAvailable":
                if(Hangar == 0L) RequestHangar(long.Parse(stack[0]));
                break;
            case "DroneRegisteredAt":
                RegisterHangar(stack[0], stack[1]);
                AddFlightPath(stack[1]);
                Start();
                break;
        }
    }

    public void AddFlightPath(String gpsList) 
    {
        List<string> pathParts = new List<string>(gpsList.Split('<'));

        pathParts.ForEach(
            delegate(string pathPart) {
                if (pathPart == string.Empty) return;

                FlightPath path = FlightPath.ParseString(pathPart);
                if (path.Waypoints.Count == 0) return;
                FlightPaths.Add(path);
            }
        );
    }

    public void GoHome() {
        if(IsHomeConnected() == false) {
            AddFlightPath(Homepath);
            Start();
        }
    }

    public bool IsHomeConnected() {
        if (!Connector.IsConnected) {
            return false;
        }

        List<IMyShipMergeBlock> connectors = new List<IMyShipMergeBlock>();
        MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(
            connectors, 
            (IMyShipMergeBlock connector) => connector.CustomData.Contains(Sys.EntityId.ToString()) && Connector.CubeGrid.EntityId == connector.CubeGrid.EntityId
        );
        return connectors.Count > 0;
    }

    public bool IsCargoConnected() {
        if (!Connector.IsConnected) {
            return false;
        }

        List<IMyShipMergeBlock> connectors = new List<IMyShipMergeBlock>();
        MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(
            connectors, 
            (IMyShipMergeBlock connector) => Connector.CubeGrid.EntityId == connector.CubeGrid.EntityId && connector.IsConnected && connector.CustomData.Trim() == "Loader"
        );
        return connectors.Count > 0;
    }

    protected void Start() 
    {
        Lights.TurnOn();
        Mark = DateTime.Now.AddSeconds(5);
        Mode = "Mark";
    }

    protected void CheckFlight()
    {
        Vector3D offset = CalculateConnectorOffset();
        LastDistance = Distance;
        Distance = Vector3D.Distance(RemoteControl.GetPosition() - offset, Target);
        //double traveled = Vector3D.Distance(RemoteControl.GetPosition() - offset, StartPoint);

        if (Target.Equals(Vector3D.Zero)) {
            Distance = 0;
            //traveled = 0;
        }

        if(!Connector.IsConnected) SetupThrusters(true);

        bool withAvoidance = Mode != "Direct" || (Mode == "Direct" && Distance > 60.0);
        RemoteControl.SetCollisionAvoidance(withAvoidance);
        bool isTargetReached = ((Distance > (Mode == "Direct" ? 0.05 : 50.0)) && RemoteControl.IsAutoPilotEnabled) == false;

        if (!Connector.IsConnected && LastRunWasConnected && !HasToDeliverCargo) {
            MBOS.Sys.Traffic.Add("[CF]: Finish flight by disconnect cargo.");
            Mode = "Direct";
            isTargetReached = true;
            Disconnect(); // Turn connector off for a while
            countMissingCargo = 0;
        }
        if (Connector.IsConnected && !LastRunWasConnected && IsCargoConnected()) {
            MBOS.Sys.Traffic.Add("[CF]: Finish flight by connect cargo.");
            Mode = "Direct";
            isTargetReached = true;
            countMissingCargo = 0;
        }
        LastRunWasConnected = Connector.IsConnected;

        if (!isTargetReached) {
            double distanceToPercent = 100.0;
            if(Mode == "Direct" || (CurrentPath.Waypoints.Count > (CurrentPath.HasToDock ? 1 : 0))) {
                distanceToPercent = Distance > 100.0 ? 100.0 : Distance;
            }
            float speedLimit = 20f / 100f * Convert.ToSingle(distanceToPercent);
            float minSpeed = (Mode == "Direct") ? 1f :  3f;
            RemoteControl.SpeedLimit = speedLimit < minSpeed || Target.Equals(Vector3D.Zero) ? minSpeed : speedLimit;
            return;
        }
        
        if (Mark >= DateTime.Now) return;
        if (EnableConnectorAt < DateTime.Now) Connector.Enabled = true;

        /*
        if((Mode == "Direct" || Mode == "Flight") && !Connector.IsConnected) {
            if (Math.Abs(LastDistance - Distance) < 0.1) {
                NoFlightDetectCount++;
            } else {
                NoFlightDetectCount = 0;
            }

            if (NoFlightDetectCount > 5) {
                Mode = "Mark";
                NoFlightDetectCount = 0;
            }
            
        } else NoFlightDetectCount = 0;
        */


        MBOS.Sys.Traffic.Add("[CF]: Next Mode:" + Mode +"(HDC:"+(HasToDeliverCargo?"Y":"N")+" C:"+(Connector.IsConnected?"Y":"N")+")");

        switch(Mode) {
            case "Direct": // Finish direct flight
                if (HasToDeliverCargo && Connector.IsConnected) {
                    Mark = DateTime.Now.AddSeconds(3);
                }
                Dock();
                break;
            case "Flight":
                if (CurrentPath.HasToDock && CurrentPath.Waypoints.Count <= 1) {
                    FlightToPoint(false, CurrentPath.Waypoints[0]);
                    Mode = "FlightToDock";
                    break;
                }
                if (FlightPaths.Count > 0 || (CurrentPath.Waypoints.Count > (CurrentPath.HasToDock ? 1 : 0))) {
                    FlightNextPath();
                    break;
                }
                Dock();
                break;
            case "FlightToDock":
                DirectFlight(CurrentPath.DockingAt);
                break;
            case "Init":
                Dock();
                break;
            case "None":
                if (IsHomeConnected() == false) {
                    GoHome();
                }
                break;
            case "Dock":
                Mode = "Mark";
                if (IsHomeConnected()) {
                    Batteries.SetReacharge();
                    Lights.TurnOff();
                    SetupThrusters(false);
                    HasToDeliverCargo = false;
                } else {
                    if (!HasToDeliverCargo) {
                        if(Connector.IsConnected) {
                            MBOS.Sys.Traffic.Add("[CF]: Disconnect unexpected cargo.");
                            Disconnect();
                        }
                    } else if (HasToDeliverCargo) {
                        if(!Connector.IsConnected) {
                            countMissingCargo++;
                            if (countMissingCargo > 3) {
                                countMissingCargo = 0;
                                MBOS.Sys.Traffic.Add("[CF]: Cargo is missing...abort mission");
                                FlightPaths.Clear();
                                GoHome();
                                break;
                            }
                            MBOS.Sys.Traffic.Add("[CF]: Cargo is missing...try again");
                            DirectFlight(CurrentPath.Waypoints[CurrentPath.Waypoints.Count - 1]);
                            Mode = "Flight"; 
                            break;
                        }
                        HasToDeliverCargo = false; // Next stop unload
                    }
                }
                break;
            case "Start":
            case "Mark":
                if (FlightPaths.Count > 0) {
                    if (IsHomeConnected()) {
                        StartFlight();
                        HasToDeliverCargo = true;
                        LastRunWasConnected = false;
                    } else {
                        FlightNextPath(true);
                    }
                    break;
                } 
                Mode = "None";
                break;
            default:
                if (FlightPaths.Count > 0) {
                    StartFlight();
                }
                break;
        }

    }

    protected void StartingFlight(MyWaypointInfo waypoint)
    {
        Mode = "Start";
        StartPoint = RemoteControl.GetPosition();
        Target = waypoint.Coords;
        
        Batteries.SetAuto();
        Lights.TurnOn();
        SetupThrusters(true);
        if (Connector.IsConnected && !IsCargoConnected()) {
            Disconnect();
        }

        RemoteControl.FlightMode = FlightMode.OneWay;
        RemoteControl.SpeedLimit = 1f;
        RemoteControl.SetCollisionAvoidance(false);
        RemoteControl.SetDockingMode(true);
        RemoteControl.ClearWaypoints();
        AddWaypointWithConnectorOffset(waypoint);

        RemoteControl.SetAutoPilotEnabled(true);
    }

    public void Dock() 
    {
        Mode = "Dock";
        RemoteControl.FlightMode = FlightMode.OneWay;
        RemoteControl.SpeedLimit = 1f;
        RemoteControl.SetAutoPilotEnabled(false);
        RemoteControl.SetCollisionAvoidance(true);
        RemoteControl.SetDockingMode(true);
        RemoteControl.ClearWaypoints();
        if(IsHomeConnected() && FlightPaths.Count == 0) {
            Mark = DateTime.Now.AddSeconds(2);
        } else {
            Mark = DateTime.Now.AddSeconds(4);
        }
    }

    public void Disconnect() {
        Connector.Enabled = false;
        EnableConnectorAt = DateTime.Now.AddSeconds(10);
    }

    protected void StartFlight() 
    {
        if (FlightPaths.Count == 0) return;
        
        StartingFlight(FlightPaths[0].Waypoints[0]);
    }

    protected void DirectFlight(MyWaypointInfo waypoint)
    {
        Mode = "Direct";
        StartPoint = RemoteControl.GetPosition();
        Target = waypoint.Coords;
        
        Batteries.SetAuto();
        Lights.TurnOn();
        if (Connector.IsConnected && !IsCargoConnected()) {
            Disconnect();
        }

        RemoteControl.FlightMode = FlightMode.OneWay;
        RemoteControl.SpeedLimit = 1f;
        RemoteControl.SetCollisionAvoidance(false);
        RemoteControl.SetDockingMode(true);
        RemoteControl.ClearWaypoints();
        AddWaypointWithConnectorOffset(waypoint);

        RemoteControl.SetAutoPilotEnabled(true);
        EnableCargoThrusters();
    }

    protected void FlightNextPath(bool isStart = false) {
        if (Connector.IsConnected && !IsCargoConnected()) {
            Disconnect();
        }
        SetupThrusters(true);

        CurrentPath = FlightPaths[0];
        MyWaypointInfo nextTarget = CurrentPath.Waypoints[0];
        if (CurrentPath.Waypoints.Count > (CurrentPath.HasToDock ? 1 : 0)) {
            CurrentPath.Waypoints.RemoveAt(0);
        }
        if (CurrentPath.Waypoints.Count == (CurrentPath.HasToDock ? 1 : 0)) {
            FlightPaths.RemoveAt(0);
        }
        FlightToPoint(isStart, nextTarget);
        Mode = "Flight";
    }

    protected void FlightToPoint(bool isStart, MyWaypointInfo nextTarget) {
        StartPoint = RemoteControl.GetPosition();
        Target = nextTarget.Coords;
        TargetInfo = nextTarget.ToString();
        
        RemoteControl.FlightMode = FlightMode.OneWay;
        if (isStart) {
            RemoteControl.SpeedLimit = 1f;
            RemoteControl.SetCollisionAvoidance(false);
            RemoteControl.SetDockingMode(true);
            Mark = DateTime.Now.AddSeconds(3); // flight 3 sec without acceleration
        } else {
            RemoteControl.SetCollisionAvoidance(false);
            RemoteControl.SetDockingMode(true);
        }
        RemoteControl.ClearWaypoints();

        AddWaypointWithConnectorOffset(nextTarget);
        
        RemoteControl.SetAutoPilotEnabled(true);
        EnableCargoThrusters();
    }

    public Vector3D CalculateConnectorOffset() {
        double offsetCorrection = 1.2;

        if(IsHomeConnected() == false && Connector.IsConnected) {
            List<IMyShipConnector> otherConnectors = new List<IMyShipConnector>();
            MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(
                otherConnectors, 
                (IMyShipConnector connectorItem) => connectorItem.CubeGrid.EntityId == Connector.CubeGrid.EntityId
            );
            if(otherConnectors.Count > 0) {
                 return (RemoteControl.GetPosition() - otherConnectors[0].GetPosition()) * offsetCorrection;
            }
        }
        
        return (RemoteControl.GetPosition() - Connector.GetPosition()) * offsetCorrection;
    }

    protected void AddWaypointWithConnectorOffset(MyWaypointInfo waypoint) {
        Vector3D coords = new Vector3D(waypoint.Coords);

        // Add offset
        coords += CalculateConnectorOffset();

        RemoteControl.AddWaypoint(coords, waypoint.Name);
    }

    protected void RequestHangar(long receiver)
    {
        Sys.Transceiver.SendMessage(receiver, "DroneNeedHome|" + Sys.EntityId.ToString() + "|" + TRANSPORT_TYPE);
    }

    protected void RegisterHangar(String hangar, String homepath)
    {
        Hangar = long.Parse(hangar);
        Homepath = homepath;
    }

    protected void EnableCargoThrusters()
    {
        if (!Connector.IsConnected) return;

        SetupThrusters(true);
    }

    protected void SetupThrusters(bool enabled)
    {
        List<IMyThrust> thrusters = new List<IMyThrust>();
        MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyThrust>(
            thrusters, 
            (IMyThrust thruster) => thruster.CubeGrid.EntityId == Connector.CubeGrid.EntityId
        );

        thrusters.ForEach((IMyThrust thruster) => thruster.Enabled = enabled);
    }
}

TransportDrone Drone;
MBOS Sys;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo);
    Runtime.UpdateFrequency = UpdateFrequency.None;

    InitProgram();
}

public void Save()
{
    if(Drone != null) {
        Sys.Config("Mode").Value = Drone.Mode;

        List<String> pathConfig = new List<String>();
        foreach(TransportDrone.FlightPath path in Drone.FlightPaths) {
            pathConfig.Add(path.ToString());
        }
        Sys.Config("Paths").Value = String.Join("*", pathConfig.ToArray());
        Sys.Config("Hangar").Value = Drone.Hangar.ToString();
        Sys.Config("Homepath").Value = Drone.Homepath;
        Sys.Config("CurrenPath").Value = Drone.CurrentPath.ToString();
        Sys.Config("HasToDeliverCargo").Value = Drone.HasToDeliverCargo ? "Y": "N";
    }
    
    Sys.SaveConfig();
}

public void InitProgram() 
{
    Sys.LoadConfig();

    IMyRemoteControl remoteControl = null;
    IMyShipMergeBlock cargoConnector = FindConnector();
    
    List<IMyRemoteControl> blocks = new List<IMyRemoteControl>();
    GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(
        blocks, 
        (IMyRemoteControl blockItem) => blockItem.CubeGrid.EntityId == Me.CubeGrid.EntityId && blockItem.CustomData.Trim() == "DroneControl"
    );
    if (blocks.Count > 0) {
        Echo("DroneControl found.");
        remoteControl= blocks[0];
    }

    Save(); // create default data

    bool missingConditions = false;
    if(remoteControl == null) {
        missingConditions = true;
        Echo("Can not find remote control with custom data 'DroneControl' in this grid.");
    }
    if(cargoConnector == null) {
        missingConditions = true;
        Echo("Can not find merge block with custom data 'Cargo' in this grid.");
    }
    if(missingConditions) {
        Sys.ComputerDisplay.WriteText("\n\n\nMissing configuration.", false);
        return;
    }

    string hangarValue = Sys.Config("Hangar").Value;
    long hangar = hangarValue == String.Empty ? 0L : long.Parse(hangarValue);
    String homepath = Sys.Config("Homepath").Value;
    bool hasToDeliverCargo = Sys.Config("HasToDeliverCargo").Value == "Y";

    Drone = new TransportDrone(
        remoteControl, 
        Sys,
        new Batteries(GridTerminalSystem, Me.CubeGrid.EntityId),
        new Lights(GridTerminalSystem, Me.CubeGrid.EntityId),
        cargoConnector,
        hangar,
        homepath,
        hasToDeliverCargo
    );

    string currentPath = Sys.Config("CurrenPath").Value;
    if (currentPath != string.Empty) {
        Drone.CurrentPath = TransportDrone.FlightPath.ParseString(currentPath);
    }

    if (hangar != 0L) {
        List<String> pathList = new List<String>(Sys.Config("Paths").Value.Split('*'));
        foreach(String pathData in pathList) {
            if (pathData == String.Empty) continue;
            Drone.AddFlightPath(pathData);
        }
        Drone.Mode = Sys.Config("Mode").ValueWithDefault(Drone.Mode);
    }
    
    if (hangar == 0L) {
        Sys.BroadCastTransceiver.SendMessage("DroneNeedHome|" + Sys.EntityId.ToString() + "|" + TRANSPORT_TYPE);
    } else if(Drone.Mode == "None" || Drone.Mode == "Init") {
        Drone.GoHome();
    }

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    Echo("Program initialized.");
}


public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);
    
    if (Drone == null) {
        InitProgram();
        if (Drone == null) { 
            Runtime.UpdateFrequency = UpdateFrequency.None;
            return;
        }
    }

    Drone.Run();
    Save();
    UpdateInfo();

    if (Drone.Mode != "None") {
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    } else if (Drone.IsHomeConnected()) {
        Runtime.UpdateFrequency = UpdateFrequency.None;
    } else {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    }
}

public IMyShipMergeBlock FindConnector() 
{
    List<IMyShipMergeBlock> connectors = new List<IMyShipMergeBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(
        connectors, 
        (IMyShipMergeBlock connector) => connector.CubeGrid.EntityId == Sys.GridId && connector.CustomData.Trim() == "Cargo"
    );

    if (connectors.Count > 0) {
        return connectors[0];
    }

    return null;
}

public void UpdateInfo()
{
    String currentPath = "";
    List<MyWaypointInfo> waypoints = new List<MyWaypointInfo>();
    Drone.RemoteControl.GetWaypointInfo(waypoints);
    foreach(MyWaypointInfo waypoint in waypoints) {
        currentPath += waypoint.ToString();
    }

    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "RemoteControl: " + Drone.RemoteControl.CustomName + "\n"
        + "Connector: " + (Drone.Connector != null ? Drone.Connector.CustomName : "not found") + "\n"
        + "Connector connected: " + (Drone.Connector != null && Drone.Connector.IsConnected ? "Yes" : "No") + "\n"
        + "Home: " + Drone.Hangar.ToString() + "\n"
        + "Home connected: " + (Drone.Connector != null && Drone.IsHomeConnected() ? "Yes" : "No") + "\n"
        + "Mode: " + Drone.Mode + "\n"
        + "CurrentPath: " + currentPath + "\n"
        + "Next Target: " + Drone.Target.ToString() + (Drone.Connector != null ? "(Offset: " + Drone.CalculateConnectorOffset().ToString() + ")" : "") + "\n"
        + "             " + Drone.TargetInfo + "\n"
        + "Distance: " + Drone.Distance.ToString() + "\n"
        + "Pathes to flight: " + Drone.FlightPaths.Count + "\n"
        + "NoFlightDetectCount: " + Drone.NoFlightDetectCount.ToString() + "\n"
        + "----------------------------------------\n"
        + Sys.Transceiver.DebugTraffic()
    ;
    Sys.ComputerDisplay.WriteText(output, false);
}

public void ReadArgument(String args) 
{
    if (args == String.Empty) return;

    List<String> parts = new List<String>(args.Split(' ')); 
    String command = parts[0].Trim();
    parts.RemoveAt(0);
    String allArgs = String.Join(" ", parts.ToArray());
    switch (command) {
        case "NewHome":
            Drone.Hangar = 0L;
            Save();
            InitProgram();
            break;
        case "AddPath":
            Drone.ExecuteMessage("AddFlightPath|"+allArgs);
            Drone.ExecuteMessage("Start");
            break;
        case "ReceiveMessage":
            Echo("Received radio data.");
            Drone.Run();
            break;
        case "Disconnect":
            Drone.Disconnect();
            Echo("Disconnected.");
            break;
        case "Reset":
            Drone = null;
            Me.CustomData = "";
            InitProgram();
            break;
        default:
            Echo("Available Commands: NowHome, AddPath and Disconnect");
            break;
    }
}

public class MBOS {
    public class ConfigValue
    { 
        public String Key; 
        protected String _value; 
        
        public String Value {
            get { return _value; }
            set { _value = value; }
        }

        public IMyTerminalBlock Block {
            get { return Sys.GetBlock(_value); }
            set { _value = value == null ? "" : Sys.GetId(value); }
        }

        public ConfigValue(String key, String value)  
        { 
            Key = key; 
            _value = value; 
        } 

        public String ValueWithDefault(String defaultValue) 
        {
            return Value != String.Empty ? Value : defaultValue;
        }
        
        public override String ToString()
        { 
            return Key + '=' + _value; 
        } 
    }

    public static MBOS Sys;
    public String Name = "Module";
    public String Version = "0.0.0";
    public String DataFormat = "0";
    public IMyProgrammableBlock Me;
    public IMyGridTerminalSystem GridTerminalSystem;
    public IMyIntergridCommunicationSystem IGC;
    public Action<string> Echo;
    public UniTransceiver Transceiver;
    public WorldTransceiver BroadCastTransceiver;
    public List<String> Traffic = new List<String>();

    public long GridId { get { return Me.CubeGrid.EntityId; }}
    public long EntityId { get { return Me.EntityId; }}

    public List<ConfigValue> ConfigList = new List<ConfigValue>();
    public IMyTextSurface ComputerDisplay;

    protected bool ConfigLoaded = false;

    public MBOS(IMyProgrammableBlock me, IMyGridTerminalSystem gridTerminalSystem, IMyIntergridCommunicationSystem igc, Action<string> echo) {
        Me = me;
        GridTerminalSystem = gridTerminalSystem;
        IGC = igc;
        Echo = echo;
        
        ComputerDisplay = Me.GetSurface(0);
        ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
        ComputerDisplay.ClearImagesFromSelection();
        ComputerDisplay.ChangeInterval = 0;

        MBOS.Sys = this;
        Transceiver = new UniTransceiver(this, Traffic);
        BroadCastTransceiver = new WorldTransceiver(this, Traffic);
    }

    public ConfigValue Config(String key) {
        ConfigValue config = ConfigList.Find(x => x.Key == key);
        if(config != null) return config;
        
        ConfigValue newValue = new ConfigValue(key, String.Empty); 
        ConfigList.Add(newValue); 
        return newValue; 
    } 

    public IMyTerminalBlock GetBlock(String id)
    {   
        long cubeId = 0L;
        try
        {
            cubeId = long.Parse(id);
        }
        catch (FormatException)
        {
            return null;
        }

        IMyTerminalBlock block = GridTerminalSystem.GetBlockWithId(cubeId);
        if(block == null || block.CubeGrid.EntityId != GridId) {
            Echo("Don't found:" + cubeId.ToString() + " on " + GridId.ToString());
            return null;
        }

        return block;
    }

    public String GetId(IMyTerminalBlock block)
    {
        return block.EntityId.ToString();
    }
    
    public void LoadConfig()
    { 
        if(ConfigLoaded) return;
        ConfigLoaded = true;
        String data = Me.CustomData;
        
        if (data.Length > 0) { 
            String[] configs = data.Split('\n'); 
            
            if(configs[0] != "FORMAT v" + DATA_FORMAT) return;
            
            for(int i = 1; i < configs.Length; i++) {
                String line = configs[i]; 
                if (line.Length > 0) {
                    String[] parts = line.Split('=');
                    if(parts.Length != 2) continue;
                    ConfigValue config = Config(parts[0].Trim());
                    config.Value = config.Value != String.Empty ? config.Value : parts[1].Trim();
                }
            } 
        } 
    } 

    public void SaveConfig()
    {
        List<String> store = new List<String>();  
        int i; 

        for(i = 0; i < ConfigList.Count; i++) { 
            store.Add(ConfigList[i].ToString()); 
        } 
        
        Me.CustomData = "FORMAT v" + DATA_FORMAT + "\n" + String.Join("\n", store.ToArray()); 
    }

    public class WorldTransceiver {
        
        public String Channel;
        public IMyBroadcastListener BroadcastListener;

        protected MBOS Sys;
        protected TransmissionDistance Range = TransmissionDistance.AntennaRelay;
        protected String LastSendData = "";
        protected List<String> Traffic = new List<String>();

        public WorldTransceiver(MBOS sys, List<String> traffic) {
            Sys = sys;
            Traffic = traffic;
            Channel = "world";

            ListenerAware();
        }

        protected void ListenerAware()
        {
            List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
            Sys.IGC.GetBroadcastListeners(listeners, (IMyBroadcastListener listener) => listener.Tag == Channel && listener.IsActive);

            BroadcastListener = listeners.Count > 0 ? listeners[0] : Sys.IGC.RegisterBroadcastListener(Channel);
            BroadcastListener.SetMessageCallback("ReceiveMessage");
        }

        public String ReceiveMessage()
        { 
            if (!BroadcastListener.HasPendingMessage) return String.Empty;

            MyIGCMessage message = BroadcastListener.AcceptMessage();
            String incoming = message.As<String>();

            if (incoming == LastSendData) 
            {
                return String.Empty; // ignore own echoed data
            }
                
            String[] stack = incoming.Trim().Split('|');

            stack = stack.Skip(1).ToArray(); // remove timestamp

            String messageText = String.Join("|", stack);
            Traffic.Add("[B]< " + messageText);

            return messageText;
        }

        public void SendMessage(String data) 
        {
            String message = DateTime.Now.ToBinary() + "|" + data;
            Sys.IGC.SendBroadcastMessage<String>(Channel, message, Range);
            LastSendData = message;
            Traffic.Add("[B]> " + data);
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

    public class UniTransceiver
    {
        protected IMyUnicastListener Listener;
        protected MBOS Sys;
        protected List<String> Traffic = new List<String>();

        public UniTransceiver(MBOS sys, List<String> traffic)
        {
            Sys = sys;
            Traffic = traffic;
            Listener = Sys.IGC.UnicastListener;
            Listener.SetMessageCallback("ReceiveMessage");
        }
        

        public String ReceiveMessage()
        { 
            if (!Listener.HasPendingMessage) return String.Empty;

            MyIGCMessage message = Listener.AcceptMessage();
            String incoming = message.As<String>();

            Traffic.Add("[U]< " + incoming);

            return incoming;
        }

        public void SendMessage(long target, String data) 
        {
            Traffic.Add("[U]> " + data);
            Sys.IGC.SendUnicastMessage<string>(target, "whisper", data);
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
}

public class Batteries {
    protected List<IMyBatteryBlock> BatteryList = new List<IMyBatteryBlock>();
    
    public Batteries(IMyGridTerminalSystem gridTerminalSystem, long gridId) {
        BatteryList.Clear();
        gridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(BatteryList, (IMyBatteryBlock block) => block.CubeGrid.EntityId == gridId);
    }

    public void SetReacharge() 
    {
        foreach(IMyBatteryBlock battery in BatteryList) {
            battery.ChargeMode = ChargeMode.Recharge;
        }
    }

    public void SetAuto() 
    {
        foreach(IMyBatteryBlock battery in BatteryList) {
            battery.ChargeMode = ChargeMode.Auto;
        }
    }
}

public class Lights {
    protected List<IMyLightingBlock> LightList = new List<IMyLightingBlock>();
    
    public Lights(IMyGridTerminalSystem gridTerminalSystem, long gridId) {
        LightList.Clear();
        gridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(LightList, (IMyLightingBlock block) => block.CubeGrid.EntityId == gridId);
    }

    public void TurnOn() 
    {
        foreach(IMyLightingBlock light in LightList) {
            light.Enabled = true;
        }
    }

    public void TurnOff(bool reflectorOnly = false) 
    {
        foreach(IMyLightingBlock light in LightList) {
            if (reflectorOnly == false || light is IMyReflectorLight) {
                light.Enabled = false;
            }
        }
    }
}