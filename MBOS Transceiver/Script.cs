const String NAME = "Transceiver";
const String VERSION = "2.0.0";
const String DATA_FORMAT = "2";

public class WorldTransceiver : MBOS.GridTransceiver {
    protected bool CanInit = false;

    public WorldTransceiver(MBOS sys, String channel = "world") : base(sys)
    {
        Channel = channel;

        CanInit = true;
        ListenerAware();
    }

    protected new void ListenerAware()
    {
        if(!CanInit) return;
        //List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
        //Sys.IGC.GetBroadcastListeners(listeners, (IMyBroadcastListener listener) => listener.Tag == Channel && IsActive);

        if (BroadcastListener != null) {
            Sys.IGC.DisableBroadcastListener(BroadcastListener);
        }

        BroadcastListener = Sys.IGC.RegisterBroadcastListener(Channel);
        BroadcastListener.SetMessageCallback("GetWorldMessage");
    }
}

MBOS Sys;
WorldTransceiver Transceiver;

public void Save()
{   
   Sys.SaveConfig();
}

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo);
    Transceiver = new WorldTransceiver(Sys, Sys.Config("channel").ValueWithDefault("world"));
    Runtime.UpdateFrequency = UpdateFrequency.None;

    Main("", UpdateType.Once);
}

public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);
    
    string output = "[MBOS]  [" + DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Traffic (Channel: '" + Transceiver.Channel + "'):\n"
        + Transceiver.DebugTraffic()
    ;
    Sys.ComputerDisplay.WriteText(output, false);
}

public void ReadArgument(String args) 
{
    if (args == String.Empty) return;
     
    List<String> parts = new List<String>(args.Split(' ')); 
    String command = parts[0].Trim();
    parts.RemoveAt(0);
    switch (command) {
        case "SetChannel":
            //SetChannel(parts[0]);
            Echo("Channel changed.");
            break;
        case "SendMessage":
            //SendMessage(String.Join(" ", parts.ToArray()));
            Echo("Message sent.");
            break;
        case "SendDirectMessage":
            //SendDirectMessage(parts);
            Echo("Direct Message sent.");
            break;
        case "GetBroadcaseMessage":
            Sys.Transceiver.ReceiveMessage();
            Echo("Grid Message received.");
            break;
        case "GetWorldMessage":
            Transceiver.ReceiveMessage();
            Echo("World Message received.");
            break;
        case "GetUniMessage":
            Sys.DirectTranceiver.ReceiveMessage();
            Echo("Direct Message received.");
            break;
        default:
            Echo(
                "Available commands:\n"
                + "  * SetChannel <new channel name>\n"
                + "  * SendMessage <Message text>\n"
                + "  * ReadMessages\n"
            );
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
            get { 
                return Sys.GetBlock(_value);
            }
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

    public IMyProgrammableBlock Me;
    public IMyGridTerminalSystem GridTerminalSystem;
    public IMyIntergridCommunicationSystem IGC;
    public Action<string> Echo;
    public GridTransceiver Transceiver;
    public UniTransceiver DirectTranceiver;

    public long EntityId { get { return Me.CubeGrid.EntityId; }}

    public List<ConfigValue> ConfigList = new List<ConfigValue>();
    public IMyTextSurface ComputerDisplay;

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
        Transceiver = new GridTransceiver(this);
        DirectTranceiver = new UniTransceiver(this);
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

    public class GridTransceiver {
        
        public String Channel;
        public IMyBroadcastListener BroadcastListener;
        public String MyId { get { return Sys.EntityId.ToString(); }}

        protected MBOS Sys;
        protected TransmissionDistance Range = TransmissionDistance.CurrentConstruct;
        protected String LastSendData = "";
        protected List<String> Traffic = new List<String>();

        public GridTransceiver(MBOS sys) {
            Sys = sys;
            Channel = "Grid#" + MyId;

            ListenerAware();
        }

        protected void ListenerAware()
        {
            //List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
            //Sys.IGC.GetBroadcastListeners(listeners, (IMyBroadcastListener listener) => listener.Tag == Channel && IsActive);

            if (BroadcastListener != null) {
                Sys.IGC.DisableBroadcastListener(BroadcastListener);
            }

            BroadcastListener = Sys.IGC.RegisterBroadcastListener(Channel);
            BroadcastListener.SetMessageCallback("GetBroadcaseMessage");
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

        public UniTransceiver(MBOS sys)
        {
            Sys = sys;
            Listener = Sys.IGC.UnicastListener;
            Listener.SetMessageCallback("GetUniMessage");
        }
        

        public String ReceiveMessage()
        { 
            if (!Listener.HasPendingMessage) return String.Empty;

            MyIGCMessage message = Listener.AcceptMessage();
            String incoming = message.As<String>();

            return incoming;
        }

        public void SendMessage(long target, String data) 
        {
            Sys.IGC.SendUnicastMessage<string>(target, "whisper", data);
        }
    }
}