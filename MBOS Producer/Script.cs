const String NAME = "Producer";
const String VERSION = "1.0.0";
const String DATA_FORMAT = "2";

/*
    Register examples:
        Register ChargedEnergyCell Battery 1 Power Charger #6 GPS:DockAt #6:48071.05:30388.26:23037.28:
        Register EmptyEnergyCell Single 1 Charge Connector #1
*/

public enum UnitType
{
    Single = 1,
    Container = 2,
    Liquid = 3,
    Battery = 4 // same a single but with battery check
}

public class Manager
{
    public class Resource
    {
        public String Unit;
        public UnitType Type;
        public double VolumePerUnit = 1.0;
        public IMyShipConnector Connector;
        public MyWaypointInfo Waypoint = MyWaypointInfo.Empty;
        public int Stock = 0;
        public long RegisteredByManager = 0;
        public int Reservation = 0;
        public MyShipConnectorStatus SingleUnitStockWhen = MyShipConnectorStatus.Connected;
        
        public Resource(String unit, UnitType type, double volumePerUnit, IMyShipConnector connector, MyWaypointInfo waypoint) {
            Unit = unit;
            Type = type;
            VolumePerUnit = volumePerUnit;
            Connector = connector;
            Waypoint = waypoint;
        }

        public Resource(MBOS system, String data) {
            List<String> parts = new List<String>(data.Split('*'));

            Unit = parts[0];
            Type = (UnitType) Enum.Parse(typeof(UnitType), parts[1]);
            VolumePerUnit = double.Parse(parts[2]);
            Connector = system.GetBlock(parts[3]) as IMyShipConnector;
            MBOS.ParseGPS(parts[4], out Waypoint);
            Stock = int.Parse(parts[5]);
            RegisteredByManager = long.Parse(parts[6]);
            Reservation = int.Parse(parts[7]);
        }

        public override String ToString() {
            return Unit.ToString()
                + "*" + Type.ToString()
                + "*" + VolumePerUnit.ToString()
                + "*" + Connector.EntityId.ToString()
                + "*" + Waypoint.ToString()
                + "*" + Stock.ToString()
                + "*" + RegisteredByManager.ToString()
                + "*" + Reservation.ToString()
            ;
        }

        public bool StockHasChanged(MBOS system) {
            int oldStock = Stock;
            UpdateStock(system);

            return oldStock != Stock;
        }

        protected void UpdateStock(MBOS system) {
            switch(Type) {
                case UnitType.Single:
                    Stock = Connector.Status == SingleUnitStockWhen ? 1 : 0;
                    break;
                case UnitType.Battery:
                    if (Connector.Status != MyShipConnectorStatus.Connected) {
                        Stock = 0;
                        break;
                    }
                    float current = 0f;
                    float max = 0f;
                    List<IMyTerminalBlock> batteries = new List<IMyTerminalBlock>();

                    system.GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(
                        batteries,
                        (IMyTerminalBlock block) => block.CubeGrid.EntityId == Connector.OtherConnector.CubeGrid.EntityId
                    );

                    batteries.ForEach(delegate(IMyTerminalBlock block) {
                        IMyBatteryBlock battery = block as IMyBatteryBlock;
                        max += battery.MaxStoredPower;
                        current += battery.CurrentStoredPower;
                    });

                    Stock = (100f / max * current) >= 100f ? 1 : 0;
                    break;
            }
        }
    }

    public List<Resource> Resources = new List<Resource>();
    public MyShipConnectorStatus SingleUnitStockWhen = MyShipConnectorStatus.Connected;
    protected MBOS System;

    public Manager(MBOS system) {
        System = system;
        Load();
    }

