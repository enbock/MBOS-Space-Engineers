const String NAME = "Transceiver";
const String VERSION = "1.3.1";
const String DATA_FORMAT = "1.1";

public class Module {
    public IMyTerminalBlock Block;
    public IMyTextSurface Display;
    public int Count = 0;
    
    public Module(IMyTerminalBlock block) {
        Block = block;
    }

    public override String ToString() 
    { 
        return Block.EntityId.ToString();
    } 
}

public class Call {
    public Module Bus;
    public String Argument;
    
    public Call(Module bus, String argument) {
        Bus = bus;
        Argument = argument;
    }

    public String GetId() 
    { 
        return Bus.ToString();
    }
}

Module Core = null;
Module Bus  = null;
//Module Antenna = null;
IMyBroadcastListener BroadcastListener = null;
string LastSendData = "";
string LastRepeatedData = "";
long Timestamp;
string Channel = "default";
IMyTextSurface ComputerDisplay;

List<String> Traffic = new List<String>();
List<Call> CallStack = new List<Call>();

public void Save()
{   
    Me.CustomData = "FORMAT v" + DATA_FORMAT + "\n"
        + "MODULE=Transmitter\n"
        + (
            Core != null ? (
                "Core=" + Core
            ) : ""
        ) + "\n"
        + (
            Bus != null ? (
                "Bus=" + Bus
            ) : ""
        ) + "\n"
        //+ (Antenna != null ? "Antenna=" + Antenna : "") + "\n"
        + "Channel=" + Channel + "\n"
        + "\n"
        + String.Join("\n", Traffic.ToArray()) + "\n"
    ;
}

public Program()
{
    ComputerDisplay = Me.GetSurface(0);
    ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
    ComputerDisplay.ClearImagesFromSelection();
    ComputerDisplay.ChangeInterval = 0;

    if (Me.CustomData.Length == 0)  {
        Main("");
        return;
    }
    String[] store = Me.CustomData.Split('\n');
    foreach(String line in store) {
        String[] args = line.Split('=');
        if (line.IndexOf("FORMAT") == 0) {
            if(line != "FORMAT v" + DATA_FORMAT)  {
                Main("");
                return;
            }
        }
        if (line.IndexOf("Bus=") == 0) {
            LoadBusFromConfig(line);
        }
        /*if (line.IndexOf("Antenna=") == 0) {
            SetAntenna(args[1]);
        }*/
        if (line.IndexOf("Channel=") == 0) {
            Channel = args[1];
        }
    }
    SetChannel(Channel);
    Main("");
}

public void LoadBusFromConfig(String config)
{
    Bus = null;

    String[] args = config.Split('=');
    if (args.Length != 2) return;
    String[] blocks = (args[1]).Split('*');
    if (blocks.Length != 3) return;

    IMyProgrammableBlock bus = GetBlock(blocks[0]) as IMyProgrammableBlock;
    IMyProgrammableBlock core = GetBlock(blocks[1]) as IMyProgrammableBlock;

    if (bus != null) Bus = new Module(bus);
    if (core != null) Core = new Module(core);
}

public void Main(String argument)
{
    Timestamp = System.DateTime.Now.ToBinary();
    Runtime.UpdateFrequency = UpdateFrequency.None;

    if (argument == "UNINSTALL") {
        Uninstall();
    } else {
        ReadArgument(argument);
        if (Core == null) {
            RegisterOnFirstCore();
        }
    }
    
    Save();
    ExecuteCalls();
    Save();

    if(Traffic.Count > 40) {
        Traffic.RemoveRange(0, Traffic.Count - 40);
    }
    string output = "[MBOS]"
        + " [" + System.DateTime.Now.ToLongTimeString() + "]" 
        + " [" + (Core != null ? Core.Count.ToString() : "0") + "]" 
        + "\n\n"
        + "[" + NAME + " v" + VERSION + "]\n" 
        + "Registered on core: " + (
            Core != null 
            ? (
                Core.Block.CustomName
                + "\n   Stack:" + CallStack.Count
            ) 
            : "none"
        ) + "\n"
        + "Registered on bus: " + (Bus != null ? Bus.Block.CustomName : "none") + "\n"
        //+ "Connected on antenna: " + (Antenna != null ? Antenna.Block.CustomName : "none") + "\n"
        + "Traffic (Channel: " + Channel + "):\n"
        + DebugTraffic()
    ;
    ComputerDisplay.WriteText(output);

    /*if (Antenna == null) {
        Echo("Please, type 'SetAntenna <name of the antenna>' in argument field and press run.");
    }*/
}

