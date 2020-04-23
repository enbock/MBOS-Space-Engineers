const String NAME = "Drone Hangar";
const String VERSION = "1.0.0";
const String DATA_FORMAT = "1.0";

/**

Drone Hangar Manager.

* Connectors must be named with "Pod".

*/

public class DroneHangar
{
    public class Pod {
        public IMyShipConnector Connector;
        public List<MBOS.ConfigValue> ConfigList = new List<MBOS.ConfigValue>();
        public long Drone = 0L;

        public Pod(IMyShipConnector connector) {
            Connector = connector;

            LoadConfig();
            SaveConfig(); // init
        }

        protected void LoadConfig() {
            MBOS.Sys.LoadConfig(Connector.CustomData, ConfigList);
            String drone = MBOS.Sys.Config("Drone", ConfigList).Value;
            Drone = drone == String.Empty ? 0L : long.Parse(drone);
        }

        public void SaveConfig() {
            MBOS.Sys.Config("Drone", ConfigList).Value = Drone.ToString();
            MBOS.Sys.SaveConfig(Connector, ConfigList);
        }

    }

    public List<Pod> Pods = new List<Pod>();
    public MyWaypointInfo FlightInPoint = MyWaypointInfo.Empty;

    public DroneHangar(String flightInPoint) {
        MBOS.ParseGPS(flightInPoint, out FlightInPoint);
        FindPods();
        BroadCastEmptySlots();
    }

    public void ExecuteMessage(String message) {
        List<String> parts = new List<String>(message.Split('|'));
        String command = parts[0];
        parts.RemoveAt(0);

        switch(command) {
            case "DroneNeedHome":
                RegisterDrone(long.Parse(parts[0]));
                break;
        }
    }

    protected void FindPods() {
        Pods.Clear();
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(
            connectors, 
            (IMyShipConnector connector) => connector.CubeGrid.EntityId == MBOS.Sys.GridId && connector.CustomName.ToLower().IndexOf("pod") > -1
        );

        MBOS.Sys.Echo("Found "+connectors.Count.ToString()+" connectors");

        foreach(IMyShipConnector connector in connectors) {
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

    protected void RegisterDrone(long drone) {
        List<Pod> knownDrones = Pods.FindAll((Pod pod) => pod.Drone == drone);
        foreach(Pod pod in knownDrones) {
            pod.Drone = 0L;
        }
        List<Pod> emptyPods = Pods.FindAll((Pod pod) => pod.Drone == 0L);

        if (emptyPods.Count == 0) {
            MBOS.Sys.Echo("No pod for " + drone.ToString() + " available.");
        }
        Pod registeredPod = emptyPods[0];
        registeredPod.Drone = drone;
        registeredPod.SaveConfig();

        Vector3D dockAt = registeredPod.Connector.CubeGrid.GridIntegerToWorld(registeredPod.Connector.Position);
        MBOS.Sys.Transceiver.SendMessage(
            registeredPod.Drone, 
            "DroneRegisteredAt|" + MBOS.Sys.EntityId.ToString()
            + "|" + FlightInPoint.ToString()
            + ">" + (new MyWaypointInfo("Dock At", dockAt)).ToString()
        );
        
        MBOS.Sys.Echo("Drone " + drone.ToString() + " registered.");
    }
}

DroneHangar Hangar;
MBOS Sys;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo);
    Runtime.UpdateFrequency = UpdateFrequency.None;

    InitProgram();
}

public void Save()
{
    if(Hangar != null) {
        //Sys.Config("RemoteControl").Block = Drone.RemoteControl;
    }
    
    Sys.SaveConfig();
}

public void InitProgram() 
{
    Sys.LoadConfig();

    String waypoint = Sys.Config("FlightInPoint").Value;

    if (waypoint == String.Empty) {
        Echo("Missing flight in point.\nRun FlightIn <GPS>");
        return;
    }

    Hangar = new DroneHangar(waypoint);

    Runtime.UpdateFrequency = UpdateFrequency.None; //UpdateFrequency.Update100;
    Echo("Program initialized.");
}


public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (Hangar == null) {
        InitProgram();
        if (Hangar == null) { 
            Runtime.UpdateFrequency = UpdateFrequency.None;
            return;
        }
    }

    Save();
    UpdateInfo();
}


public void UpdateInfo()
{
    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Flight in point: " + Hangar.FlightInPoint.ToString() + "\n"
        + "Number of pods: " + Hangar.Pods.Count.ToString() + "\n"
        + "----------------------------------------\n"
        + Sys.Transceiver.DebugTraffic() +"\n"
        + "----------------------------------------\n"
        + Sys.BroadCastTransceiver.DebugTraffic()
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
            Echo(">>>:"+ allArgs+"|");
            if(MBOS.ParseGPS(allArgs, out gps)) {
                Sys.Config("FlightInPoint").Value = gps.ToString();
                Hangar = null;
                Echo("Flight In Point set to: " + gps.ToString());
            } else {
                Echo("Error by parsing GPS coordinate");
            }
            break;
        case "ReceiveMessage":
            Echo("Received radio data.");
            String message = string.Empty;
            while((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
                Hangar.ExecuteMessage(message);
            }
            while((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
                Hangar.ExecuteMessage(message);
            }
            break;
        default:
            Echo("Available Commands: ");
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
        Transceiver = new UniTransceiver(this);
        BroadCastTransceiver = new WorldTransceiver(this);
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
            Echo("Dont found:" + cubeId.ToString() + " on " + GridId.ToString());
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
    } 

    public void LoadConfig(String data, List<ConfigValue> configList)
    {   
        if (data.Length > 0) { 
            String[] configs = data.Split('\n'); 
            
            if(configs[0] != "FORMAT v" + DATA_FORMAT) return;
            
            for(int i = 1; i < configs.Length; i++) {
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
        
        public String Channel;
        public IMyBroadcastListener BroadcastListener;

        protected MBOS Sys;
        protected TransmissionDistance Range = TransmissionDistance.AntennaRelay;
        protected String LastSendData = "";
        protected List<String> Traffic = new List<String>();

        public WorldTransceiver(MBOS sys) {
            Sys = sys;
            Channel = "world";

            ListenerAware();
        }

        protected void ListenerAware()
        {
            List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
            Sys.IGC.GetBroadcastListeners(listeners, (IMyBroadcastListener listener) => listener.Tag == Channel && listener.IsActive);

            //if (BroadcastListener != null) {
            //    Sys.IGC.DisableBroadcastListener(BroadcastListener);
            //}

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
            Traffic.Add("< " + messageText);

            return messageText;
        }

        public void SendMessage(String data) 
        {
            String message = DateTime.Now.ToBinary() + "|" + data;
            Sys.IGC.SendBroadcastMessage<String>(Channel, message, Range);
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

    public class UniTransceiver
    {
        protected IMyUnicastListener Listener;
        protected MBOS Sys;
        protected List<String> Traffic = new List<String>();

        public UniTransceiver(MBOS sys)
        {
            Sys = sys;
            Listener = Sys.IGC.UnicastListener;
            Listener.SetMessageCallback("ReceiveMessage");
        }
        

        public String ReceiveMessage()
        { 
            if (!Listener.HasPendingMessage) return String.Empty;

            MyIGCMessage message = Listener.AcceptMessage();
            String incoming = message.As<String>();

            Traffic.Add("< " + incoming);

            return incoming;
        }

        public void SendMessage(long target, String data) 
        {
            Traffic.Add("> " + data);
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

    public static bool ParseGPS(String gpsData, out MyWaypointInfo gps) {
        return MyWaypointInfo.TryParse(gpsData, out gps);
    }
}