    public bool RegisterResource(List<string> parts) {
        String unit = parts[0];
        UnitType type = (UnitType) Enum.Parse(typeof(UnitType), parts[1]);
        double volumePerUnit = double.Parse(parts[2]);
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        parts.RemoveRange(0, 3);

        String nameAndGpsValue = String.Join(" ", parts.ToArray());
        List<String> nameAndGpsParts = new List<String>(
             nameAndGpsValue.Split(new string[] {" GPS:"}, StringSplitOptions.RemoveEmptyEntries)
        );

        IMyShipConnector connector = System.GetBlockByName(nameAndGpsParts[0]) as IMyShipConnector;
        if (connector == null) {
            if (connector == null) {
                return false;
            }
        }
        Vector3D dockAt = connector.CubeGrid.GridIntegerToWorld(connector.Position);
        waypoint = new MyWaypointInfo(unit + " Target", dockAt);

        if (nameAndGpsParts.Count > 1 && MBOS.ParseGPS("GPS:" + nameAndGpsParts[1], out waypoint) == false) {
            return false;
        }

        List<Resource> foundResources = Resources.FindAll(
            (Resource resource) => resource.Unit == unit 
                && resource.Type == type
                && resource.Connector.EntityId == connector.EntityId
                && resource.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        Resource newResource;
        if(foundResources.Count > 0) {
            newResource = foundResources[0];
            newResource.VolumePerUnit = volumePerUnit;
        } else {
            newResource = new Resource(unit, type, volumePerUnit, connector, waypoint);
            Resources.Add(newResource);
        }

        BroadCastResource(newResource);

        return true;
    }

    public void Save() {
        List<String> list = new List<String>();
        Resources.ForEach((Resource resource) => list.Add(resource.ToString()));
        System.Config("Resources").Value = String.Join("|", list.ToArray());
        System.Config("SingleUnitStockWhen").Value = SingleUnitStockWhen.ToString();
    }

    public void ExecuteMessage(String message) {
        List<String> parts = new List<String>(message.Split('|'));
        String command = parts[0];
        parts.RemoveAt(0);

        switch(command) {
            case "ProducerRegistered":
                ProducerRegistered(parts[0], long.Parse(parts[1]));
                break;
            case "ReRegisterProducer":
                Resources.ForEach((Resource resource) => BroadCastResource(resource));
                break;
        }
    }

    public void UpdateStock(bool force = false)
    {
        Resources.ForEach(delegate(Resource resource) {
            if(resource.RegisteredByManager == 0) return;
            if(force == true) SendUpdate(resource);
            if(resource.StockHasChanged(System) == false) return;

            // Apply reservations
            switch(resource.Type) {
                case UnitType.Single:
                    if (resource.Stock == 0 && resource.Reservation == 1) {
                        resource.Reservation = 0;
                    }
                    break;
            }

            SendUpdate(resource);
        });
    }

    protected void Load() {
        String singleStockWhen = System.Config("SingleUnitStockWhen").Value;
        if (singleStockWhen != String.Empty) {
            SingleUnitStockWhen = (MyShipConnectorStatus) Enum.Parse(typeof(MyShipConnectorStatus), singleStockWhen);
        }
        List<String> list = new List<String>(System.Config("Resources").Value.Split('|'));
        list.ForEach(delegate(String line) {
            if(line != String.Empty) {
                Resource newResource = new Resource(System, line);
                newResource.SingleUnitStockWhen = SingleUnitStockWhen;
                Resources.Add(newResource);
                BroadCastResource(newResource);
            }
        });
    }

    protected void SendUpdate(Resource resource) {
        System.Transceiver.SendMessage(
            resource.RegisteredByManager,
            "UpdateResourceStock|" + resource.Unit 
            + "|" + resource.Stock.ToString()
            + "|" + resource.Reservation.ToString()
            + "|" + System.EntityId.ToString()
            + "|" + resource.Waypoint.ToString()
        );
    }

    protected void BroadCastResource(Resource resource) {
        System.BroadCastTransceiver.SendMessage(
            "RegisterProducer|" + resource.Unit 
            + "|" + System.EntityId
            + "|" + resource.Type.ToString()
            + "|" + resource.VolumePerUnit.ToString()
            + "|" + resource.Waypoint.ToString()
        );
    }
    protected void ProducerRegistered(String unit, long managerId) {
        Resources.ForEach(delegate(Resource resource){
            if (resource.Unit != unit) return;
            resource.RegisteredByManager = managerId;
            SendUpdate(resource);
        });
    }
}

MBOS Sys;
Manager FactoryManager;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo);
    Runtime.UpdateFrequency = UpdateFrequency.None;

    InitProgram();
}

public void Save()
{
    FactoryManager.Save();
    
    Sys.SaveConfig();
}

public void InitProgram() 
{
    Sys.LoadConfig();

    FactoryManager = new Manager(Sys);

    Runtime.UpdateFrequency = UpdateFrequency.Update100; //UpdateFrequency.Update100;
    Echo("Program initialized.");
}


public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (FactoryManager == null) {
        InitProgram();
    }

    FactoryManager.UpdateStock();
    Save();
    UpdateInfo();
}


public void UpdateInfo()
{
    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Registered Resources: " + FactoryManager.Resources.Count.ToString() + "\n"
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
        case "Register":
            if(FactoryManager.RegisterResource(parts)) {
                Echo("New resources registered.");
            } else {
                Echo("Registration failed.");
            }
            break;
        case "ForceUpdate":
            FactoryManager.UpdateStock(true);
            break;
        case "SingleUpdateStockWhen":
            FactoryManager.SingleUnitStockWhen = (MyShipConnectorStatus) Enum.Parse(typeof(MyShipConnectorStatus), parts[0]);
            FactoryManager.Resources.ForEach((Manager.Resource resource) => resource.SingleUnitStockWhen = FactoryManager.SingleUnitStockWhen);
            break;
        case "Reset":
            FactoryManager.Resources.Clear();
            break;
        case "ReceiveMessage":
            Echo("Received radio data.");
            String message = string.Empty;
            while((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
                FactoryManager.ExecuteMessage(message);
            }
            while((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
                FactoryManager.ExecuteMessage(message);
            }
            break;
        default:
            Echo(
                "Available Commands: \n"
                + " * Register <Resource Name> {Single|Conatiner|Liquid} <Volume> <Connector> [<GPS>]\n"
                + " * ForceUpdate\n"
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