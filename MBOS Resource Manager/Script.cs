const String NAME = "Resource Manager";
const String VERSION = "1.9.1";
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
        public MyWaypointInfo ConsumerWaypoint = MyWaypointInfo.Empty;
        public MyWaypointInfo ProducerWaypoint = MyWaypointInfo.Empty;
        public MBOS Sys;
        public int Quantity;
        public string DroneType;

        public DeliverMission(MBOS sys, String unit, MyWaypointInfo consumerWaypoint, MyWaypointInfo producerWaypoint, int quantity, string droneType) {
            Sys = sys;
            Id = System.DateTime.Now.ToBinary();
            Unit = unit;
            ConsumerWaypoint = consumerWaypoint;
            ProducerWaypoint = producerWaypoint;
            Quantity = quantity;
            DroneType = droneType;
        }

        public DeliverMission(MBOS sys, String data) {
            Sys = sys;
            List<String> parts = new List<String>(data.Split('*'));

            Id = long.Parse(parts[0]);
            Unit = parts[1];
            MBOS.ParseGPS(parts[2], out ConsumerWaypoint);
            MBOS.ParseGPS(parts[3], out ProducerWaypoint);
            Quantity = int.Parse(parts[4]);
            DroneType = parts[5];
        }

        public override String ToString() {
            return Id.ToString()
                + "*" + Unit
                + "*" + ConsumerWaypoint.ToString()
                + "*" + ProducerWaypoint.ToString()
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
            case "RemoveProducer":
                RemoveProducer(parts);
                break;
            case "RemoveConsumer":
                RemoveConsumer(parts);
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
            output += "  * " + pair.Key + ": " + pair.Value.Stock.ToString() + " (" + pair.Value.Reserved.ToString() + ")\n";
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
                + " (On way: " + pair.Value.InDelivery.ToString() 
                + "; Received: " + pair.Value.Delivered.ToString() 
                + "; Missions: " + pair.Value.Missions.ToString() 
                + ")\n";

            if (pair.Value.InDelivery == 0 && pair.Value.Missions == 0 && pair.Value.Delivered != 0) {
                Consumers.ForEach(
                    delegate(Consumer consumer) {
                        if (consumer.Unit != pair.Key) return;
                        consumer.Delivered = 0;
                        MBOS.Sys.Traffic.Add("Correct delivered count for consumer " + pair.Key +"("+consumer.EntityId+")");
                    }
                );
            }
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
            "ProducerRegistered|" + newProducer.Unit + "|" + Sys.EntityId + "|" + newProducer.Waypoint.ToString()
        );
    }

    protected void RemoveProducer(List<String> parts)
    {
        String unit = parts[0];
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        MBOS.ParseGPS(parts[1], out waypoint);
        List<Producer> foundProducers = Producers.FindAll(
            (Producer item) => item.Unit == unit && item.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        foundProducers.ForEach(
            delegate(Producer producer) {
                Producers.Remove(producer);
            }
        );
    }

    private void RemoveConsumer(List<String> parts) 
    {
        String unit = parts[0];
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        MBOS.ParseGPS(parts[1], out waypoint);
        List<Consumer> foundConsumers = Consumers.FindAll(
            (Consumer item) => item.Unit == unit && item.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        foundConsumers.ForEach(
            delegate(Consumer consumer) {
                Consumers.Remove(consumer);
            }
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
            "ConsumerRegistered|" + newConsumer.Unit + "|" + Sys.EntityId + "|" + newConsumer.Waypoint.ToString()
        );
    }

    protected void RequestResource(List<string> parts) {
        String unit = parts[0];
        int quantity = int.Parse(parts[1]);
        MyWaypointInfo waypoint = MyWaypointInfo.Empty;
        MBOS.ParseGPS(parts[2], out waypoint);
        
        if (quantity > 0) {
            List<DeliverMission> foundMissions = Missions.FindAll(
                (DeliverMission mission) => mission.Unit == unit && mission.ConsumerWaypoint.Coords.Equals(waypoint.Coords, 0.01)
            );
            int inDelivery = 0;
            Consumers.ForEach(
                delegate(Consumer consumer) { 
                    if (consumer.Unit != unit) return;
                    inDelivery -= consumer.Delivered;
                }
            );
            foundMissions.ForEach((DeliverMission mission) => inDelivery += mission.Quantity);
            inDelivery = inDelivery < 0 ? 0 : inDelivery;
            if (inDelivery >= quantity) {
                MBOS.Sys.Traffic.Add("Requested resource " + unit + " already in delivery.");
                return;
            }
        }
        
        List<Consumer> foundConsumers = Consumers.FindAll(
            (Consumer consumerItem) => consumerItem.Unit == unit && consumerItem.Waypoint.Coords.Equals(waypoint.Coords, 0.01)
        );
        if (foundConsumers.Count == 0) {
            MBOS.Sys.Traffic.Add("No consumer of resource " + unit + " with waypoint " + waypoint + " found.");
            return;
        }
        foundConsumers[0].Requested = quantity;
    }

    protected Dictionary<String,long> LastStationByUnit = new Dictionary<String,long>();

    public void SearchRequestingConsumerForMissions() {
        List<Consumer> requestingConsumers = Consumers.FindAll((Consumer consumerItem) => (consumerItem.Requested - consumerItem.Delivered) > 0);

         for(int index = 0; index < requestingConsumers.Count; index ++) {
            Consumer consumer = requestingConsumers[index];
            
            if (LastStationByUnit.ContainsKey(consumer.Unit) && consumer.GridId == LastStationByUnit[consumer.Unit]) continue; //Search for next station/unit

            List<DeliverMission> foundMissions = Missions.FindAll(
                (DeliverMission mission) => mission.Unit == consumer.Unit && mission.ConsumerWaypoint.Coords.Equals(consumer.Waypoint.Coords, 0.01)
            );
            if(foundMissions.Count > 0) {
                continue; // only one drone can fly to point ;)
            }

            if (FindProducerAndCreateMission(consumer)) {
                if(!LastStationByUnit.ContainsKey(consumer.Unit)) {
                    LastStationByUnit.Add(consumer.Unit, 0L);
                }
                LastStationByUnit[consumer.Unit] = consumer.GridId;
                Consumers.Remove(consumer);
                Consumers.Add(consumer);
                return; // only one mission per itteration. Otherwise we got duplicated ids and a drone chaos ;)
            }
        }

        
        LastStationByUnit = new Dictionary<String,long>(); // reset if all stations got missions
    }

    public void CorrectRuntimeData() {
        Producers.ForEach(delegate (Producer producer) {
            if(producer.Stock == 0 && producer.Reserved != 0) {
                producer.Reserved = 0; // Fix wrong reservation count, when delivery not counted [Effect of reload/restart game]
                MBOS.Sys.Traffic.Add("Correct deliver count for producer" + producer.Unit);
            }
        });
    }

    protected bool FindProducerAndCreateMission(Consumer consumer) {
        int neededQuantity = consumer.Requested - consumer.Delivered;
        if (neededQuantity <= 0) return false;
        
        List<Producer> foundProducers = Producers.FindAll(
            (Producer producer) => producer.Unit == consumer.Unit && (producer.Stock - producer.Reserved) > 0
        );

        for(int index = 0; index < foundProducers.Count; index ++) {
            Producer producer = foundProducers[index];
            
            List<DeliverMission> foundMissions = Missions.FindAll(
                (DeliverMission missionItem) => missionItem.ProducerWaypoint.Coords.Equals(producer.Waypoint.Coords, 0.01)
            );
            if(foundMissions.Count > 0) {
                continue; // only one drone can take from point ;)
            }
            
            int stock = producer.Stock - producer.Reserved;
            if (stock == 0) continue;

            int quantity = stock >= neededQuantity ? neededQuantity : stock;
            producer.Reserved += quantity;
            MBOS.Sys.Transceiver.SendMessage(
                producer.EntityId, 
                "OrderResource|" + producer.Unit + "|" + quantity + "|" + producer.Waypoint.ToString()
            );

            DeliverMission mission = new DeliverMission(
                MBOS.Sys, 
                consumer.Unit,
                consumer.Waypoint, 
                producer.Waypoint, 
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

            return true; // only one mission per target/consumer!
        }

        return false;
    }

    protected void ReRegisterAllStations()
    {
        MBOS.Sys.BroadCastTransceiver.SendMessage("ReRegisterProducer");
        MBOS.Sys.BroadCastTransceiver.SendMessage("ReRegisterConsumer");
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
            MBOS.Sys.Traffic.Add("ERROR: Update of resource without existant consumer for " + unit + " at " + waypoint);
            return;
        }
        List<DeliverMission> foundMissions = Missions.FindAll((DeliverMission missionItem) => missionItem.ConsumerWaypoint.Coords.Equals(foundConsumer[0].Waypoint.Coords, 0.01));
        if (foundMissions.Count == 0) {
            MBOS.Sys.Traffic.Add("ERROR: Delivery ignored! No mission found for " + unit + " at " + waypoint);
            return;
        } 
        foundConsumer[0].Delivered += quantity;
        if (foundConsumer[0].Delivered > foundConsumer[0].Requested) {
            MBOS.Sys.Traffic.Add("ERROR: To much delivering detected. Correcting it.");
            foundConsumer[0].Delivered = foundConsumer[0].Requested;
        }
    }

    protected void MissionCompleted(long missionId) {
        List<DeliverMission> foundMissions = Missions.FindAll((DeliverMission missionItem) => missionItem.Id == missionId);
        if(foundMissions.Count == 0) return; // was already removed

        DeliverMission mission = foundMissions[0];
        Missions.Remove(mission);

        List<Consumer> foundConsumers = Consumers.FindAll(
            (Consumer consumerItem) => consumerItem.Unit == mission.Unit && consumerItem.Waypoint.Coords.Equals(mission.ConsumerWaypoint.Coords, 0.01)
        );
        if(foundConsumers.Count == 0) {
            MBOS.Sys.Traffic.Add("ERROR: Completed mission for not found consumer " + mission.Unit + " at " + mission.ConsumerWaypoint);
            return;
        }
        Consumer consumer = foundConsumers[0];
        if(consumer.Delivered < mission.Quantity) {
            // Öhm...Drone lost cargo?
            MBOS.Sys.Traffic.Add("ERROR: Completed mission does not deliver! " + mission.Unit + " at " + mission.ConsumerWaypoint);
            ReRegisterAllStations();
            return;
        }
        consumer.Requested -= mission.Quantity;
        consumer.Requested = consumer.Requested < 0 ? 0 : consumer.Requested;
        consumer.Delivered -= mission.Quantity;
        consumer.Delivered = consumer.Delivered < 0 ? 0 : consumer.Delivered;
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
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo, Runtime);
    Sys.UpdatesBetweenMessages = 3;

    InitProgram();
    UpdateInfo();
    Save();
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

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    Echo("Program initialized.");
    Sys.BroadCastTransceiver.SendMessage("ReRegisterProducer");
    Sys.BroadCastTransceiver.SendMessage("ReRegisterConsumer");
}

public void CheckMessages() {
    String message = string.Empty;
    if((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
        Manager.ExecuteMessage(message);
        return;
    }
    if((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
        Manager.ExecuteMessage(message);
    }
}

public void Main(String argument, UpdateType updateSource)
{
    ReadArgument(argument);

    if (Manager == null) {
        InitProgram();
    }

    CheckMessages();
    Manager.SearchRequestingConsumerForMissions();
    Manager.CorrectRuntimeData();
    Save();
    UpdateInfo();

    List<ResourceManager.Consumer> requestingConsumers = Manager.Consumers.FindAll((ResourceManager.Consumer consumerItem) => (consumerItem.Requested - consumerItem.Delivered) > 0);
    
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
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
        case "SetLCD":
            IMyTextPanel lcd = Sys.GetBlockByName(allArgs) as IMyTextPanel;
            if(lcd != null) {
                Manager.Screen = lcd;
            }
            break;
        case "Reset":
            Manager = null;
            Sys.Config("Producers").Value = String.Empty;
            Sys.Config("Consumers").Value = String.Empty;
            Sys.Config("Missions").Value = String.Empty;
            Sys.Transceiver.Buffer.Input.Clear();
            Sys.Transceiver.Buffer.Output.Clear();
            Sys.BroadCastTransceiver.Buffer.Input.Clear();
            Sys.BroadCastTransceiver.Buffer.Output.Clear();
            Sys.BroadCastTransceiver.SendMessage("ResetOrders");
            break;
        case "ResetMissions":
            Manager.Missions.Clear();
            Manager.Producers.ForEach((ResourceManager.Producer producer) => producer.Reserved = 0);
            Echo("Missions cleared.");
            Sys.BroadCastTransceiver.SendMessage("ResetOrders");
            break;
        case "ResetOrders":
            Sys.BroadCastTransceiver.SendMessage("ResetOrders");
            break;
        default:
            Echo("Available Commands: \n  * SetLCD <Name of Panel>\n  * ResetMissions\n  * ResetOrders");
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
