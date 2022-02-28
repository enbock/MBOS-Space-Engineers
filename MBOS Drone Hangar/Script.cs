const String NAME = "Drone Hangar";
const String VERSION = "3.2.0";
const String DATA_FORMAT = "2";

public class DroneHangar
{
    public MBOS Sys;
    public MyWaypointInfo FlightIn;

    public class Pod 
    {
        public long ConnectorId;
        public String Name;
        public List<MBOS.ConfigValue> ConfigList = new List<MBOS.ConfigValue>();
        public long Drone = 0L;
        public string Type = "none";
        public DateTime LastSeen = DateTime.Now;

        public IMyShipMergeBlock Connector {
            get {
                return MBOS.Sys.GridTerminalSystem.GetBlockWithId(ConnectorId) as IMyShipMergeBlock; 
            }
        }
        public Pod(IMyShipMergeBlock connector) {
            ConnectorId = connector.EntityId;
            Name = connector.CustomName;

            LoadConfig();
        }

        protected void LoadConfig() {
            MBOS.Sys.LoadConfig(Connector.CustomData, ConfigList);
            String drone = MBOS.Sys.Config("Drone", ConfigList).Value;
            Drone = drone == String.Empty ? 0L : long.Parse(drone);
            Type = MBOS.Sys.Config("Type", ConfigList).Value;
            Type = Type != String.Empty ? Type : "none";
        }

        public void SaveConfig() {
            MBOS.Sys.Config("Drone", ConfigList).Value = Drone.ToString();
            MBOS.Sys.Config("Type", ConfigList).Value = Type;
            MBOS.Sys.SaveConfig(Connector, ConfigList);
        }

    }

    public class DeliveryMission 
    {
        public string MissionId;
        public string DroneType;
        public string FlightPath;
        public long Drone = 0L;
        public Pod Pod;
        public bool Started = false;
        
        public DeliveryMission(string missionId, string droneType, string flightPath) {
            MissionId = missionId;
            DroneType = droneType;
            FlightPath = flightPath;
        }
        
        public DeliveryMission(string data) {
            List<String> parts = new List<String>(data.Split('*'));
            MissionId = parts[0];
            DroneType = parts[1];
            FlightPath = parts[2];
            Drone = long.Parse(parts[3]);
            Started = parts[4] == "Y";
        }

        public override String ToString() {
            return MissionId
                + "*" + DroneType
                + "*" + FlightPath
                + "*" + Drone.ToString()
                + "*" + (Started ? "Y" : "N")
            ;
        }
    }

    public List<Pod> Pods = new List<Pod>();
    public List<DeliveryMission> Missions = new List<DeliveryMission>();
    public long WaitBetweenStarts = 1L; // in seconds
    protected DateTime LastStart = DateTime.Now;

    public DroneHangar(MBOS sys, MyWaypointInfo flightIn) {
        Sys = sys;
        FlightIn = flightIn;

        Init();
        Load();
    }

    public void Init() {
        FindPods();
        BroadCastEmptySlots();
    }

    public void ExecuteMessage(String message) {
        List<String> parts = new List<String>(message.Split('|'));
        String command = parts[0];
        parts.RemoveAt(0);

        switch(command) {
            case "DroneNeedHome":
                RegisterDrone(long.Parse(parts[0]), parts[1]);
                break;
            case "RequestTransport":
                RequestTransport(parts);
                break;
            case "ResetOrders":
                Missions.Clear();
                break;
            case "RequestRenewStationRegister":
                RegisterAtNetwrok();
                break;
        }
    }

    public void RegisterAtNetwrok() {
        Sys.BroadCastTransceiver.SendMessage(
            "RegisterHangar|" + Sys.EntityId + "|" + Sys.GridId + "|" + FlightIn.ToString()
        );
    }

    public void Save() {
        List<String> missions = new List<String>();
        Missions.ForEach((DeliveryMission mission) => missions.Add(mission.ToString()));
        Sys.Config("Missions").Value = String.Join("|", missions.ToArray());
    }

