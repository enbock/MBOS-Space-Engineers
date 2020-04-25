const String NAME = "Flight Control";
const String VERSION = "1.0.0";
const String DATA_FORMAT = "1";

public class FlightControl
{
    public class Station {
        public long EntityId;
        public long GridId;
        public MyWaypointInfo FlightIn = MyWaypointInfo.Empty;

        public Station(long entityId, long gridId, MyWaypointInfo flightIn) {
            EntityId = entityId;
            GridId = gridId;
            FlightIn = flightIn;
        }

        public Station(string data) {
            List<String> parts = new List<String>(data.Split('*'));
            EntityId = long.Parse(parts[0]);
            GridId = long.Parse(parts[1]);

            MyWaypointInfo waypoint = MyWaypointInfo.Empty;
            MBOS.ParseGPS(parts[2], out waypoint);
            FlightIn = waypoint;
        }

        public override String ToString() {
            return EntityId.ToString()
                + "*" + GridId.ToString()
                + "*" + FlightIn.ToString()
            ;
        }
    }

    public class Hangar : Station {
        public Dictionary<string, int> DroneStock = new Dictionary<string, int>();

        public Hangar(long entityId, long gridId, MyWaypointInfo flightIn) : base (entityId, gridId, flightIn) {}
        public Hangar(string data) : base (data) {
            List<String> parts = new List<String>(data.Split('*'));

            List<string> droneStock = new List<string>(parts[3].Split('°'));
            droneStock.ForEach(
                delegate(string pair) {
                    if(pair == string.Empty) return;
                    List<String> keyValue = new List<String>(data.Split('+'));
                    DroneStock.Add(keyValue[0], int.Parse(keyValue[1]));
                }
            );
        }

        public override String ToString() {
            String output = base.ToString();
            
            List<string> droneStock = new List<string>();
            foreach(KeyValuePair<string, int> pair in DroneStock) {
                droneStock.Add(pair.Key + "+" + pair.Value);
            }
            output += "*" + string.Join("°", droneStock.ToArray());

            return output;
        }
    }

    public class FlightPath {
        public long StartGridId;
        public long TargetGridId;
        public List<MyWaypointInfo> Waypoints = new List<MyWaypointInfo>();
    }

    MBOS Sys;
    public List<Hangar> Hangars = new List<Hangar>();
    public List<Station> Stations = new List<Station>();
    public MyWaypointInfo FallBackFlightPathWaypoint = MyWaypointInfo.Empty;

    public FlightControl(MBOS sys, MyWaypointInfo fallbackFleightPathWaypoint) {
        Sys = sys;
        FallBackFlightPathWaypoint = fallbackFleightPathWaypoint;

        Load();
    }

    public void Save() {
        List<String> list = new List<String>();
        Stations.ForEach((Station station) => list.Add(station.ToString()));
        Sys.Config("Stations").Value = String.Join("|", list.ToArray());

        list.Clear();
        Hangars.ForEach((Hangar hangar) => list.Add(hangar.ToString()));
        Sys.Config("Hangars").Value = String.Join("|", list.ToArray());
    }

    public void SendReady() {
        Sys.BroadCastTransceiver.SendMessage("FlightControlReady|"+Sys.EntityId);
    }

    public void ExecuteMessage(string message) {
        List<String> parts = new List<String>(message.Split('|'));
        String command = parts[0];
        parts.RemoveAt(0);

        switch(command) {
            case "RegisterStation":
                RegisterStation(long.Parse(parts[0]), long.Parse(parts[1]), parts[2]);
                break;
            case "RegisterHangar":
                RegisterHangar(long.Parse(parts[0]), long.Parse(parts[1]), parts[2]);
                break;
        }
    }

    protected void Load() {
        List<String> list = new List<String>(Sys.Config("Stations").Value.Split('|'));
        list.ForEach(delegate(String line) {
            if(line != String.Empty) Stations.Add(new Station(line));
        });
        
        list = new List<String>(Sys.Config("Hangars").Value.Split('|'));
        list.ForEach(delegate(String line) {
            if(line != String.Empty) Hangars.Add(new Hangar(line));
        });
    }

