const String NAME = "Consumer";
const String VERSION = "1.2.4";
const String DATA_FORMAT = "1";

/*
    Register examples:
        Register EmptyEnergyCell Single 1 Power Charger #1
        Register ChargedEnergyCell Battery 1 Charge Connector #1
        Register Ore/Iron Container 8000 IronOreDeliverConnector#1
        Register EmptyContainer Single 1 IronOreSupplyConnector#1
*/

public enum UnitType
{
    Single = 1,
    Container = 2,
    Liquid = 3,
    Battery = 4
}

public class Manager
{
    public class Resource
    {
        public String Unit;
        public UnitType Type;
        public int RequiredStock;
        public IMyShipConnector Connector;
        public MyWaypointInfo Waypoint = MyWaypointInfo.Empty;
        public int Stock = 0;
        public long RegisteredByManager = 0;

        public bool isConnectorInUse { 
            get { 
                return (Connector.IsWorking == true && (Connector.Status == MyShipConnectorStatus.Connected || Connector.Status == MyShipConnectorStatus.Connectable));
            }
        }
        
        public Resource(String unit, UnitType type, int requiredStock, IMyShipConnector connector) {
            Unit = unit;
            Type = type;
            Connector = connector;
            RequiredStock = requiredStock;

            Vector3D dockAt = Connector.GetPosition();
            Waypoint = new MyWaypointInfo(Unit + " Target", dockAt);
        }

        public Resource(MBOS system, String data) {
            List<String> parts = new List<String>(data.Split('*'));

            Unit = parts[0];
            Type = (UnitType) Enum.Parse(typeof(UnitType), parts[1]);
            RequiredStock = int.Parse(parts[2]);
            Connector = system.GetBlock(parts[3]) as IMyShipConnector;
            
            Vector3D dockAt = Connector.GetPosition();
            Waypoint = new MyWaypointInfo(Unit + " Target", dockAt);
            
            Stock = int.Parse(parts[5]);
            RegisteredByManager = long.Parse(parts[6]);
        }

        public override String ToString() {
            return Unit.ToString()
                + "*" + Type.ToString()
                + "*" + RequiredStock.ToString()
                + "*" + Connector.EntityId.ToString()
                + "*" + Waypoint.ToString()
                + "*" + Stock.ToString()
                + "*" + RegisteredByManager.ToString()
            ;
        }

        public bool StockHasChanged() {
            int oldStock = Stock;
            UpdateStock();

            return oldStock != Stock;
        }

        protected void UpdateStock() {
            switch(Type) {
                case UnitType.Single:
                case UnitType.Battery:
                    Stock = isConnectorInUse ? 1 : 0;
                    break;
                case UnitType.Container:
                    float current = 0f;
                    List<IMyTerminalBlock> cargo = new List<IMyTerminalBlock>();

                    MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(
                        cargo,
                        (IMyTerminalBlock block) => block.CubeGrid.EntityId == MBOS.Sys.GridId
                    );

                    cargo.ForEach(delegate(IMyTerminalBlock block) {
                        IMyCargoContainer container = block as IMyCargoContainer;
                        IMyInventory inventory = container.GetInventory();
                        current += (float)inventory.GetItemAmount(MyDefinitionId.Parse("MyObjectBuilder_" + Unit));
                    });

                    Stock = isConnectorInUse || (int) Math.Floor(current) >= RequiredStock ? 1 : 0;
                    break;
            }
        }
    }

    public List<Resource> Resources = new List<Resource>();
    protected MBOS System;

    public Manager(MBOS system) {
        System = system;
        Load();
    }

