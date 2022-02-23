const String NAME = "Producer";
const String VERSION = "2.1.1";
const String DATA_FORMAT = "2";

/*
    Register examples:
        Register ChargedEnergyCell Battery 1 Power Charger #1
        Register EmptyEnergyCell Single 1 Charge Connector #1
        Register Ore/Iron Container 8000 IronOreSupplyConnector#1
        Register EmptyContainer Single 1 IronOreDeliverConnector#1
*/

public enum UnitType
{
    Single = 1,
    Container = 2,
    Liquid = 3,
    Battery = 4 // same a single but with battery filled check
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
        public MyWaypointInfo ConnectedWaypoint = MyWaypointInfo.Empty;
        public int Stock = 0;
        public long RegisteredByManager = 0;
        public int Reservation = 0;
        
        public Resource(String unit, UnitType type, double volumePerUnit, IMyShipConnector connector, MyWaypointInfo waypoint) {
            Unit = unit;
            Type = type;
            VolumePerUnit = volumePerUnit;
            Connector = connector;
            Waypoint = waypoint;
            ConnectedWaypoint = waypoint;
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
            if (parts.Count >= 9) {
                MBOS.ParseGPS(parts[8], out ConnectedWaypoint);
            } else {
                ConnectedWaypoint = Waypoint;
            }
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
                + "*" + ConnectedWaypoint.ToString()
            ;
        }

        public bool StockHasChanged() {
            int oldStock = Stock;
            UpdateStock();

            return oldStock != Stock;
        }

        public void LoadCargoToContainer() {
            if(Type != UnitType.Container || Connector.Status != MyShipConnectorStatus.Connected) return;
            
            List<IMyTerminalBlock> stationCargo = new List<IMyTerminalBlock>();
            MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(
                stationCargo,
                (IMyTerminalBlock block) => block.CubeGrid.EntityId ==  MBOS.Sys.GridId
            );

            List<IMyTerminalBlock> containerCargo = new List<IMyTerminalBlock>();
            MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(
                containerCargo,
                (IMyTerminalBlock block) => block.CubeGrid.EntityId == Connector.OtherConnector.CubeGrid.EntityId
            );

            int neededQuantity = (int) Math.Floor(VolumePerUnit);
            MyItemType requestedItem = MyDefinitionId.Parse("MyObjectBuilder_" + Unit);
            
            containerCargo.ForEach(delegate(IMyTerminalBlock block) {
                IMyCargoContainer container = block as IMyCargoContainer;
                IMyInventory inventory = container.GetInventory();
                neededQuantity -= (int) Math.Floor((float) inventory.GetItemAmount(requestedItem));
            });

            if(neededQuantity <= 0) return;
            
            stationCargo.ForEach(delegate(IMyTerminalBlock block) {
                if(neededQuantity <= 0) return;
                IMyCargoContainer container = block as IMyCargoContainer;
                IMyInventory inventory = container.GetInventory();
                int hasQuantity = (int) Math.Floor((float) inventory.GetItemAmount(requestedItem));
                if(hasQuantity > neededQuantity) hasQuantity = neededQuantity;
                MyInventoryItem? item = inventory.FindItem(requestedItem);
                if (item != null) {
                    int itemAmount = (int) ((MyInventoryItem) item).Amount;
                    if (itemAmount > hasQuantity) itemAmount = hasQuantity;
                    containerCargo.ForEach(delegate(IMyTerminalBlock targetBlock) {
                        IMyCargoContainer targetContainer = targetBlock as IMyCargoContainer;
                        IMyInventory targetInventory = targetContainer.GetInventory();
                        if (inventory.TransferItemTo(targetInventory, (MyInventoryItem) item, itemAmount)) {
                            hasQuantity -= itemAmount;
                        }
                    });
                }
            });

            return;
        }

