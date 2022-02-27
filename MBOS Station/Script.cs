const String NAME = "Station";
const String VERSION = "1.3.0";
const String DATA_FORMAT = "1";

public class Station
{
    public MBOS Sys;
    public MyWaypointInfo FlightIn;

    public Station(MBOS sys, MyWaypointInfo flightIn) {
        Sys = sys;
        FlightIn = flightIn;
    }

    public void RegisterAtNetwrok() {
        Sys.BroadCastTransceiver.SendMessage(
            "RegisterStation|" + Sys.EntityId + "|" + Sys.GridId + "|" + FlightIn.ToString()
        );
    }

    public void ExecuteMessage(string message) {
        List<String> parts = new List<String>(message.Split('|'));
        String command = parts[0];
        parts.RemoveAt(0);

        switch(command) {
            case "RequestRenewStationRegister":
                RegisterAtNetwrok();
                break;
        }
    }
}

MBOS Sys;
Station StationSystem;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo, Runtime);

    InitProgram();
    UpdateInfo();
    Save();
}

public void Save()
{
    //StationSystem.Save();
    
    Sys.SaveConfig();
}

public void InitProgram() 
{
    Sys.LoadConfig();

    string flightInGps = Sys.Config("FlightIn").Value;

    if (flightInGps == string.Empty) {
        Echo("Missing flight in point. Run `FlightIn <GPS>`");
        return;
    }

    MyWaypointInfo flightIn = MyWaypointInfo.Empty;
    if(MBOS.ParseGPS(flightInGps, out flightIn) == false) {
        Echo("Wrong GPS for FlightIn stored. Run `FlightIn <GPS>`");
        return;
    }

    StationSystem = new Station(Sys, flightIn);
    StationSystem.RegisterAtNetwrok();

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    Echo("Program initialized.");
}

public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (StationSystem == null) {
        InitProgram();
    }

    if (StationSystem == null) {
        return;
    }

    CheckMessages();
    Save();
    UpdateInfo();
}

public void CheckMessages()
{
    String message = string.Empty;
    if((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
        StationSystem.ExecuteMessage(message);
    }
    if((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
        StationSystem.ExecuteMessage(message);
    }
}

public void UpdateInfo()
{
    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Station GridID: " + Sys.GridId + "\n"
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
        case "ReceiveMessage":
            break;
        case "FlightIn":
            MyWaypointInfo gps = MyWaypointInfo.Empty;
            if(MBOS.ParseGPS(allArgs, out gps)) {
                Sys.Config("FlightIn").Value = gps.ToString();
                StationSystem = null;
                Echo("Flight In Point set to: " + gps.ToString());
            } else {
                Echo("Error by parsing GPS coordinate");
            }
            break;
        /*case "SetLCD":
            IMyTextPanel lcd = Sys.GetBlockByName(allArgs) as IMyTextPanel;
            if(lcd != null) {
                StationSystem.Screen = lcd;
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
    public IMyGridProgramRuntimeInfo Runtime;

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
            SendInterval = Sys.Runtime.UpdateFrequency == UpdateFrequency.Update10 ? 10 : (Sys.Runtime.UpdateFrequency == UpdateFrequency.Update100 ? 0 : 100);
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
            SendInterval = Sys.Runtime.UpdateFrequency == UpdateFrequency.Update10 ? 10 : (Sys.Runtime.UpdateFrequency == UpdateFrequency.Update100 ? 0 : 100);
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
