const String NAME = "Resource Manager";
const String VERSION = "1.3.0";
const String DATA_FORMAT = "1";

public enum UnitType
{
    Single = 1,
    Container = 2,
    Liquid = 3,
    Battery = 4
}

public class ResourceManager {
    public class Producer {
        public long EntityId;
        public long GridId;
        public String Unit = String.Empty;
        public UnitType Type = UnitType.Container;
        public double VolumePerUnit = 0.0;
        public MyWaypointInfo Waypoint = MyWaypointInfo.Empty;
        public int Stock = 0;
        public int Reserved = 0;

        public Producer(long entityId, long gridId, String unit, UnitType type, double volumePerUnit, MyWaypointInfo waypoint) {
            EntityId = entityId;
            GridId = gridId;
            Unit = unit;
            Type = type;
            VolumePerUnit = volumePerUnit;
            Waypoint = waypoint;
        }

        public Producer(String data) {
            List<String> parts = new List<String>(data.Split('*'));

            EntityId = long.Parse(parts[0]);
            GridId = long.Parse(parts[1]);
            Unit = parts[2];
            Type = (UnitType) Enum.Parse(typeof(UnitType), parts[3]);
            VolumePerUnit = double.Parse(parts[4]);
            MBOS.ParseGPS(parts[5], out Waypoint);
            Stock = int.Parse(parts[6]);
            Reserved = int.Parse(parts[7]);
        }

        public override String ToString() {
            return EntityId.ToString()
                + "*" + GridId.ToString()
                + "*" + Unit.ToString()
                + "*" + Type.ToString()
                + "*" + VolumePerUnit.ToString()
                + "*" + Waypoint.ToString()
                + "*" + Stock.ToString()
                + "*" + Reserved.ToString()
            ;
        }
    }

    public class Consumer {
        public long EntityId;
        public long GridId;
        public String Unit = String.Empty;
        public MyWaypointInfo Waypoint = MyWaypointInfo.Empty;
        public int Requested = 0; // set by RequestResource ; decrease by MissionCompleted
        public int Delivered = 0; // increase by ResourceDelivered ; decrease by MissionCompleted
        
        public Consumer(long entityId, long gridId, String unit, MyWaypointInfo waypoint) {
            EntityId = entityId;
            GridId = gridId;
            Unit = unit;
            Waypoint = waypoint;
        }

        public Consumer(String data) {
            List<String> parts = new List<String>(data.Split('*'));

            EntityId = long.Parse(parts[0]);
            GridId = long.Parse(parts[1]);
            Unit = parts[2];
            MBOS.ParseGPS(parts[3], out Waypoint);
            Requested = int.Parse(parts[4]);
            Delivered = int.Parse(parts[5]);
        }

        public override String ToString() {
            return EntityId.ToString()
                + "*" + GridId.ToString()
                + "*" + Unit.ToString()
                + "*" + Waypoint.ToString()
                + "*" + Requested.ToString()
                + "*" + Delivered.ToString()
            ;
        }
    }

    public class DeliverMission {
        public long Id;
        public String Unit = String.Empty;
        public MyWaypointInfo Waypoint = MyWaypointInfo.Empty;
        public MBOS Sys;
        public int Quantity;
        public string DroneType;

        public DeliverMission(MBOS sys, String unit, MyWaypointInfo waypoint, int quantity, string droneType) {
            Sys = sys;
            Id = System.DateTime.Now.ToBinary();
            Unit = unit;
            Waypoint = waypoint;
            Quantity = quantity;
            DroneType = droneType;
        }

        public DeliverMission(MBOS sys, String data) {
            Sys = sys;
            List<String> parts = new List<String>(data.Split('*'));

            Id = long.Parse(parts[0]);
            Unit = parts[1];
            MBOS.ParseGPS(parts[2], out Waypoint);
            Quantity = int.Parse(parts[3]);
            DroneType = parts[4];
        }

        public override String ToString() {
            return Id.ToString()
                + "*" + Unit
                + "*" + Waypoint.ToString()
                + "*" + Quantity.ToString()
                + "*" + DroneType
            ;
        }
    }

    public List<Producer> Producers = new List<Producer>();
    public List<Consumer> Consumers = new List<Consumer>();
    public List<DeliverMission> Missions = new List<DeliverMission>();
    public IMyTextPanel Screen;
    MBOS Sys;

