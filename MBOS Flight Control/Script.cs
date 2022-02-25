const String NAME = "Flight Control";
const String VERSION = "2.1.0";
const String DATA_FORMAT = "1";

/*
    Examples from X-World:
        RequestFlight|-8585778962141942972|transport|GPS:EmptyEnergyCell Connected:18213.223080337317:140078.30541871215:-105136.17496217653:|141800612156212441|GPS:EmptyEnergyCell Target:17998.674241177887:141498.49835116041:-105890.46044299923:|92440036194299365
*/

public class FlightControl
{
    public class FlightTime {
        public double Time;
        public Station Target;

        public FlightTime(Station target, double time)
        {
            Target = target;
            Time = time;
        }

        public override String ToString()
        {
            return Target.EntityId.ToString() + "~" + Time.ToString();
        }
    }

    public class Station {
        public long EntityId;
        public long GridId;
        public MyWaypointInfo FlightIn = MyWaypointInfo.Empty;
        public List<FlightTime> Weights = new List<FlightTime>();

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
            List<String> weights = new List<String>();
            Weights.ForEach((FlightTime t) => weights.Add(t.ToString()));

            return EntityId.ToString()
                + "*" + GridId.ToString()
                + "*" + FlightIn.ToString()
                + "*" + String.Join("§", weights.ToArray())
            ;
        }