    protected void Load()
    {
        Missions.Clear();
        List<String> missions = new List<String>(Sys.Config("Missions").Value.Split('|'));
        missions.ForEach(delegate(String line) {
            if(line != String.Empty) Missions.Add(new DeliveryMission(line));
        });

        Missions.ForEach(
            delegate(DeliveryMission mission) {
                List<Pod> foundPods = Pods.FindAll((Pod podItem) => podItem.Drone == mission.Drone);
                if (foundPods.Count == 0) return;
                mission.Pod = foundPods[0];
            }
        );
    }

    protected void FindPods() {
        List<IMyShipMergeBlock> connectors = new List<IMyShipMergeBlock>();
        MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(
            connectors, 
            (IMyShipMergeBlock connector) => connector.CustomName.ToLower().IndexOf("pod") > -1
        );

        MBOS.Sys.Echo("Found "+connectors.Count.ToString()+" pods");

        foreach(IMyShipMergeBlock connector in connectors) {
            List<Pod> pods = Pods.FindAll((Pod podItem) => podItem.Connector != null && podItem.Connector.EntityId == connector.EntityId);
            if (pods.Count > 0) continue;
            MBOS.Sys.Echo("Register Pod "+connector.CustomName);
            Pods.Add(new Pod(connector));
        }
    }

    protected void BroadCastEmptySlots() {
        List<Pod> emptyPods = Pods.FindAll((Pod pod) => pod.Drone == 0L);

        MBOS.Sys.Echo("Having " + emptyPods.Count.ToString() + " free pods.");

        if(emptyPods.Count > 0) {
            MBOS.Sys.BroadCastTransceiver.SendMessage("DroneHangarHasPodsAvailable|" + MBOS.Sys.EntityId);
        }

    }

    protected void RegisterDrone(long drone, string type) {
        List<Pod> knownDrones = Pods.FindAll((Pod pod) => pod.Drone == drone);
        foreach(Pod pod in knownDrones) {
            pod.Drone = 0L;
        }
        List<Pod> emptyPods = Pods.FindAll((Pod pod) => pod.Connector != null && pod.Drone == 0L);

        if (emptyPods.Count == 0) {
            MBOS.Sys.Echo("No pod for " + drone.ToString() + " available.");
            return;
        }
        Pod registeredPod = emptyPods[0];
        registeredPod.Drone = drone;
        registeredPod.Type = type;
        registeredPod.SaveConfig();

        Vector3D dockAt = registeredPod.Connector.GetPosition();
        MBOS.Sys.Transceiver.SendMessage(
            registeredPod.Drone, 
            "DroneRegisteredAt|" + MBOS.Sys.EntityId.ToString()
            + "|" + FlightIn.ToString()
            + ">" + (new MyWaypointInfo("Dock At", dockAt)).ToString()
        );

        MBOS.Sys.Echo("Drone " + drone.ToString() + " registered.");
    }

    protected void RequestTransport(List<string> parts) {
        string missionId = parts[0];
        string droneType = parts[1];
        string flightPath = parts[2];
        
        List<DeliveryMission> foundMissions = Missions.FindAll((DeliveryMission missionItem) => missionItem.MissionId == missionId);
        if(foundMissions.Count > 0) {
            MBOS.Sys.Traffic.Add("Mission already registered: " + missionId);
        } else {
            MBOS.Sys.Traffic.Add("Register new mission: " + missionId);
            DeliveryMission newMission = new DeliveryMission(missionId, droneType, flightPath);
            Missions.Add(newMission);
        }

        CheckMissions();
    }

    public void CheckMissions()
    {
        CheckLostPods();
        CheckHomeCommingMissions();
        CheckStartingMissions();
        SearchAssingableMissions();
        UpdateLastSeenDrones();
    }

    public void CheckLostPods()
    {
        Pods.ForEach(
            delegate (Pod pod) {
                if (pod.Connector != null && pod.Connector.IsWorking) return;
                Missions
                    .FindAll((DeliveryMission mission) => mission.Pod == pod)
                    .ForEach(delegate (DeliveryMission mission) {
                        MBOS.Sys.BroadCastTransceiver.SendMessage("MissionCompleted|" + mission.MissionId);
                        Missions.Remove(mission);
                    })
                ;
                Pods.Remove(pod);
            }
        );
    }