public void ReadArgument(String args) 
{
    if (args == String.Empty) return;
    
    if (args.IndexOf("API://") == 0) {
        ApplyAPICommunication(args);
        return;
    }
     
    List<String> parts = new List<String>(args.Split(' ')); 
    String command = parts[0].Trim();
    parts.RemoveAt(0);
    switch (command) {
        /*case "SetAntenna":
            SetAntenna(String.Join(" ", parts.ToArray()));
            Echo("New antenna connected.");
            break;*/
        case "SetChannel":
            SetChannel(parts[0]);
            Echo("Channel changed.");
            break;
        case "SendMessage":
            SendMessage(String.Join(" ", parts.ToArray()));
            Echo("Message sent.");
            break;
        case "ReadMessages":
            ReceiveMessages();
            break;
        default:
            Echo(
                "Available commands:\n"
                //+ "  * SetAntenna <antenna name>\n"
                + "  * SetChannel <new channel name>\n"
                + "  * SendMessage <Message text>\n"
                + "  * ReadMessages\n"
            );
            break;
    }
}

/*public void SetAntenna(string name)
{
    IMyFunctionalBlock antenna = (GetBlock(name) as IMyFunctionalBlock);
    antenna =  antenna != null ? antenna : (GetBlockByName(name) as IMyFunctionalBlock);

    if (antenna == null) return;
    if (antenna is IMyLaserAntenna) {
        (antenna as IMyLaserAntenna).AttachedProgrammableBlock = Me.EntityId;
    } else if (antenna is IMyRadioAntenna) {
        (antenna as IMyRadioAntenna).AttachedProgrammableBlock = Me.EntityId;
    } else {
        return;
    }

    if (Antenna != null) {
        if (Antenna.Block is IMyLaserAntenna) {
        (Antenna.Block as IMyLaserAntenna).AttachedProgrammableBlock = 0L;
        } else {
            (Antenna.Block as IMyRadioAntenna).AttachedProgrammableBlock = 0L;
        }
    }
    
    Antenna = new Module(antenna);
    SetChannel(Channel);
}*/

public void SetChannel(string channel) {
    Channel = channel;
    List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
    IGC.GetBroadcastListeners(listeners);
    BroadcastListener = IGC.RegisterBroadcastListener(Channel);
    BroadcastListener.SetMessageCallback("ReadMessages");
}

public void ReceiveMessages()
{
    if (BroadcastListener == null) return;

    while(BroadcastListener.HasPendingMessage) {
        MyIGCMessage message = BroadcastListener.AcceptMessage();
        string incoming = message.Data.ToString();

        if (incoming == LastSendData || incoming == LastRepeatedData) 
        {
            return; // ignore own echoed data
        }
            
        string[] stack = incoming.Split('|');
        stack = stack.Skip(1).ToArray(); // remove timestamp
        string messageText =  String.Join("|", stack);
        Traffic.Add("< " + messageText);
        AddCall(Bus, "API://Dispatch/RadioData/" + GetId(Me) +  "/" + messageText);
        
        /* // repeat
        IGC.SendBroadcastMessage(Channel, incoming);
        LastRepeatedData = incoming;
        */
    }
}

public IMyTerminalBlock GetBlock(string id)
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
    
    if(block != null && block.CubeGrid == Me.CubeGrid) {
        return block;
    }
    
    return null;
}

public string GetId(IMyTerminalBlock block)
{
    return block.EntityId.ToString();
}

public IMyTerminalBlock GetBlockByName(string name)
{
    // The Block inventory.
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(name, blocks);
    
    for(int i = 0; i < blocks.Count; i++) {
        IMyTerminalBlock block = blocks[i];
        if (block.CubeGrid  == Me.CubeGrid && block.CustomName == name) {
            return block;
        }
    }
    
    return null;
}