    protected void RegisterStation(long entityId, long gridId, string flightInGps) {
        MyWaypointInfo flightIn = MyWaypointInfo.Empty;
        MBOS.ParseGPS(flightInGps, out flightIn);
        Station newStation;
        List<Station> foundStations = Stations.FindAll((Station station) => station.GridId == gridId);
        if (foundStations.Count > 0) {
            newStation = foundStations[0];
            newStation.FlightIn = flightIn;
            newStation.EntityId = entityId;
        } else {
            newStation = new Station(entityId, gridId, flightIn);
            Stations.Add(newStation);
        }
    }

    protected void RegisterHangar(long entityId, long gridId, string flightInGps) {
        MyWaypointInfo flightIn = MyWaypointInfo.Empty;
        MBOS.ParseGPS(flightInGps, out flightIn);
        Hangar newHangar;
        List<Hangar> foundHangars = Hangars.FindAll((Hangar hangar) => hangar.GridId == gridId);
        if (foundHangars.Count > 0) {
            newHangar = foundHangars[0];
            newHangar.FlightIn = flightIn;
            newHangar.EntityId = entityId;
        } else {
            newHangar = new Hangar(entityId, gridId, flightIn);
            Hangars.Add(newHangar);
        }
    }
}

MBOS Sys;
FlightControl FlightController;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo);
    Runtime.UpdateFrequency = UpdateFrequency.None;

    InitProgram();
    UpdateInfo();
    Save();
}

public void Save()
{
    if (FlightController != null) FlightController.Save();
    
    Sys.SaveConfig();
}

public void InitProgram() 
{
    Sys.LoadConfig();

    string fallbackWaypointGps = Sys.Config("FallBackFlightPathWaypoint").Value;

    if (fallbackWaypointGps == string.Empty) {
        Echo("Fallback waypoint needed. Run `SetFallback <GPS>`");
        return;
    }
    MyWaypointInfo fallbackWaypoint = MyWaypointInfo.Empty;
    if(MBOS.ParseGPS(fallbackWaypointGps, out fallbackWaypoint) == false) {
        Echo("Wrong fallback waypoint. Run `SetFallback <GPS>`");
        return;
    }

    FlightController = new FlightControl(Sys, fallbackWaypoint);
    FlightController.SendReady();

    Runtime.UpdateFrequency = UpdateFrequency.None; //UpdateFrequency.Update100;
    Echo("Program initialized.");
}

public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (FlightController == null) {
        InitProgram();
    }
    if (FlightController == null) {
        return;
    }

    Save();
    UpdateInfo();
}

public void UpdateInfo()
{
    if (FlightController == null) {
        Sys.ComputerDisplay.WriteText("", false);
        return;
    }
    //FlightController.UpdateScreen();

    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Stations: " + FlightController.Stations.Count.ToString() + "\n"
        + "Hangars: " + FlightController.Hangars.Count.ToString() + "\n"
        + "Fallback FP: " + FlightController.FallBackFlightPathWaypoint.ToString() + "\n"
        + "----------------------------------------\n"
        + Sys.Transceiver.DebugTraffic() + "\n"
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
        case "ReceiveMessage":
            String message = string.Empty;
            while((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
                FlightController.ExecuteMessage(message);
            }
            while((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
                FlightController.ExecuteMessage(message);
            }
            break;
        case "Reset":
            if(FlightController == null) break;
            FlightController.Hangars.Clear();
            FlightController.Stations.Clear();
            FlightController.SendReady();
            break;
        case "SetFallback":
            MyWaypointInfo gps = MyWaypointInfo.Empty;
            if(MBOS.ParseGPS(allArgs, out gps)) {
                Sys.Config("FallBackFlightPathWaypoint").Value = gps.ToString();
                FlightController = null;
                Echo("Fallback flightpath set over: " + gps.ToString());
            } else {
                Echo("Error by parsing GPS coordinate");
            }
            break;

         /*case "SetLCD":
            IMyTextPanel lcd = Sys.GetBlockByName(allArgs) as IMyTextPanel;
            if(lcd != null) {
                FlightController.Screen = lcd;
            }
            break;*/
        default:
            Echo("Available Commands: \n  * SetLCD <Name of Panel>");
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