        public FlightTime GetWeightForStation(Station target)
        {
            FlightTime weight = Weights.Find((FlightTime f) => f.Target == target);
            if (weight == null) {
                weight = new FlightTime(target, Vector3D.Distance(FlightIn.Coords, target.FlightIn.Coords) / 50.0); // Flightspeed 50m/s (must higher than real speed!)
                Weights.Add(weight);
            }
            return weight;
        }
    }

    public class Hangar : Station {
        public Hangar(long entityId, long gridId, MyWaypointInfo flightIn) : base (entityId, gridId, flightIn) {}
        public Hangar(string data) : base (data) {}
    }

    MBOS Sys;
    public List<Hangar> Hangars = new List<Hangar>();
    public List<Station> Stations = new List<Station>();

    public FlightControl(MBOS sys) {
        Sys = sys;

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
        Sys.BroadCastTransceiver.SendMessage("RequestRenewStationRegister|"+Sys.EntityId);
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
            case "RequestFlight":
                RequestFlight(parts);
                break;
            case "FlightTime":
                UpdateFlightTime(parts[0], parts[1], double.Parse(parts[2]));
                break;
        }
    }

    private class LoadedDataForStation {
        public Station Station;
        public String Data;
        public LoadedDataForStation(Station station, String data)
        {
            Station = station;
            Data = data;
        }
    }

    protected void Load() {
        List<String> list;

        list = new List<String>(Sys.Config("Hangars").Value.Split('|'));
        list.ForEach(delegate(String line) {
            if(line == String.Empty) return;
            Hangar hangar = new Hangar(line);
            Hangars.Add(hangar);
            AddStation(hangar);
        });

        list = new List<String>(Sys.Config("Stations").Value.Split('|'));
        List<LoadedDataForStation> flightTimes = new List<LoadedDataForStation>();
        list.ForEach(delegate(String line) {
            if(line == String.Empty) return;
            Station loadedStation = new Station(line);
            loadedStation = AddStation(loadedStation); // Posibible pointer change to station object!

            List<String> parts = new List<String>(line.Split('*'));
            if(parts.Count > 2) flightTimes.Add(new LoadedDataForStation(loadedStation, parts[0]));
        });

        flightTimes.ForEach(delegate(LoadedDataForStation line) {
            LoadFlightTimes(line.Station, line.Data);
        });
    }

    public void LoadFlightTimes(Station station, String data) 
    {
        List<String> weights = new List<String>(data.Split('§'));

        weights.ForEach(
            delegate (String weightData) {
                List<String> parts = new List<String>(weightData.Split('~'));
                if (parts.Count < 2) return;

                long entityId = long.Parse(parts[0]);
                double time = double.Parse(parts[1]);

                Station found = Stations.Find((Station s) => s.EntityId == entityId);
                if (found == null) return;
                station.GetWeightForStation(found).Time = time;
            }
        );
    }

    protected void RegisterStation(long entityId, long gridId, string flightInGps) {
        MyWaypointInfo flightIn = MyWaypointInfo.Empty;
        MBOS.ParseGPS(flightInGps, out flightIn);
        Station newStation;
        List<Station> foundStations = Stations.FindAll((Station station) => station.EntityId == entityId);
        if (foundStations.Count > 0) {
            newStation = foundStations[0];
            newStation.FlightIn = flightIn;
            newStation.EntityId = entityId;
            UpdateWeights(newStation);
        } else {
            newStation = new Station(entityId, gridId, flightIn);
            AddStation(newStation);
        }
    }

    private Station AddStation(Station newStation)
    {
        Station station = Stations.Find((Station s) => s.EntityId == newStation.EntityId);
        if (station != null) return station; 

        Stations.Add(newStation);
        UpdateWeights(newStation);

        return newStation;
    }

    private void UpdateWeights(Station station)
    {
        Stations.ForEach((Station i) => {
            station.GetWeightForStation(i);
            i.GetWeightForStation(station);
        });
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
            UpdateWeights(newHangar);
        } else {
            newHangar = new Hangar(entityId, gridId, flightIn);
            Hangars.Add(newHangar);
        }
        AddStation(newHangar);
    }

    protected void RequestFlight(List<string> parts)
    {
        string missionId = parts[0];
        string type = parts[1];
        long startGrid = long.Parse(parts[3]);
        long targetGrid = long.Parse(parts[5]);

        // TODO Add multiple hangar handling: List<Hangar> foundHangars = Hangars.FindAll((Hangar hangar) => hangar.GridId == gridId);
        List<Hangar> foundHangars = Hangars;
        if(foundHangars.Count == 0) return;
        Hangar hangar = foundHangars[0];

        MyWaypointInfo startWaypoint = MyWaypointInfo.Empty;
        if(MBOS.ParseGPS(parts[2], out startWaypoint) == false) return;
        MyWaypointInfo targetWaypoint = MyWaypointInfo.Empty;
        if(MBOS.ParseGPS(parts[4], out targetWaypoint) == false) return;

        List<Station> foundStations;
        Station startStation;
        Station targetStation;
        foundStations = Stations.FindAll((Station stationItem) => stationItem.GridId == startGrid);
        if(foundStations.Count == 0) {
            MBOS.Sys.Traffic.Add("ERROR: Start station not found: " + startGrid);
            return;
        }
        startStation = foundStations[0];

        foundStations = Stations.FindAll((Station stationItem) => stationItem.GridId == targetGrid);
        if(foundStations.Count == 0) {
            MBOS.Sys.Traffic.Add("ERROR: Target station not found: " + targetGrid);
            return;
        } 
        targetStation = foundStations[0];

        String flightPath = hangar.FlightIn.ToString();

        flightPath += FindFlightPath(hangar, startStation);
        flightPath += startStation.FlightIn.ToString() + ">" + startWaypoint.ToString() + "<" + startStation.FlightIn.ToString();
        
        flightPath += FindFlightPath(startStation, targetStation);
        flightPath += targetStation.FlightIn.ToString() + ">" + targetWaypoint.ToString() + "<" + targetStation.FlightIn.ToString();

        flightPath += FindFlightPath(targetStation, hangar);

        Sys.Transceiver.SendMessage(hangar.EntityId, "RequestTransport|" + missionId + "|" + type + "|" + flightPath);
    }

    private void UpdateFlightTime(String from, String to, double time)
    {
        if (time <= 0 || from == to) return;

        Station fromStation = Stations.Find((Station s) => s.FlightIn.ToString() == from);
        Station toStation = Stations.Find((Station s) => s.FlightIn.ToString() == to);
        if (fromStation == null || toStation == null) return;

        fromStation.GetWeightForStation(toStation).Time = time;
    }

    private List<Station> closedlist = new List<Station>();

    private String FindFlightPath(Station start, Station destination)
    {
        if (start == destination) return String.Empty;

        List<Station>path = new List<Station>();
        Station current = FindNext(start, destination);
        closedlist.Clear();
        while(current != destination) {
            path.Add(current);
            current = FindNext(current, destination);
        }
        String flightPath = String.Empty;
        path.ForEach((Station s) => flightPath += s.FlightIn.ToString());

        return flightPath;
    }

    private class DistanceNode {
        public double Weight;
        public Station Target;
    }

    private Station FindNext(Station current, Station destination) {
        closedlist.Add(current);
        List<DistanceNode> distances = new List<DistanceNode>();
        Stations.ForEach(
            delegate (Station s) {
                if (closedlist.Contains(s)) return;
                DistanceNode n = new DistanceNode();
                n.Target = s;
                double a = current.GetWeightForStation(s).Time;
                double b = s.GetWeightForStation(destination).Time;
                n.Weight = a + b;
                distances.Add(n);
            }
        );

        if (distances.Count == 0) return destination;

        distances.Sort(
            delegate (DistanceNode a, DistanceNode b) {
                if (a.Weight == b.Weight) return 0;
                return a.Weight < b.Weight ? -1 : 1;
            }
        );
        return distances[0].Target;
    }
}

MBOS Sys;
FlightControl FlightController;

public Program()
{
    Sys = new MBOS(Me, GridTerminalSystem, IGC, Echo, Runtime);
    Runtime.UpdateFrequency = UpdateFrequency.Update100;

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
    
    FlightController = new FlightControl(Sys);
    FlightController.SendReady();

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
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

    CheckMessages();
    Save();
    UpdateInfo();
}

public void CheckMessages() 
{
    String message = string.Empty;
    if((message = Sys.BroadCastTransceiver.ReceiveMessage()) != string.Empty) {
        FlightController.ExecuteMessage(message);
    }
    if((message = Sys.Transceiver.ReceiveMessage()) != string.Empty) {
        FlightController.ExecuteMessage(message);
    }
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
            if (allArgs != string.Empty) FlightController.ExecuteMessage(allArgs);
            break;
        case "Reset":
            if(FlightController == null) break;
            FlightController.Hangars.Clear();
            FlightController.Stations.Clear();
            FlightController.SendReady();
            break;
        default:
            Echo(
                "Available Commands: \n"
                +"  * ReceiveMessage <Simulated Network Message>\n"
                +"  * Reset\n"
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