public List<Module> FindCores() {
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    List<Module> result = new List<Module>();
    
    GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(blocks);
    
    for(int i = 0; i < blocks.Count; i++) {
        if(blocks[i].CubeGrid != Me.CubeGrid) continue;
        
        if(blocks[i].CustomData.IndexOf("MODULE=Core") != -1) {
            Module core = new Module((IMyProgrammableBlock)blocks[i]);
            result.Add(core);
        }
    }
    
    return result;
}

public void AddCall(Module module, String argument) {
    CallStack.Add(new Call(module, argument));
}

public void ExecuteCalls()
{
    if(Core == null) {
        CallStack.Clear();
        return;
    }
    List<Call> stack = CallStack;
    CallStack = new List<Call>();

    foreach(Call call in stack) {
    string targetId = call.GetId();
        if ((Core.Block as IMyProgrammableBlock).TryRun("API://Execute/" + targetId + "/" + call.Argument) == false) {
            CallStack.Add(call);
        }
    }

    if(CallStack.Count > 0) {
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }
}

public void RegisterOnFirstCore()
{
    List<Module> cores = FindCores();
    foreach (Module core in cores) {
        if ((core.Block as IMyProgrammableBlock).TryRun("API://RegisterModule/" + GetId(Me)) == false) continue;
        Runtime.UpdateFrequency = UpdateFrequency.None;
        Core = core;
        return;
    }
    
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
 }

public void RegisterOnFirstBus(String coreId, String[] busses)
{
    foreach(String busId in busses) {
        IMyProgrammableBlock block = GetBlock(busId) as IMyProgrammableBlock;
        if (block == null) continue;

        Module bus = new Module(block);
        if (block.TryRun("API://AddListener/SendRadio/" + GetId(Me))) {
            Bus = bus;
            return;
        }
    }

    OnCoreRegistration();
}

public void Uninstall()
{
    if (Bus != null) AddCall(Bus, "API://RemoveListener/SendRadio/" + GetId(Me));
    if (Core != null) AddCall(Core, "API://RemoveModule/" + GetId(Me));
    Echo("Deinstalled.");
}

public void ApplyAPICommunication(String apiInput)
{
    String[] arg = apiInput.Replace("API://", "").Split('/');
    
    switch(arg[0]) {
        case "Registered": // core validated
            OnCoreRegistration();
            break;
        case "Removed": // external core removal
            OnRemoval();
            break;
        case "ListenerAdded":
            break;
        case "ListenerRemoved": // external core removal
            OnRemoval();
            break;
        case "ScheduleEvent": // core call
            OnTimeEvent(arg);
            break;
        case "Dispatched":
            if (arg[3] == Bus.ToString()) {
                string[] data = new string[arg.Length - 4];
                Array.Copy(arg, 4, data, 0, arg.Length - 4);
                OnEvent(arg[1], arg[2], String.Join("/", data));
            }
            break;
        case "CoreModules":
            RegisterOnFirstBus(arg[1], arg[2].Split(','));
            break;
        default:
            Echo("Unknown request: " + apiInput);
            break;
    }
}

public void OnCoreRegistration()
{
    AddCall(Core, "API://GetModules/" + GetId(Me));
}

public void OnRemoval()
{
    Core = null;
    Bus = null;
   // Antenna = null;
    Me.CustomData = "";
}

public void OnTimeEvent(String[] arg)
{
    if (Core == null || arg[1] != Core.ToString()) return;

    Core.Count = Int32.Parse(arg[2]);
}

public void OnEvent(String eventName, String sourceId, String data)
{
    IMyProgrammableBlock source = GetBlock(sourceId) as IMyProgrammableBlock;
    switch(eventName) {
        case "SendRadio":
            SendMessage(data);
            break;
        default:
            Echo("Unknown received event: " + eventName);
            break;
    }
}

public void SendMessage(String data) {
    String message = Timestamp + "|" + data;
    if(BroadcastListener != null) {
        IGC.SendBroadcastMessage(Channel, message, TransmissionDistance.AntennaRelay);
    }
    LastSendData = message;
    Traffic.Add("> " + data);
}

public String DebugTraffic()
{
    String output = "";

    int i;
    for(i=Traffic.Count - 1; i >= 0; i--) {
        output += Traffic[i]+"\n";
    }

    return output;
}