    public bool RegisterResource(List<string> parts) {
        String unit = parts[0];
        UnitType type = (UnitType) Enum.Parse(typeof(UnitType), parts[1]);
        int requiredStock = int.Parse(parts[2]);
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        parts.RemoveRange(0, 3);

        String name = String.Join(" ", parts.ToArray());

        IMyShipConnector connector = System.GetBlockByName(name) as IMyShipConnector;
        if (connector == null) {
            if (connector == null) {
                return false;
            }
        }
        Vector3D dockAt = connector.GetPosition();
        waypoint = new MyWaypointInfo(unit + " Target", dockAt);

        List<Resource> foundResources = Resources.FindAll(
            (Resource resource) => resource.Unit == unit 
                && resource.Type == type
                && resource.Connector.EntityId == connector.EntityId
                && resource.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        Resource newResource;
        if(foundResources.Count > 0) {
            newResource = foundResources[0];
            newResource.RequiredStock = requiredStock;
        } else {
            newResource = new Resource(unit, type, requiredStock, connector);
            Resources.Add(newResource);
        }

        BroadCastResource(newResource);

        return true;
    }

    public void Save() {
        List<String> list = new List<String>();
        Resources.ForEach((Resource resource) => list.Add(resource.ToString()));
        System.Config("Resources").Value = String.Join("|", list.ToArray());
    }

    public void ExecuteMessage(String message) {
        List<String> parts = new List<String>(message.Split('|'));
        String command = parts[0];
        parts.RemoveAt(0);

        switch(command) {
            case "ConsumerRegistered":
                string unit = parts[0];
                long managerId = long.Parse(parts[1]);
                parts.RemoveRange(0, 2);
                MyWaypointInfo waypoint = MyWaypointInfo.Empty;
                if (MBOS.ParseGPS(String.Join(" ", parts.ToArray()), out waypoint) == false) {
                    MBOS.Sys.Traffic.Add("ERROR: Registered message: Waypoint wrong.");
                    break;
                } 
                ConsumerRegistered(unit, managerId, waypoint);
                break;
            case "ReRegisterConsumer":
                Resources.ForEach((Resource resource) => BroadCastResource(resource));
                break;
        }
    }

    public void UpdateStock()
    {
        Resources.ForEach(delegate(Resource resource) {
            if(resource.RegisteredByManager == 0) return;
            int oldStock = resource.Stock;
            if(resource.StockHasChanged() == false) return;

            int deliveredStock = resource.Stock - oldStock;
            if(deliveredStock > 0) {
                MBOS.Sys.Transceiver.SendMessage(
                    resource.RegisteredByManager,
                    "ResourceDelivered|" + resource.Unit + "|" + deliveredStock.ToString() + "|" + resource.Waypoint.ToString()
                );
            }

            UpdateStock(resource);
        });
    }

    protected void UpdateStock(Resource resource) {
        int neededQuantity = 1 - resource.Stock;
        MBOS.Sys.Transceiver.SendMessage(
            resource.RegisteredByManager,
            "RequestResource|" + resource.Unit + "|" + neededQuantity.ToString() + "|" + resource.Waypoint.ToString()
        );
    }

    protected void Load() {
        List<String> list = new List<String>(System.Config("Resources").Value.Split('|'));
        list.ForEach(delegate(String line) {
            if(line != String.Empty) {
                Resource newResource = new Resource(System, line);
                Resources.Add(newResource);
                BroadCastResource(newResource);
            }
        });
    }

    protected void BroadCastResource(Resource resource) {
        System.BroadCastTransceiver.SendMessage(
            "RegisterConsumer|" + resource.Unit 
            + "|" + System.EntityId
            + "|" + System.GridId
            + "|" + resource.Waypoint.ToString()
        );
    }

    protected void ConsumerRegistered(String unit, long managerId, MyWaypointInfo waypoint) {
        Resources.ForEach(delegate(Resource resource){
            if (resource.Unit != unit || resource.Waypoint.Coords.Equals(waypoint.Coords, 0.01) == false) return;
            resource.RegisteredByManager = managerId;
            UpdateStock(resource);
        });
    }
}

MBOS Sys;
Manager ConsumerManager;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo);
    Runtime.UpdateFrequency = UpdateFrequency.None;

    InitProgram();
}

public void Save()
{
    ConsumerManager.Save();
    
    Sys.SaveConfig();
}

public void InitProgram() 
{
    Sys.LoadConfig();

    ConsumerManager = new Manager(Sys);

    Runtime.UpdateFrequency = UpdateFrequency.Update100; //UpdateFrequency.Update100;
    Echo("Program initialized.");
}


public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (ConsumerManager == null) {
        InitProgram();
    }

    ConsumerManager.UpdateStock();
    Save();
    UpdateInfo();
}


public void UpdateInfo()
{
    String neededResourceOutput = "Needed Resources:\n";
    Dictionary<string, int> neededResources = new Dictionary<string, int>();
    ConsumerManager.Resources.ForEach(
        delegate(Manager.Resource resource) {
            if(resource.Stock >= resource.RequiredStock) return;
            if(neededResources.ContainsKey(resource.Unit) == false) {
                neededResources.Add(resource.Unit, 0);
            }
            neededResources[resource.Unit] += 1 - resource.Stock;
        }
    );
    foreach(KeyValuePair<string, int> pair in neededResources) {
        neededResourceOutput += "    * " + pair.Key + ": " + pair.Value.ToString() + "\n";
    }

    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Registered Resource Units: " + ConsumerManager.Resources.Count.ToString() + "\n"
        + neededResourceOutput
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
        case "Register":
            if(ConsumerManager.RegisterResource(parts)) {
                Echo("New resources registered.");
            } else {
                Echo("Registration failed.");
            }
            break;
        case "Reset":
            ConsumerManager.Resources.Clear();
            break;
        case "ReceiveMessage":
            Echo("Received radio data.");
            String message = string.Empty;
            while((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
                ConsumerManager.ExecuteMessage(message);
            }
            while((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
                ConsumerManager.ExecuteMessage(message);
            }
            break;
        default:
            Echo(
                "Available Commands: \n"
                + " * Register <Resource Name> {Single|Conatiner|Liquid} <Required Amount> <Connector> [<GPS>]\n"
                + " * SingleUpdateStockWhen {Connected|Connectable|Unconnected}"
            );
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
    public List<String> Traffic = new List<String>();

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

    public static bool ParseGPS(String gpsData, out MyWaypointInfo gps) {
        return MyWaypointInfo.TryParse(gpsData, out gps);
    }
}