    public ResourceManager(MBOS sys) {
        Sys = sys;
        Load();
    }

    public void Load() {
        List<String> list = new List<String>(Sys.Config("Producers").Value.Split('|'));
        list.ForEach(delegate(String line) {
            if(line != String.Empty) Producers.Add(new Producer(line));
        });
        
        list = new List<String>(Sys.Config("Consumers").Value.Split('|'));
        list.ForEach(delegate(String line) { 
            if(line != String.Empty) Consumers.Add(new Consumer(line));
        });

        list = new List<String>(Sys.Config("Missions").Value.Split('|'));
        list.ForEach(delegate(String line) { 
            if(line != String.Empty) Missions.Add(new DeliverMission(Sys, line));
        });

        Screen = Sys.Config("Screen").Block as IMyTextPanel;
    }

    public void Save() {
        List<String> list = new List<String>();
        Producers.ForEach((Producer producer) => list.Add(producer.ToString()));
        Sys.Config("Producers").Value = String.Join("|", list.ToArray());

        list.Clear();
        Consumers.ForEach((Consumer consumer) => list.Add(consumer.ToString()));
        Sys.Config("Consumers").Value = String.Join("|", list.ToArray());

        list.Clear();
        Missions.ForEach((DeliverMission mission) => list.Add(mission.ToString()));
        Sys.Config("Missions").Value = String.Join("|", list.ToArray());

        Sys.Config("Screen").Block = Screen;
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
            case "UpdateResourceStock":
                UpdateResourceStock(parts);
                break;
            case "RequestResource":
                RequestResource(parts);
                break;
            case "ResourceDelivered":
                ResourceDelivered(parts);
                break;
            case "MissionCompleted":
                MissionCompleted(long.Parse(parts[0]));
                break;
        }
    }

    public class ResourceInfo {
        public int Stock = 0;
        public int Reserved = 0;
        public int Requested = 0;
        public int InDelivery = 0;
        public int Delivered = 0;
        public int Missions = 0;
    }

    public void UpdateScreen() {
        if(Screen == null) {
            return;
        }

        String output = "";
        output += "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "] [" 
            + NAME + " v" + VERSION + "]\nStock Resources:\n------------------------------\n"
        ;

        Dictionary<String, ResourceInfo> info = new Dictionary<String, ResourceInfo>();

        Producers.ForEach(delegate(Producer producer) {
            if (info.ContainsKey(producer.Unit) == false) {
                info.Add(producer.Unit, new ResourceInfo());
            }
            ResourceInfo item = info[producer.Unit];
            item.Stock += producer.Stock;
            item.Reserved += producer.Reserved;

        });

        foreach(KeyValuePair<String, ResourceInfo> pair in info) {
            output += "  * " + pair.Key + ": " + pair.Value.Stock.ToString() + "(" + pair.Value.Reserved.ToString() + ")\n";
        }

        output += "\nRequired Resources:\n------------------------------\n";
        info = new Dictionary<String, ResourceInfo>();
        Consumers.ForEach(delegate(Consumer consumer) {
            if (info.ContainsKey(consumer.Unit) == false) {
                info.Add(consumer.Unit, new ResourceInfo());
            }
            ResourceInfo item = info[consumer.Unit];
            item.Requested += consumer.Requested;
            item.Delivered += consumer.Delivered;
        });

        Missions.ForEach(
            delegate(DeliverMission mission) {
                if (info.ContainsKey(mission.Unit) == false) {
                    info.Add(mission.Unit, new ResourceInfo());
                }
                ResourceInfo item = info[mission.Unit];
                item.Missions ++;
                item.InDelivery += mission.Quantity;
            }
        );

        foreach(KeyValuePair<String, ResourceInfo> pair in info) {
            output += "  * " + pair.Key + ": " + pair.Value.Requested.ToString() 
                + "(On way: " + pair.Value.InDelivery.ToString() 
                + "; Received: " + pair.Value.Delivered.ToString() 
                + "; Missions: " + pair.Value.Missions.ToString() 
                + ")\n";
        }

        Screen.WriteText(output, false);
    }

    protected void RegisterProducer(List<String> parts) {
        long entityId = long.Parse(parts[1]);
        long gridId = long.Parse(parts[2]);
        String unit = parts[0];
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        MBOS.ParseGPS(parts[5], out waypoint);
    
        List<Producer> foundProducers = Producers.FindAll(
            (Producer item) => item.EntityId == entityId && item.Unit == unit && item.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        Producer newProducer;
        if (foundProducers.Count == 0) {
            newProducer = new Producer(
                entityId,
                gridId,
                unit,
                (UnitType) Enum.Parse(typeof(UnitType), parts[3]),
                double.Parse(parts[4]),
                waypoint
            );
            Producers.Add(newProducer);
        } else {
            newProducer = foundProducers[0];
        }

        Sys.Transceiver.SendMessage(
            newProducer.EntityId, 
            "ProducerRegistered|" + newProducer.Unit + "|" + Sys.EntityId
        );
    }

    protected void UpdateResourceStock(List<String> parts) {
        long entityId = long.Parse(parts[3]);
        String unit = parts[0];
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        MBOS.ParseGPS(parts[4], out waypoint);

        Producers.FindAll(
            (Producer item) => item.EntityId == entityId && item.Unit == unit && item.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        ).ForEach(delegate(Producer producer){
            producer.Stock = int.Parse(parts[1]);
            producer.Reserved = int.Parse(parts[2]);
        });
    }

    protected void RegisterConsumer(List<String> parts) {
        long entityId = long.Parse(parts[1]);
        long gridId = long.Parse(parts[2]);
        String unit = parts[0];
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        MBOS.ParseGPS(parts[3], out waypoint);
        List<Consumer> foundConsumers = Consumers.FindAll(
            (Consumer item) => item.EntityId == entityId && item.Unit == unit && item.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        Consumer newConsumer;
        if (foundConsumers.Count == 0) {
            newConsumer = new Consumer(entityId, gridId, unit, waypoint);
            Consumers.Add(newConsumer);
        } else {
            newConsumer = foundConsumers[0];
        }

        Sys.Transceiver.SendMessage(
            newConsumer.EntityId, 
            "ConsumerRegistered|" + newConsumer.Unit + "|" + Sys.EntityId
        );
    }

    protected void RequestResource(List<string> parts) {
        String unit = parts[0];
        int quantity = int.Parse(parts[1]);
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        MBOS.ParseGPS(parts[2], out waypoint);
        
        List<DeliverMission> foundMissions = Missions.FindAll(
            (DeliverMission mission) => mission.Unit == unit && mission.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        int inDelivery = 0;
        foundMissions.ForEach((DeliverMission mission) => inDelivery += mission.Quantity);
        if (inDelivery >= quantity) {
            MBOS.Sys.Echo("Requested resource " + unit + " already in delivery.");
            return;
        }
        
        List<Consumer> foundConsumers = Consumers.FindAll(
            (Consumer consumerItem) => consumerItem.Unit == unit && consumerItem.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        if (foundConsumers.Count == 0) {
            MBOS.Sys.Echo("No consumer of resource " + unit + " with waypoint " + waypoint + " found.");
            return;
        }
        foundConsumers[0].Requested = quantity;
    }

    public void SearchRequestingConsumerForMissions() {
        List<Consumer> requestingConsumers = Consumers.FindAll((Consumer consumerItem) => (consumerItem.Requested - consumerItem.Delivered) > 0);
        requestingConsumers.ForEach(
            delegate(Consumer consumer) {
                List<DeliverMission> foundMissions = Missions.FindAll(
                    (DeliverMission mission) => mission.Unit == consumer.Unit && mission.Waypoint.Coords.Equals(consumer.Waypoint.Coords, 0.01)
                );
                if(foundMissions.Count > 0) {
                    return; // only one drone can fly to point ;)
                }

                FindProducerAndCreateMission(consumer);
            }
        );
    }

    protected void FindProducerAndCreateMission(Consumer consumer) {
        int neededQuantity = consumer.Requested - consumer.Delivered;
        if (neededQuantity <= 0) return;
        
        List<Producer> foundProducers = Producers.FindAll(
            (Producer producer) => producer.Unit == consumer.Unit && (producer.Stock - producer.Reserved) > 0
        );

        for(int index = 0; index < foundProducers.Count && neededQuantity > 0; index ++) {
            Producer producer = foundProducers[index];
            int stock = producer.Stock - producer.Reserved;
            if (stock == 0) continue;

            int quantity = stock >= neededQuantity ? neededQuantity : stock;
            producer.Reserved += quantity;
            MBOS.Sys.Transceiver.SendMessage(producer.EntityId, "OrderResource|" + producer.Unit + "|" + quantity);

            // TODO: For error checks, we can implement here waiting for "confirm order" message

            DeliverMission mission = new DeliverMission(
                MBOS.Sys, 
                consumer.Unit,
                consumer.Waypoint, 
                quantity, 
                MapUnitTypeToDroneType(producer.Type)
            );
            Missions.Add(mission);

            MBOS.Sys.BroadCastTransceiver.SendMessage(
                "RequestFlight|" + mission.Id.ToString()
                    + "|" + mission.DroneType
                    + "|" + producer.Waypoint.ToString()
                    + "|" + producer.GridId.ToString()
                    + "|" + consumer.Waypoint.ToString()
                    + "|" + consumer.GridId.ToString()
            );

            return; // only one mission per target/consumer!
        }
    }

    protected void ResourceDelivered(List<string> parts) {
        String unit = parts[0];
        int quantity = int.Parse(parts[1]);
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        MBOS.ParseGPS(parts[2], out waypoint);
        
        List<Consumer> foundConsumer = Consumers.FindAll(
            (Consumer consumerItem) => consumerItem.Unit == unit && consumerItem.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        if(foundConsumer.Count == 0) {
            MBOS.Sys.Echo("ERROR: Update of resource without existant consumer for " + unit + " at " + waypoint);
            return;
        }
        foundConsumer[0].Delivered += quantity;
    }

    protected void MissionCompleted(long missionId) {
        List<DeliverMission> foundMissions = Missions.FindAll((DeliverMission missionItem) => missionItem.Id == missionId);
        if(foundMissions.Count == 0) return; // was already removed

        DeliverMission mission = foundMissions[0];
        Missions.Remove(mission);

        List<Consumer> foundConsumers = Consumers.FindAll(
            (Consumer consumerItem) => consumerItem.Unit == mission.Unit && consumerItem.Waypoint.Coords.Equals(mission.Waypoint.Coords, 0.01)
        );
        if(foundConsumers.Count == 0) {
            MBOS.Sys.Echo("ERROR: Completed mission for not found consumer " + mission.Unit + " at " + mission.Waypoint);
            return;
        }
        Consumer consumer = foundConsumers[0];
        if(consumer.Delivered < mission.Quantity) {
            // Öhm...Drone lost cargo?
            MBOS.Sys.Echo("ERROR: Completed mission does not deliver! Redo mission: " + mission.Unit + " at " + mission.Waypoint);
            return;
        }
        consumer.Requested -= mission.Quantity;
        consumer.Delivered -= mission.Quantity;
    }

    protected String MapUnitTypeToDroneType(UnitType unitType) {
        switch(unitType) {
            case UnitType.Single:
                return "transport";
            case UnitType.Battery:
                return "transport";
            default:
                return "transport";
        }
    }
}

MBOS Sys;
ResourceManager Manager;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo);
    Runtime.UpdateFrequency = UpdateFrequency.None;

    InitProgram();
    UpdateInfo();
    Save();

    Sys.BroadCastTransceiver.SendMessage("ReRegisterProducer");
    Sys.BroadCastTransceiver.SendMessage("ReRegisterConsumer");
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

    Manager.SearchRequestingConsumerForMissions();
    Save();
    UpdateInfo();
}

public void UpdateInfo()
{
    Manager.UpdateScreen();

    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Registered Producers: " + Manager.Producers.Count.ToString() + "\n"
        + "Registered Consumers: " + Manager.Consumers.Count.ToString() + "\n"
        + "Delivery Missions: " + Manager.Missions.Count.ToString() + "\n"
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
            Echo("Received radio data.");
            String message = string.Empty;
            while((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
                Manager.ExecuteMessage(message);
            }
            while((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
                Manager.ExecuteMessage(message);
            }
            break;
        case "SetLCD":
            IMyTextPanel lcd = Sys.GetBlockByName(allArgs) as IMyTextPanel;
            if(lcd != null) {
                Manager.Screen = lcd;
            }
            break;
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