    public void CheckHomeCommingMissions(String lostPod = "")
    {
        List<DeliveryMission> runningMissions = Missions.FindAll((DeliveryMission missionItem) => missionItem.Started == true);
        runningMissions.ForEach(
            delegate(DeliveryMission mission) {
                if(
                    mission.Pod != null && mission.Pod.Connector != null 
                    && !mission.Pod.Connector.IsConnected && (lostPod == "" || mission.Pod.Connector.CustomName != lostPod)
                ) return;

                Missions.Remove(mission);
                MBOS.Sys.BroadCastTransceiver.SendMessage("MissionCompleted|" + mission.MissionId);
            }
        );
    }
    
    protected void CheckStartingMissions()
    {
        List<DeliveryMission> startingMissions = Missions.FindAll((DeliveryMission missionItem) => missionItem.Started == false && missionItem.Pod != null);
        
        startingMissions.ForEach(
            delegate(DeliveryMission mission) {
                if (mission.Pod != null && mission.Pod.Connector != null && mission.Pod.Connector.IsConnected) return;
                mission.Started = true;
            }
        );
    }
    
    protected void SearchAssingableMissions()
    {
        if (LastStart > DateTime.Now) return;
        List<DeliveryMission> queuedMissions = Missions.FindAll((DeliveryMission missionItem) => missionItem.Pod == null);
        
        queuedMissions.ForEach(
            delegate(DeliveryMission mission) {
                if (LastStart > DateTime.Now) return;
                
                List<Pod> freePods = Pods.FindAll(
                    delegate(Pod podItem) {
                        if (LastStart > DateTime.Now) return false;

                        IMyProgrammableBlock block = MBOS.Sys.GridTerminalSystem.GetBlockWithId(podItem.Drone) as IMyProgrammableBlock;
                        if (block == null || podItem.Connector == null || !podItem.Connector.IsConnected) {
                            return false;
                        }

                        // batteries charged?
                        List<IMyTerminalBlock> batteries = new List<IMyTerminalBlock>();
                        float current = 0f;
                        float max = 0f;
                        MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(
                            batteries,
                            (IMyTerminalBlock batteryblock) => batteryblock.CubeGrid.EntityId == podItem.Connector.CubeGrid.EntityId
                        );
                        batteries.ForEach(delegate(IMyTerminalBlock batteryblock) {
                            IMyBatteryBlock battery = batteryblock as IMyBatteryBlock;
                            max += battery.MaxStoredPower;
                            current += battery.CurrentStoredPower;
                        });
                        if (max == 0 || (100f / max * current) < 98f) {
                            return false;
                        }

                        // not already assigned?
                        List<DeliveryMission> assignedMissions = Missions.FindAll((DeliveryMission missionItem) => missionItem.Pod == podItem);

                        return assignedMissions.Count == 0;
                    } 
                );
                if (freePods.Count == 0) return;
                Pod pod = freePods[0];
                mission.Drone = pod.Drone;
                mission.Pod = pod;

                LastStart = DateTime.Now.AddSeconds(WaitBetweenStarts);

                MBOS.Sys.Transceiver.SendMessage(mission.Drone, "AddFlightPath|" + mission.FlightPath);
                MBOS.Sys.Transceiver.SendMessage(mission.Drone, "StartDrone");
            }
        );
    }

    private void UpdateLastSeenDrones()
    {
        Pods.ForEach(
            delegate (Pod pod) {
                if (pod.Connector == null || !pod.Connector.IsWorking) {
                    Sys.Echo("Pod damaged: " + pod.Name);
                    return;
                }
                if (pod.Connector.CustomData == String.Empty) {
                    return;
                }
                if (pod.Connector.IsConnected) {
                    pod.LastSeen = DateTime.Now;
                    return;
                }
                Sys.Echo(pod.Name+" last seen "+ (Math.Round(DateTime.Now.Subtract(pod.LastSeen).TotalSeconds).ToString()) +"s ");
            }
        );
    }
}