        protected void UpdateStock() {
            float current = 0f;
            float max = 0f;

            List<IMyTerminalBlock> cargo = new List<IMyTerminalBlock>();
            if (Connector.Status == MyShipConnectorStatus.Connected) {
                MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(
                    cargo,
                    (IMyTerminalBlock block) => block.CubeGrid.EntityId == Connector.OtherConnector.CubeGrid.EntityId
                );
            }

            switch(Type) {
                case UnitType.Single:
                    if (cargo.Count > 0) {
                        cargo.ForEach(delegate(IMyTerminalBlock block) {
                            IMyCargoContainer container = block as IMyCargoContainer;
                            IMyInventory inventory = container.GetInventory();
                            current += (float) inventory.CurrentVolume;
                        });
                        Stock = current > 0 ? 0 : 1;
                    } else {
                        Stock = 0;
                        if (Connector.IsWorking && Connector.Status == MyShipConnectorStatus.Connectable) {
                            Stock = Connector.OtherConnector.CustomName.ToLower().IndexOf("empty") > -1 ? 1 : 0;
                        }
                    }
                    break;
                case UnitType.Battery:
                    if (Connector.IsWorking == false || Connector.Status != MyShipConnectorStatus.Connected) {
                        Stock = 0;
                        break;
                    }
                    List<IMyTerminalBlock> batteries = new List<IMyTerminalBlock>();

                    MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(
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
                case UnitType.Container:
                    cargo.ForEach(delegate(IMyTerminalBlock block) {
                        IMyCargoContainer container = block as IMyCargoContainer;
                        IMyInventory inventory = container.GetInventory();
                        current += (float) inventory.GetItemAmount(MyDefinitionId.Parse("MyObjectBuilder_" + Unit));
                    });

                    Stock = (int) Math.Floor(current) >= (int) Math.Floor(VolumePerUnit) ? 1 : 0;
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
        Vector3D dockAt = connector.CubeGrid.GetPosition();
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
    }

    public void ExecuteMessage(String message) {
        List<String> parts = new List<String>(message.Split('|'));
        String command = parts[0];
        parts.RemoveAt(0);
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;

        switch(command) {
            case "ProducerRegistered":
                string unit = parts[0];
                long managerId = long.Parse(parts[1]);
                parts.RemoveRange(0, 2);
                if (MBOS.ParseGPS(String.Join(" ", parts.ToArray()), out waypoint) == false) {
                    MBOS.Sys.Traffic.Add("ERROR: Registered message: Waypoint wrong.");
                    break;
                } 
                ProducerRegistered(unit, managerId, waypoint);
                break;
            case "ReRegisterProducer":
                ReRegisterProducer();
                break;
            case "OrderResource":
                if(MBOS.ParseGPS(parts[2], out waypoint) == false) {
                    MBOS.Sys.Echo("ERRROR: OrderResource delivers wrong waypoint: " + parts[2]);
                }
                OrderResource(parts[0], int.Parse(parts[1]), waypoint);
                break;
            case "ResetOrders":
                Resources.ForEach((Resource resource) => resource.Reservation = 0);
                ReRegisterProducer();
                break;
        }
    }

    public void UpdateStock(bool force = false)
    {
        Resources.ForEach(delegate(Resource resource) {
            UpgradeResourceWaypoint(resource);

            if(resource.Stock < resource.Reservation) resource.Reservation = resource.Stock;
            if(resource.StockHasChanged() == false && force == false) return;

            // Apply reservations
            switch(resource.Type) {
                case UnitType.Single:
                case UnitType.Battery:
                    if (resource.Stock == 0 && resource.Reservation > 0) {
                        resource.Reservation = 0;
                    }
                    break;
            }

            SendUpdate(resource);
        });
    }

    public void LoadCargoToContainer() {
        Resources.ForEach(delegate(Resource resource) {
            resource.LoadCargoToContainer();
        });
    }

    protected void ReRegisterProducer() {
        Resources.ForEach(
            delegate(Resource resource) {
                resource.RegisteredByManager = 0L;
                BroadCastResource(resource);
            }
        );
    }

    protected bool UpgradeResourceWaypoint(Resource resource) {
        if (
            resource.Connector.Status == MyShipConnectorStatus.Connected 
        ) {
            List<IMyShipMergeBlock> otherFreeConnectors = new List<IMyShipMergeBlock>();
            MBOS.Sys.GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(
                otherFreeConnectors, 
                (IMyShipMergeBlock connectorItem) => 
                    connectorItem.CubeGrid.EntityId == resource.Connector.OtherConnector.CubeGrid.EntityId
                    && connectorItem.IsConnected == false
            );
            if (resource.RegisteredByManager != 0L && resource.Waypoint.Equals(resource.ConnectedWaypoint)) {
                TransmitResourceRemoval(resource);
            }
            if (otherFreeConnectors.Count == 0) {
                return false;
            }
            MyWaypointInfo newWaypoint = new MyWaypointInfo(
                resource.Unit + " Connected", 
                otherFreeConnectors[0].GetPosition()
            );
            if (newWaypoint.Equals(resource.ConnectedWaypoint) == false) {
                if (resource.RegisteredByManager != 0L) {
                    TransmitResourceRemoval(resource);
                }
                resource.ConnectedWaypoint = newWaypoint;
                BroadCastResource(resource);

                return true;
            }
        }
        return false;
    }

    protected void Load() {
        List<String> list = new List<String>(System.Config("Resources").Value.Split('|'));
        list.ForEach(delegate(String line) {
            if(line != String.Empty) {
                Resource newResource = new Resource(System, line);
                Resources.Add(newResource);
                SendUpdate(newResource);
            }
        });
    }

    protected void SendUpdate(Resource resource) {
        if(UpgradeResourceWaypoint(resource)) {
            MBOS.Sys.Traffic.Add("SendUpdate blocked. Upgrade happened. (" + resource.Stock + ")" + resource.ConnectedWaypoint);
            return;
        }
        if(resource.Waypoint.Equals(resource.ConnectedWaypoint)) {
            MBOS.Sys.Traffic.Add("SendUpdate blocked. WP should not equal. (" + resource.Stock + ")" + resource.ConnectedWaypoint);
            return;
        }
        if(resource.RegisteredByManager == 0L) {
            MBOS.Sys.Traffic.Add("SendUpdate blocked. Wait for register approval. (" + resource.Stock + ")" + resource.ConnectedWaypoint);
            BroadCastResource(resource);
            return;
        }

        System.Transceiver.SendMessage(
            resource.RegisteredByManager,
            "UpdateResourceStock|" + resource.Unit 
            + "|" + resource.Stock.ToString()
            + "|" + resource.Reservation.ToString()
            + "|" + System.EntityId.ToString()
            + "|" + resource.ConnectedWaypoint.ToString()
        );
    }

    protected void BroadCastResource(Resource resource) {
        if(UpgradeResourceWaypoint(resource)) return;
        if(resource.Waypoint.Equals(resource.ConnectedWaypoint)) {
            MBOS.Sys.Traffic.Add("BroadCastResource blocked." + resource.ConnectedWaypoint);
            return;
        }

        System.BroadCastTransceiver.SendMessage(
            "RegisterProducer|" + resource.Unit 
            + "|" + System.EntityId
            + "|" + System.GridId
            + "|" + resource.Type.ToString()
            + "|" + resource.VolumePerUnit.ToString()
            + "|" + resource.ConnectedWaypoint.ToString()
        );
    }

    protected void TransmitResourceRemoval(Resource resource) {
        System.BroadCastTransceiver.SendMessage(
            "RemoveProducer|" + resource.Unit 
            + "|" + resource.ConnectedWaypoint.ToString()
        );
        resource.RegisteredByManager = 0L;
    }

    protected void ProducerRegistered(String unit, long managerId, MyWaypointInfo waypoint) {
        Resources.ForEach(delegate(Resource resource){
            if (resource.Unit != unit || resource.ConnectedWaypoint.Coords.Equals(waypoint.Coords, 0.01) == false) return;
            resource.RegisteredByManager = managerId;
            SendUpdate(resource);
        });
    }
    
    protected void OrderResource(string unit, int quantity, MyWaypointInfo waypoint) {
        List<Resource> foundResources = Resources.FindAll(
            (Resource resourceItem) => resourceItem.Unit == unit && (
                resourceItem.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
                || resourceItem.ConnectedWaypoint.Coords.Equals(waypoint.Coords, 0.01)
            )
        );

        if(foundResources.Count == 0) {
            MBOS.Sys.Echo("ERROR: Requested order item not found for " + unit + " at " + waypoint.ToString());
            return;
        }

        foundResources[0].Reservation += quantity;
    }
}

MBOS Sys;
Manager ProducerManager;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo);

