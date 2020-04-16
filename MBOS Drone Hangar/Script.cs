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
        public String Drone;

        public Pod(IMyShipConnector connector) {
            Connector = connector;

            LoadConfig();
            SaveConfig(); // init
        }

        protected void LoadConfig() {
            MBOS.Sys.LoadConfig(Connector.CustomData, ConfigList);
            Drone = MBOS.Sys.Config("Drone", ConfigList).Value;
        }

        public void SaveConfig() {
            MBOS.Sys.Config("Drone", ConfigList).Value = Drone;
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

    protected void FindPods() {
        Pods.Clear();
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(
            connectors, 
            (IMyShipConnector connector) => connector.CubeGrid.EntityId == MBOS.Sys.EntityId && connector.CustomName.ToLower().IndexOf("pod") > -1
        );

        MBOS.Sys.Echo("Found "+connectors.Count.ToString()+" connectors");

        foreach(IMyShipConnector connector in connectors) {
            Pods.Add(new Pod(connector));
        }
    }

    protected void BroadCastEmptySlots() {
        List<Pod> emptyPods = Pods.FindAll((Pod pod) => pod.Drone == string.Empty);

        MBOS.Sys.Echo("Having " + emptyPods.Count.ToString() + " free pods.");

        if(emptyPods.Count > 0) {
            MBOS.Sys.Transceiver.SendMessage("DroneHangarHasPodsAvailable|" + MBOS.Sys.EntityId);
        }

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

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
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
        + "\n"
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
            while((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {

            }
            break;
        default:
            Echo("Available Commands: ");
            break;
    }
}

public class MBOS {
    public static MBOS Sys;
    
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

    public String Name = "Module";
    public String Version = "0.0.0";
    public String DataFormat = "0";
    public IMyProgrammableBlock Me;
    public IMyGridTerminalSystem GridTerminalSystem;
    public IMyIntergridCommunicationSystem IGC;
    public Action<string> Echo;
    public UniTransceiver Transceiver;

    public long EntityId { get { return Me.CubeGrid.EntityId; }}

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
        if(block == null || block.CubeGrid.EntityId != EntityId) {
            Echo("Dont found:" + cubeId.ToString() + " on " + EntityId.ToString());
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
            Sys.IGC.SendUnicastMessage<String>(target, "whisper", data);
        }

        public void SendMessage(String data) 
        {
            Traffic.Add("> " + data);
            Sys.IGC.SendBroadcastMessage<String>("world", data, TransmissionDistance.AntennaRelay);
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
