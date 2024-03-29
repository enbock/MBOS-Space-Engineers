const String NAME = "Consumer";
const String VERSION = "2.0.0";
const String DATA_FORMAT = "1";

/*
Connector CustomData examples:

Consume=Ore/Ice Container 10000
Produce=EmptyContainer Single 1

Consume=EmptyContainer Single 1
Produce=Ore/Iron Container 8000
LimitBy=4000 Ingot/Iron  <-- Do not request Iron Ore if more than 4000 Iron Ingot exisiting.

Consume=Component/SteelPlate Container 1
Produce=EmptyContainer Single 1

Consume=ChargedEnergyCell Battery 1
Produce=EmptyEnergyCell Single 1
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

        public bool StockHasChanged(List<Limit> limits) {
            int oldStock = Stock;
            UpdateStock(limits);

            return oldStock != Stock;
        }

        protected void UpdateStock(List<Limit> limits) {
            switch(Type) {
                case UnitType.Single:
                case UnitType.Battery:
                    Stock = isConnectorInUse ? 1 : 0;
                    break;
                case UnitType.Container:
                    List<IMyCargoContainer> cargo = new List<IMyCargoContainer>();

                    MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(
                        cargo,
                        (IMyCargoContainer block) => block.CubeGrid.EntityId == MBOS.Sys.GridId
                    );

                    Stock = (
                        isConnectorInUse 
                        || CargoAmount(Unit, cargo) >= RequiredStock 
                        || IsLimitReached(Unit, cargo, limits) 
                    ) ? 1 : 0;
                    break;
            }
        }

        protected bool IsLimitReached(String unit, List<IMyCargoContainer> cargo, List<Limit> limits) {
            bool limitReached = true;
            bool limitFound = false;
            limits.ForEach(
                delegate (Limit limit) {
                    if(limit.RequestedUnit != unit) return;
                    limitFound = true;
                    if (limitReached == true && CargoAmount(limit.ExistingUnit, cargo) < limit.MaximumAmount) limitReached = false;
                }
            );
            return limitFound && limitReached;
        }

        protected int CargoAmount(String unit, List<IMyCargoContainer> cargo) {
            int current = 0;
            
            cargo.ForEach(delegate(IMyCargoContainer container) {
                IMyInventory inventory = container.GetInventory();
                current += (int) Math.Floor((float) inventory.GetItemAmount(MyDefinitionId.Parse("MyObjectBuilder_" + unit)));
            });

            return current;
        }
    }

    public class Limit {
        public String RequestedUnit;
        public int MaximumAmount;
        public String ExistingUnit;

        public Limit(String requestedUnit, int maximumAmount, String existingUnit) {
            RequestedUnit = requestedUnit;
            MaximumAmount = maximumAmount;
            ExistingUnit = existingUnit;
        }

        public Limit(String data) {
            List<String> parts = new List<String>(data.Split('*'));

            RequestedUnit = parts[0];
            MaximumAmount = int.Parse(parts[1]);
            ExistingUnit = parts[2];
        }
        
        public override String ToString() {
            return RequestedUnit.ToString()
                + "*" + MaximumAmount.ToString()
                + "*" + ExistingUnit.ToString()
            ;
        }
    }

    public List<Resource> Resources = new List<Resource>();
    public List<Limit> Limits = new List<Limit>();
    protected MBOS System;

    public Manager(MBOS system) {
        System = system;
        ScanConsumers();
    }

    public void RegisterResource(List<string> parts, IMyShipConnector connector) {
        String unit = parts[0];
        UnitType type = (UnitType) Enum.Parse(typeof(UnitType), parts[1]);
        int requiredStock = int.Parse(parts[2]);

        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
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
        newResource.StockHasChanged(Limits);
        BroadCastResource(newResource);
    }

    public void Save() {
        List<String> list = new List<String>();
        Resources.ForEach((Resource resource) => list.Add(resource.ToString()));
        System.Config("Resources").Value = String.Join("|", list.ToArray());
    }

    public void RemoveAllConsumers()
    {
        List<Resource> res = Resources;
        Resources = new List<Resource>();
        res.ForEach((Resource r) => TransmitResourceRemoval(r));
    }

    protected void TransmitResourceRemoval(Resource resource) {
        System.BroadCastTransceiver.SendMessage(
            "RemoveConsumer|" + resource.Unit 
            + "|" + resource.Waypoint.ToString()
        );
        resource.RegisteredByManager = 0L;
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
            if(resource.StockHasChanged(Limits) == false) return;

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

    public void AddLimit(List<String> parts) {
        String requestedUnit = parts[0];
        int maxAmount = int.Parse(parts[1]);
        String exisitingUnit = parts[2];

        Limit existingLimit = Limits.Find((Limit limit) => limit.RequestedUnit == requestedUnit && limit.ExistingUnit == exisitingUnit);
        if (existingLimit == null) {
            Limit newLimit = new Limit(requestedUnit, maxAmount, exisitingUnit);
            Limits.Add(newLimit);
        } else {
            existingLimit.MaximumAmount = maxAmount;
        }
    }

    protected void UpdateStock(Resource resource) {
        int neededQuantity = 1 - resource.Stock;
        MBOS.Sys.Transceiver.SendMessage(
            resource.RegisteredByManager,
            "RequestResource|" + resource.Unit + "|" + neededQuantity.ToString() + "|" + resource.Waypoint.ToString()
        );
    }

    public void ScanConsumers() {
        // Deregister known consumers
        List<string> list = new List<String>(System.Config("Resources").Value.Split('|'));
        list.ForEach(delegate(String line) {
            if(line != String.Empty) {
                Resource res = new Resource(System, line);
                TransmitResourceRemoval(res);
            }
        });

        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        System.GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(
            connectors, 
            (IMyShipConnector c) => c.IsWorking && c.CubeGrid.EntityId == System.GridId && c.CustomData.IndexOf("Consume=") != -1
        );

        connectors.ForEach(
            delegate (IMyShipConnector c) {
                List<MBOS.ConfigValue> configList = new List<MBOS.ConfigValue>();
                MBOS.Sys.LoadConfig(c.CustomData, configList, true);

                System.Echo("Scan "+c.CustomName+"...");
                
                // Consume=Ore/Iron Container 8000
                String line = MBOS.Sys.Config("Consume", configList).Value.Trim();
                if (line == String.Empty) return;

                List<String> parts = new List<String>(line.Split(' '));
                if (parts.Count != 3) return;

                // LimitBy=4000 Ingot/Iron
                String limitLine = MBOS.Sys.Config("LimitBy", configList).Value.Trim();
                List<String> limits = new List<String>(limitLine.Split(','));
                limits.ForEach(
                    delegate (String limit) {
                        if (limit.Trim() == String.Empty) return;

                        List<String> limitParts = new List<String>(limit.Trim().Split(' '));
                        if(limitParts.Count != 2 || Limits.FindAll((Limit l) => l.RequestedUnit == parts[0] && l.ExistingUnit == limitParts[1]).Count > 0)  return;

                        Limit newLimit = new Limit(parts[0], int.Parse(limitParts[0]), limitParts[1]);
                        Limits.Add(newLimit);
                    }
                );
                
                RegisterResource(parts, c);
            }
        );
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
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo, Runtime);

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

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    Echo("Program initialized.");
}

public void CheckMessages() {
    String message = string.Empty;
    if((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
        ConsumerManager.ExecuteMessage(message);
    }
    if((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
        ConsumerManager.ExecuteMessage(message);
    }
}

public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (ConsumerManager == null) {
        InitProgram();
    }

    CheckMessages();
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
            if(1 - resource.Stock <= 0) return;
            if(neededResources.ContainsKey(resource.Unit) == false) {
                neededResources.Add(resource.Unit, 0);
            }
            neededResources[resource.Unit] += 1 - resource.Stock;
        }
    );
    foreach(KeyValuePair<string, int> pair in neededResources) {
        string limitOutput = "";
        List<String> limits = new List<String>();
        ConsumerManager.Limits.ForEach(delegate (Manager.Limit limit) { if (limit.RequestedUnit == pair.Key) limits.Add(limit.ExistingUnit); });
        if (limits.Count > 0) limitOutput = "(Limited by: " + String.Join(", ", limits.ToArray()) + ")";
        neededResourceOutput += "    * " + pair.Key + ": " + pair.Value.ToString() + " " + limitOutput + "\n";
    }

    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Registered Resource Units: " + ConsumerManager.Resources.Count.ToString() + "\n"
        + neededResourceOutput
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
        case "ScanConsumers":
            ConsumerManager.ScanConsumers();
            Echo("Scan complete.");
            break;
        case "Reset":
            ConsumerManager.Resources.Clear();
            break;
        case "Limit":
            ConsumerManager.AddLimit(parts);
            Echo("Limit registered or updated.");
            break;
        case "ReceiveMessage":
            Echo("Received radio data.");
            break;
        case "RemoveAllConsumers":
            ConsumerManager.RemoveAllConsumers();
            Echo("Consumers removed.");
            break;
        default:
            Echo(
                "Available Commands: \n"
                + " * ScanConsumers\n"
                + " * Limit <Requested Resource> <Max Amount> <Resource in Container>\n"
                + " * Reset"
                + " * RemoveAllConsumers"
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