    InitProgram();
}

public void Save()
{
    ProducerManager.Save();
    
    Sys.SaveConfig();
}

public void InitProgram() 
{
    Sys.LoadConfig();

    ProducerManager = new Manager(Sys);

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    Echo("Program initialized.");
}

public void CheckMessages() 
{
    String message = string.Empty;
    if((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
        ProducerManager.ExecuteMessage(message);
        return;
    }
    if((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
        ProducerManager.ExecuteMessage(message);
    }
}

public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (ProducerManager == null) {
        InitProgram();
    }

    CheckMessages();
    ProducerManager.LoadCargoToContainer();
    ProducerManager.UpdateStock();
    Save();
    UpdateInfo();
}


public void UpdateInfo()
{
    String stockResourceOutput = "Stock:\n";
    Dictionary<string, int> stockResources = new Dictionary<string, int>();
    Dictionary<string, int> stockReservations = new Dictionary<string, int>();
    ProducerManager.Resources.ForEach(
        delegate(Manager.Resource resource) {
            if(stockResources.ContainsKey(resource.Unit) == false) {
                stockResources.Add(resource.Unit, 0);
                stockReservations.Add(resource.Unit, 0);
            }
            stockResources[resource.Unit] += resource.Stock;
            stockReservations[resource.Unit] += resource.Reservation;
        }
    );
    foreach(KeyValuePair<string, int> pair in stockResources) {
        stockResourceOutput += "    * " + pair.Key + ": " + pair.Value.ToString() + "(" + stockReservations[pair.Key].ToString() + ")\n";
    }

    String output = "[MBOS] [" + System.DateTime.Now.ToLongTimeString() + "]\n" 
        + "\n"
        + "[" + NAME + " v" + VERSION + "]\n"
        + "\n"
        + "Registered Resources: " + ProducerManager.Resources.Count.ToString() + "\n"
        + stockResourceOutput
        + "UniCast: " + Sys.Transceiver.Buffer.Input.Count.ToString() + " | "+ Sys.Transceiver.Buffer.Output.Count.ToString() +"\n"
        + "BoradCast: " + Sys.BroadCastTransceiver.Buffer.Input.Count.ToString() + " | "+ Sys.BroadCastTransceiver.Buffer.Output.Count.ToString() +"\n"
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
            if(ProducerManager.RegisterResource(parts)) {
                Echo("New resources registered.");
            } else {
                Echo("Registration failed.");
            }
            break;
        case "ClearReservations":
            ProducerManager.Resources.ForEach((Manager.Resource resource) => resource.Reservation = 0);
            ProducerManager.UpdateStock(true);
            Echo("Reservations cleared.");
            break;
        case "ReceiveMessage":
            Echo("Received radio data.");
            break;
        default:
            Echo(
                "Available Commands: \n"
                + " * Register <Resource Name> {Single|Conatiner|Liquid} <Volume> <Connector> [<GPS>]\n"
                + " * ClearReservations"
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
        Transceiver.Buffer = new UniTransceiver.NetBuffer(Config("UniTransceiver").Value);
        BroadCastTransceiver.Buffer = new WorldTransceiver.NetBuffer(Config("WorldTransceiver").Value);
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
            String message = string.Empty;
            while((message = DownloadMessage()) != string.Empty) {
                Buffer.Input.Add(message);
            }
            UploadMessage();

            if (Buffer.Input.Count == 0) return String.Empty;
            
            message = Buffer.Input[0];
            Buffer.Input.RemoveAt(0);
            Traffic.Add("[B+]< " + message);

            return message;
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

        public UniTransceiver(MBOS sys, List<String> traffic)
        {
            Sys = sys;
            Traffic = traffic;
            Listener = Sys.IGC.UnicastListener;
            Listener.SetMessageCallback("ReceiveMessage");
        }
        

        public String ReceiveMessage()
        { 
            String message = string.Empty;
            while((message = DownloadMessage()) != string.Empty) {
                Buffer.Input.Add(message);
            }
            UploadMessage();

            if (Buffer.Input.Count == 0) return String.Empty;
            
            message = Buffer.Input[0];
            Buffer.Input.RemoveAt(0);
            Traffic.Add("[U+]< " + message);

            return message;
        }

        private String DownloadMessage()
        {
            if (!Listener.HasPendingMessage) return String.Empty;

            MyIGCMessage message = Listener.AcceptMessage();
            String incoming = message.As<String>();

            Traffic.Add("[U_]< " + incoming);

            return incoming;
        }

        public void SendMessage(long target, String data) 
        {
            Buffer.Output.Add(new SendMessageInfo(target, data));
            Traffic.Add("[U+]> " + data);
        }

        private void UploadMessage() 
        {
            if (Buffer.Output.Count == 0) return;
            SendMessageInfo info = Buffer.Output[0];
            Buffer.Output.RemoveAt(0);

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
