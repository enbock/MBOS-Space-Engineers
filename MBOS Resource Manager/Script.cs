const String NAME = "Resource Manager";
const String VERSION = "1.0.0";
const String DATA_FORMAT = "1.0";

public enum UnitType
{
    Single = 1,
    Container = 2,
    Liquid = 4
}

public class ResourceManager {
    public class Producer {
        public long EntityId;
        public String Unit = String.Empty;
        public UnitType Type = UnitType.Container;
        public double VolumePerUnit = 0.0;
        public MyWaypointInfo WayPoint = MyWaypointInfo.Empty;
        public int Stock = 0;
        public int Reserved = 0;

        public Producer(long entityId, String unit, UnitType type, double volumePerUnit, MyWaypointInfo wayPoint) {
            EntityId = entityId;
            Unit = unit;
            Type = type;
            VolumePerUnit = volumePerUnit;
            WayPoint = wayPoint;
        }

        public Producer(String data) {
            List<String> parts = new List<String>(data.Split('*'));

            EntityId = long.Parse(parts[0]);
            Unit = parts[1];
            Type = (UnitType) Enum.Parse(typeof(UnitType), parts[2]);
            VolumePerUnit = double.Parse(parts[3]);
            MBOS.ParseGPS(parts[4], out WayPoint);
            Stock = int.Parse(parts[5]);
            Reserved = int.Parse(parts[6]);
        }

        public override String ToString() {
            return EntityId.ToString()
                + "*" + Unit.ToString()
                + "*" + Type.ToString()
                + "*" + VolumePerUnit.ToString()
                + "*" + WayPoint.ToString()
                + "*" + Stock.ToString()
                + "*" + Reserved.ToString()
            ;
        }
    }

    public class Consumer {
        public long EntityId;
        public String Unit = String.Empty;
        public MyWaypointInfo WayPoint = MyWaypointInfo.Empty;
        
        public Consumer(long entityId, String unit, MyWaypointInfo wayPoint) {
            EntityId = entityId;
            Unit = unit;
            WayPoint = wayPoint;
        }

        public Consumer(String data) {
            List<String> parts = new List<String>(data.Split('*'));

            EntityId = long.Parse(parts[0]);
            Unit = parts[1];
            MBOS.ParseGPS(parts[2], out WayPoint);
        }

        public override String ToString() {
            return EntityId.ToString()
                + "*" + Unit.ToString()
                + "*" + WayPoint.ToString()
            ;
        }
    }

    public List<Producer> Producers = new List<Producer>();
    public List<Consumer> Consumers = new List<Consumer>();
    MBOS System;

    public ResourceManager(MBOS system) {
        System = system;
        Load();
    }

    public void Load() {
        List<String> list = new List<String>(System.Config("Producers").Value.Split('|'));
        list.ForEach(delegate(String line) {
            if(line != String.Empty) Producers.Add(new Producer(line));
        });
        
        list = new List<String>(System.Config("Consumer").Value.Split('|'));
        list.ForEach(delegate(String line) { 
            if(line != String.Empty) Consumers.Add(new Consumer(line));
        });
    }

    public void Save() {
        List<String> list = new List<String>();
        Producers.ForEach((Producer producer) => list.Add(producer.ToString()));
        System.Config("Producers").Value = String.Join("|", list.ToArray());

        list.Clear();
        Consumers.ForEach((Consumer consumer) => list.Add(consumer.ToString()));
        System.Config("Consumer").Value = String.Join("|", list.ToArray());
    }

    public void ExecuteMessage(String message) {
        List<String> parts = new List<String>(message.Split('|'));
        String command = parts[0];
        parts.RemoveAt(0);

        switch(command) {
            case "RegisterProducer":
                RegisterProducer(parts);
                break;
            case "RegisterConsumer":
                RegisterConsumer(parts);
                break;
        }
    }

    protected void RegisterProducer(List<String> parts) {
        long entityId = long.Parse(parts[1]);
        String unit = parts[0];
        List<Producer> foundProducers = Producers.FindAll((Producer item) => item.EntityId == entityId && item.Unit == unit);
        Producer newProducer;
        if (foundProducers.Count == 0) {
            MyWaypointInfo wayPoint = MyWaypointInfo.Empty;
            MBOS.ParseGPS(parts[4], out wayPoint);

            newProducer = new Producer(
                entityId,
                unit,
                (UnitType) Enum.Parse(typeof(UnitType), parts[2]),
                double.Parse(parts[3]),
                wayPoint
            );
        } else {
            newProducer = foundProducers[0];
        }

        System.Transceiver.SendMessage(
            newProducer.EntityId, 
            "ProducerRegistered|" + newProducer.Unit + "|" + System.EntityId
        );
    }

    protected void RegisterConsumer(List<String> parts) {
        long entityId = long.Parse(parts[1]);
        String unit = parts[0];
        List<Consumer> foundConsumers = Consumers.FindAll((Consumer item) => item.EntityId == entityId && item.Unit == unit);
        Consumer newConsumer;
        if (foundConsumers.Count == 0) {
            MyWaypointInfo wayPoint = MyWaypointInfo.Empty;
            MBOS.ParseGPS(parts[2], out wayPoint);

            newConsumer = new Consumer(entityId, unit, wayPoint);
        } else {
            newConsumer = foundConsumers[0];
        }

        System.Transceiver.SendMessage(
            newConsumer.EntityId, 
            "ConsumerRegistered|" + newConsumer.Unit + "|" + System.EntityId
        );
    }
}

MBOS Sys;
ResourceManager Manager;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo);
    Runtime.UpdateFrequency = UpdateFrequency.None;

    InitProgram();
}

public void Save()
{
    Manager.Save();
    
    Sys.SaveConfig();
}

public void InitProgram() 
{
    Sys.LoadConfig();

    Manager = new ResourceManager(Sys);

    Runtime.UpdateFrequency = UpdateFrequency.None; //UpdateFrequency.Update100;
    Echo("Program initialized.");
}


public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (Manager == null) {
        InitProgram();
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
        + "Registered Producers: " + Manager.Producers.Count.ToString() + "\n"
        + "Registered Consumers: " + Manager.Consumers.Count.ToString() + "\n"
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
        case "ReceiveMessage":
            Echo("Received radio data.");
            String message = string.Empty;
            while((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
                Manager.ExecuteMessage(message);
            }
            while((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
                Manager.ExecuteMessage(message);
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