DroneHangar Hangar;
MBOS Sys;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo, Runtime);

    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    InitProgram();
}

public void Save()
{   
    if (Hangar != null) {
        Hangar.Save();
    }
    Sys.SaveConfig();
}

public void InitProgram() 
{
    Sys.LoadConfig();

    String waypoint = Sys.Config("FlightIn").Value;

    if (waypoint == String.Empty) {
        Echo("Missing flight in point.\nRun FlightIn <GPS>");
        return;
    }

    MyWaypointInfo flightIn = MyWaypointInfo.Empty;
    if(MBOS.ParseGPS(waypoint, out flightIn) == false) {
        Echo("Wrong GPS for FlightIn stored. Run `FlightIn <GPS>`");
        return;
    }

    Hangar = new DroneHangar(Sys, flightIn);
    Hangar.RegisterAtNetwrok();

    Echo("Program initialized.");
}

public void CheckMessages()
{
    String message = string.Empty;
    if((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
        Hangar.ExecuteMessage(message);
    }
    if((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
        Hangar.ExecuteMessage(message);
    }
}

public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (Hangar == null) {
        InitProgram();
        if (Hangar == null) { 
            Echo("Hangar missing!");
            return;
        }
    }

    CheckMessages();
    Hangar.CheckMissions();
    Save();
    UpdateInfo();
}


public void UpdateInfo()
{
    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Station GridID: " + Sys.GridId + "\n"
        + "Flight in point: " + Hangar.FlightIn.ToString() + "\n"
        + "Number of pods: " + Hangar.Pods.Count.ToString() + "\n"
        + "Unused pods: " + Hangar.Pods.FindAll((DroneHangar.Pod pod) => pod.Drone == 0L).Count.ToString() + "\n"
        + "Missions(all): " + Hangar.Missions.Count.ToString() + "\n"
        + "Queued Missions: " + Hangar.Missions.FindAll((DroneHangar.DeliveryMission mission) => mission.Pod == null).Count.ToString() + "\n"
        + "Assigned Missions: " + Hangar.Missions.FindAll((DroneHangar.DeliveryMission mission) => mission.Pod != null && mission.Started == false).Count.ToString() + "\n"
        + "Flying Missions: " + Hangar.Missions.FindAll((DroneHangar.DeliveryMission mission) => mission.Started == true).Count.ToString() + "\n"
        + "UniCast: " + Sys.Transceiver.Buffer.Input.Count.ToString() + " | "+ Sys.Transceiver.Buffer.Output.Count.ToString() +"\n"
        + "BroadCast: " + Sys.BroadCastTransceiver.Buffer.Input.Count.ToString() + " | "+ Sys.BroadCastTransceiver.Buffer.Output.Count.ToString() +"\n"
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
        case "FlightIn":
            MyWaypointInfo gps = MyWaypointInfo.Empty;
            if(MBOS.ParseGPS(allArgs, out gps)) {
                Sys.Config("FlightIn").Value = gps.ToString();
                Hangar = null;
                Echo("Flight In Point set to: " + gps.ToString());
            } else {
                Echo("Error by parsing GPS coordinate");
            }
            break;
        case "ReceiveMessage":
            Sys.Traffic.Add("Received radio data.");
            if(allArgs != string.Empty) Hangar.ExecuteMessage(allArgs);
            break;
        case "ClearMissions": 
            Hangar.Missions.Clear();
            break;
        case "Reset":
            Hangar.Missions.Clear();
            Hangar.Pods.ForEach(delegate (DroneHangar.Pod podItem) { if (podItem.Connector != null) podItem.Connector.CustomData = ""; });
            Hangar.Pods.Clear();
            Hangar.Init();
            break;
        case "FindPods":
            Hangar.Init();
            break;
        case "LostPod":
        case "PodLost":
            List<DroneHangar.Pod> lostPods = Hangar.Pods.FindAll((DroneHangar.Pod podItem) => podItem.Connector != null && podItem.Connector.CustomName == allArgs);
            if (lostPods.Count == 0) {
                Echo("Pod " + allArgs + " not found.");
                break;
            }
            Hangar.CheckHomeCommingMissions(allArgs);
            Hangar.Pods.Remove(lostPods[0]);
            if(lostPods[0].Connector != null) lostPods[0].Connector.CustomData = "";
            Echo("Pod " + allArgs + " removed from system.");
            Hangar.Init();
            break;
        case "RestartMissions":
            Hangar.Missions.ForEach(
                delegate (DroneHangar.DeliveryMission mission) {
                    if (mission.Pod.Connector == null) return;
                    if (mission.Pod.Connector.IsConnected) {
                        Echo("Drone seems to fly: "+mission.Pod.Connector.CustomName);
                    }
                    Echo("Restart drone: "+mission.Pod.Connector.CustomName+"("+mission.Drone+")");
                    MBOS.Sys.Transceiver.SendMessage(mission.Drone, "AddFlightPath|" + mission.FlightPath);
                    MBOS.Sys.Transceiver.SendMessage(mission.Drone, "StartDrone");
                }
            );
            break;
        default:
            Echo("Available Commands:\n   * FlightIn <GPS>\n   * FindPods\n   * LostPod <Exact name of pod>\n   * RestartMission\n");
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
    public IMyGridProgramRuntimeInfo Runtime;
    public int UpdatesBetweenMessages = 10;

    public long GridId { get { return Me.CubeGrid.EntityId; }}
    public long EntityId { get { return Me.EntityId; }}

    public List<ConfigValue> ConfigList = new List<ConfigValue>();
    public IMyTextSurface ComputerDisplay;

    protected bool ConfigLoaded = false;
    public List<String> Traffic = new List<String>();

    public MBOS(IMyProgrammableBlock me, IMyGridTerminalSystem gridTerminalSystem, IMyIntergridCommunicationSystem igc, Action<string> echo, IMyGridProgramRuntimeInfo runtime) {
        Me = me;
        GridTerminalSystem = gridTerminalSystem;
        IGC = igc;
        Echo = echo;
        Runtime = runtime;
        
        ComputerDisplay = Me.GetSurface(0);
        ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
        ComputerDisplay.ClearImagesFromSelection();
        ComputerDisplay.ChangeInterval = 0;

        MBOS.Sys = this;
        Transceiver = new UniTransceiver(this, Traffic);
        BroadCastTransceiver = new WorldTransceiver(this, Traffic);
    }

    public ConfigValue Config(String key) {
        return Config(key, ConfigList);
    } 
    public ConfigValue Config(String key, List<ConfigValue> configList) {
        ConfigValue config = configList.Find(x => x.Key == key);
        if(config != null) return config;
        
        ConfigValue newValue = new ConfigValue(key, String.Empty); 
        configList.Add(newValue); 
        return newValue; 
    } 

    public IMyTerminalBlock GetBlock(String id)
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
        if(block == null || block.CubeGrid.EntityId != GridId) {
            Echo("Don't found: " + cubeId.ToString() + " on " + GridId.ToString());
            return null;
        }

        return block;
    }

    public IMyTerminalBlock GetBlockByName(String name)
    {   
        IMyTerminalBlock block = GridTerminalSystem.GetBlockWithName(name);
        if(block == null || block.CubeGrid.EntityId != GridId) {
            Echo("Don't found: " + name + " on " + GridId.ToString());
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
        
        LoadConfig(Me.CustomData, ConfigList);
        Transceiver.Buffer = new UniTransceiver.NetBuffer(Config("UniTransceiver").Value);
        BroadCastTransceiver.Buffer = new WorldTransceiver.NetBuffer(Config("WorldTransceiver").Value);
    } 

    public void LoadConfig(String data, List<ConfigValue> configList, bool ignoreHead = false)
    {   
        if (data.Length > 0) { 
            String[] configs = data.Split('\n'); 
            
            if(!ignoreHead && configs[0] != "FORMAT v" + DATA_FORMAT) return;
            
            for(int i = (ignoreHead ? 0 : 1); i < configs.Length; i++) {
                String line = configs[i]; 
                if (line.Length > 0) {
                    String[] parts = line.Split('=');
                    if(parts.Length != 2) continue;
                    ConfigValue config = Config(parts[0].Trim(), configList);
                    config.Value = config.Value != String.Empty ? config.Value : parts[1].Trim();
                }
            } 
        } 
    }

    public void SaveConfig()
    {
        Config("UniTransceiver").Value = Transceiver.Buffer.ToString();
        Config("WorldTransceiver").Value = BroadCastTransceiver.Buffer.ToString();
        SaveConfig(Me, ConfigList);
    }

    public void SaveConfig(IMyTerminalBlock block, List<ConfigValue> configList)
    {
        List<String> store = new List<String>();  
        int i; 

        for(i = 0; i < configList.Count; i++) { 
            store.Add(configList[i].ToString()); 
        } 
        
        block.CustomData = "FORMAT v" + DATA_FORMAT + "\n" + String.Join("\n", store.ToArray()); 
    }

    public class WorldTransceiver {
        public class NetBuffer 
        {
            public List<String> Input = new List<String>();
            public List<String> Output = new List<String>();

            public NetBuffer() {}

            public NetBuffer(String import)
            {
                if (import == String.Empty) return;

                String[] io = import.Trim().Split('µ');
                if(io.Length < 2) return;
                if (io[0].Length > 0)  Input = new List<String>(io[0].Split('§'));
                if (io[1].Length > 0) Output = new List<String>(io[1].Split('§'));
            }

            public override String ToString()
            {
                return String.Join("§", Input.ToArray()) + "µ" + String.Join("§", Output.ToArray());
            }
        }
        
        public String Channel;
        public IMyBroadcastListener BroadcastListener;

        protected MBOS Sys;
        protected TransmissionDistance Range = TransmissionDistance.AntennaRelay;
        protected String LastSendData = "";
        protected List<String> Traffic = new List<String>();
        public NetBuffer Buffer = new NetBuffer();
        private int SendCount = 0;
        private int SendInterval;

        public WorldTransceiver(MBOS sys, List<String> traffic) {
            Sys = sys;
            Traffic = traffic;
            Channel = "world";
            UpdateSendInterval();
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
            String message = string.Empty;
            while((message = DownloadMessage()) != string.Empty) {
                Buffer.Input.Add(message);
            }
            UpdateSendInterval();
            SendCount++;
            if (SendCount > SendInterval) {
                UploadMessage();
                SendCount = 0;
            }

            if (Buffer.Input.Count == 0) return String.Empty;
            
            message = Buffer.Input[0];
            Buffer.Input.RemoveAt(0);
            Traffic.Add("[B+]< " + message);

            return message;
        }

        private void UpdateSendInterval() {
            SendInterval = 
                Sys.Runtime.UpdateFrequency == UpdateFrequency.Update10 
                    ? Sys.UpdatesBetweenMessages 
                    : (Sys.Runtime.UpdateFrequency == UpdateFrequency.Update100 ? Sys.UpdatesBetweenMessages / 10 : Sys.UpdatesBetweenMessages * 10)
            ;
        }

        private String DownloadMessage() {
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
            Traffic.Add("[B_]< " + messageText);

            return messageText;
        }

        public void SendMessage(String data) 
        {
            Buffer.Output.Add(data);
            Traffic.Add("[B+]> " + data);
        }

        private void UploadMessage() 
        {
            if (Buffer.Output.Count == 0) return;

            String messageText = Buffer.Output[0];
            Buffer.Output.RemoveAt(0);

            String message = DateTime.Now.ToBinary() + "|" + messageText;
            
            Sys.IGC.SendBroadcastMessage<String>(Channel, message, Range);
            LastSendData = message;
            Traffic.Add("[B_]> " + messageText);
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
        public class SendMessageInfo
        {
            public long Receiver = 0L;
            public String Data = String.Empty; 
            public bool ReceiveMustBeInGrid = false;

            public SendMessageInfo(long receiver, String data)
            {
                Receiver = receiver;
                Data = data;
            }

            public SendMessageInfo(String import)
            {
                String[] io = import.Trim().Split('\\');
                Receiver = long.Parse(io[0]);
                Data = io[1];
            }

            public override String ToString() {
                return Receiver.ToString() + "\\" + Data;
            }
        }

        public class NetBuffer 
        {
            public List<String> Input = new List<String>();
            public List<SendMessageInfo> Output = new List<SendMessageInfo>();

            public NetBuffer() {}

            public NetBuffer(String import)
            {
                if (import == String.Empty) return;

                String[] io = import.Trim().Split('µ');
                if(io.Length < 2) return;
                if (io[0].Length > 0) Input = new List<String>(io[0].Split('§'));

                if (io[1].Length == 0) return;
                Output.Clear(); 
                List<String> output = new List<String>(io[1].Split('§'));
                output.ForEach((String outputMessage) => Output.Add(new SendMessageInfo(outputMessage)));
            }

            public override String ToString()
            {
                List<String> output = new List<String>();
                Output.ForEach((SendMessageInfo info) => output.Add(info.ToString()));

                return String.Join("§", Input.ToArray()) + "µ" + String.Join("§", output.ToArray());
            }
        }

        protected IMyUnicastListener Listener;
        protected MBOS Sys;
        protected List<String> Traffic = new List<String>();
        public NetBuffer Buffer = new NetBuffer();
        private int SendCount = 0;
        private int SendInterval;

        public UniTransceiver(MBOS sys, List<String> traffic)
        {
            Sys = sys;
            Traffic = traffic;
            UpdateSendInterval();

            Listener = Sys.IGC.UnicastListener;
            Listener.SetMessageCallback("ReceiveMessage");
        }
        

        public String ReceiveMessage()
        { 
            String message = string.Empty;
            while((message = DownloadMessage()) != string.Empty) {
                Buffer.Input.Add(message);
            }
            UpdateSendInterval();
            SendCount++;
            if (SendCount > SendInterval) {
                UploadMessage();
                SendCount = 0;
            }

            if (Buffer.Input.Count == 0) return String.Empty;
            
            message = Buffer.Input[0];
            Buffer.Input.RemoveAt(0);
            Traffic.Add("[U+]< " + message);

            return message;
        }
        
        private void UpdateSendInterval() {
            SendInterval = 
                Sys.Runtime.UpdateFrequency == UpdateFrequency.Update10 
                    ? Sys.UpdatesBetweenMessages 
                    : (Sys.Runtime.UpdateFrequency == UpdateFrequency.Update100 ? Sys.UpdatesBetweenMessages / 10 : Sys.UpdatesBetweenMessages * 10)
            ;
        }

        private String DownloadMessage()
        {
            if (!Listener.HasPendingMessage) return String.Empty;

            MyIGCMessage message = Listener.AcceptMessage();
            String incoming = message.As<String>();

            Traffic.Add("[U_]< " + incoming);

            return incoming;
        }

        public void SendMessage(long target, String data, bool receiveMustBeInGrid = false) 
        {
            SendMessageInfo info = new SendMessageInfo(target, data);
            info.ReceiveMustBeInGrid = receiveMustBeInGrid;
            Buffer.Output.Add(info);
            Traffic.Add("[U+]> " + data);
        }

        private void UploadMessage() 
        {
            if (Buffer.Output.Count == 0) return;
            SendMessageInfo info = Buffer.Output[0];
            Buffer.Output.RemoveAt(0);

            if (info.ReceiveMustBeInGrid && Sys.GridTerminalSystem.GetBlockWithId(info.Receiver) == null) {
                Traffic.Add("[U<]> " + info.Data);
                Buffer.Output.Add(info);
                return;
            }

            Traffic.Add("[U_]> " + info.Data);
            Sys.IGC.SendUnicastMessage<string>(info.Receiver, "whisper", info.Data);
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

    public static bool ParseGPS(String gpsData, out MyWaypointInfo gps) {
        return MyWaypointInfo.TryParse(gpsData, out gps);
    }
}
