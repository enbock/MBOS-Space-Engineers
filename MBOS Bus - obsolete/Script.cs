const String VERSION = "1.3.0";
const String DATA_FORMAT = "1.2";

public class Module {
    public IMyProgrammableBlock Block;
    public int CurrentCount = 0;
    public int LastCount = -1;
    
    public Module(IMyProgrammableBlock block) {
        Block = block;
    }
    
    public override String ToString() 
    { 
        return Block.EntityId.ToString();
    } 
}

public class EventList
{ 
    public String Key; 
    public List<Module> Observers;
     
    public EventList(String key)  
    { 
        Key = key; 
        Observers = new List<Module>(); 
    }
}

public class Call {
    protected Module Core;
    public IMyProgrammableBlock Block;
    public String Argument;
    
    public Call(Module core, IMyProgrammableBlock block, String argument) {
        Core = core;
        Block = block;
        Argument = argument;
    }
    
    public bool Run()
    {
        return Core.Block.TryRun("API://Execute/" + Block.EntityId.ToString() + "/" + Argument);
    }
}

Module RegisteredCore;
List<EventList> RegisteredEvents = new List<EventList>(); 
IMyTextSurface ComputerDisplay;
List<Call> CallStack = new List<Call>();

public Program()
{
    ComputerDisplay = Me.GetSurface(0);
    ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
    ComputerDisplay.ClearImagesFromSelection();
    ComputerDisplay.ChangeInterval = 0;

    IMyTerminalBlock module; 

    if (Me.CustomData.Length == 0) {
        Main("");
        return;
    }
    String[] store = Me.CustomData.Split('\n');
    if(store[0] != "FORMAT v" + DATA_FORMAT) {
        Main("");
        return;
    }
    RegisteredCore = null;
    String coreId = store[2];

    module = GetBlock(coreId);
    if(module != null) {
        RegisteredCore = new Module((IMyProgrammableBlock) module);
    }
    
    RegisteredEvents.Clear();
    for(int i = 3; i < store.Length; i++) {
        String data = store[i]; 
        if (data.Length == 0) continue;
        String[] parts = data.Split('='); 
        EventList eventList = new EventList(parts[0]);
        String[] ids = parts[1].Split('#');
        foreach(String id in ids) {
            module = GetBlock(id);
            if(module != null) 
                eventList.Observers.Add(new Module((IMyProgrammableBlock) module));
        }
        RegisteredEvents.Add(eventList);
    }
    Save();

    Main("");
}

public void Save()
{
    List<String> modules;
    
    string list = "";
    foreach(EventList i in RegisteredEvents) {
        list += i.Key + "=";
        modules = new List<String>();
        foreach(Module m in i.Observers) modules.Add(m.ToString());
        list += String.Join("#", modules.ToArray()) + "\n";
    }
    
    Me.CustomData = "FORMAT v" + DATA_FORMAT + "\n"
        + "MODULE=Bus\n"
        +(RegisteredCore != null ? RegisteredCore.ToString() : "") + "\n"
        + list
    ;
}

String lastArg = "";
public void Main(string argument)
{
    Runtime.UpdateFrequency = UpdateFrequency.None;
    lastArg = argument;

    if (argument.IndexOf("API://") == 0) {
        ApplyAPICommunication(argument);
    } else {
        if (argument == "UNINSTALL") {
            Uninstall();
        } else {
            Echo("Available Commands:\n * UNINSTALL\n");
    
            if (RegisteredCore == null) {
                RegisterOnCore();
            }
        }
    }

    ExecuteCalls();

    string output = "[MBOS]"
        + " [" + System.DateTime.Now.ToLongTimeString() + "]" 
        + " [" + (RegisteredCore != null ? RegisteredCore.CurrentCount.ToString() : "0") + "]" 
        + "\n\n"
        + "[Bus v" + VERSION + "]\n" 
        + "Registered on core: " + (RegisteredCore != null ? (
                RegisteredCore.Block.CustomName
                + "\n   Stack:" + CallStack.Count
            ) : "") + "\n"
        + "Registered Events:\n"
        + DumpEventList()
    ;
    ComputerDisplay.WriteText(output);
}

public String DumpEventList()
{
    string dump = "";
    foreach(EventList list in RegisteredEvents) {
        dump += "  " + list.Key + ": " +list.Observers.Count +"(";
        foreach(Module block in list.Observers) {
            dump += block.Block.CustomName + " ";
        }
        dump += ")\n";
    }
    
    return dump;
}

public void ApplyAPICommunication(String apiInput)
{
    string[] arg = apiInput.Replace("API://", "").Split('/');
    
    switch(arg[0]) {
        case "Registered":
            IMyProgrammableBlock block = GetBlock(arg[1]) as IMyProgrammableBlock;
            if(block == null) break;
            RegisteredCore = new Module(block);
            break;
        case "Removed": 
            Uninstall(true);
            break;
        case "ScheduleEvent":
            if (RegisteredCore == null) break;
            RegisteredCore.CurrentCount = Int32.Parse(arg[2]);
            break;
        case "AddListener":
            AddListener(arg[1], arg[2]);
            break;
        case "RemoveListener":
            RemoveListener(arg[1], arg[2]);
            break;
        case "Dispatch":
            string[] data = new string[arg.Length - 3];
            Array.Copy(arg, 3, data, 0, arg.Length - 3);
            DispatchEvent(arg[1], arg[2], String.Join("/", data));
            break;
        default:
            Echo("Unknown request: " + apiInput);
            break;
    }

    Save();
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

public void RegisterOnCore()
{
    List<Module> cores = FindCores();
    foreach(Module core in cores) {
        if (core.Block.TryRun("API://RegisterModule/" + GetId(Me))) {
            Echo("Register on " + core.Block.CustomName + ": successful");
            return;
        }
    }  
    Echo("Register on any core: failed");
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Uninstall(bool withoutCoreRemoval = false)
{
    foreach(EventList list in RegisteredEvents) {
        foreach(Module observer in list.Observers) 
            AddCall(observer.Block, "API://ListenerRemoved/" + list.Key + "/" + GetId(Me));
    }
    RegisteredEvents.Clear();
    if (RegisteredCore != null && withoutCoreRemoval == false) {
        AddCall(RegisteredCore.Block, "API://RemoveModule/" + GetId(Me));
    }
    RegisteredCore = null;

    Echo("Deinstalled.");
}

public void AddListener(string eventName, string listener)
{
    IMyProgrammableBlock observer = GetBlock(listener) as IMyProgrammableBlock;
    if(observer == null) return;

    EventList list = RegisteredEvents.Find(x => x.Key == eventName);
    if(list == null) {
        list = new EventList(eventName);
        RegisteredEvents.Add(list);
    }

    if(!list.Observers.Exists(x => x.Block == observer)) {
        list.Observers.Add(new Module(observer));
    }
    AddCall(observer, "API://ListenerAdded/" + eventName + "/" + GetId(Me));
}

public void RemoveListener(string eventName, string listener)
{
    IMyProgrammableBlock observer = GetBlock(listener) as IMyProgrammableBlock;
    if(observer == null) return;

    EventList list = RegisteredEvents.Find(x => x.Key == eventName);
    if(list == null) {
        AddCall(observer, "API://ListenerRemoved/" + eventName + "/" + GetId(Me));
        return;
    };

    Module registeredObserver = list.Observers.Find(x => x.Block == observer);
    if(registeredObserver != null) {
        list.Observers.Remove(registeredObserver);
    }
    AddCall(observer, "API://ListenerRemoved/" + eventName + "/" + GetId(Me));
}

public void DispatchEvent(string eventName, string sender, string data)
{
    EventList list = RegisteredEvents.Find(x => x.Key == eventName);
    if(list == null) return;
    foreach(Module module in list.Observers) {
        Echo(eventName + " -> " + sender);
        AddCall(module.Block, "API://Dispatched/" + eventName + "/" + sender + "/" + GetId(Me) + "/" + data);
    }
}

public void AddCall(IMyProgrammableBlock block, String argument) {
    CallStack.Add(new Call(RegisteredCore, block, argument));
}

public void ExecuteCalls()
{
    List<Call> stack = CallStack;
    CallStack = new List<Call>();

    foreach(Call call in stack) {
        if (call.Run() == false) {
            CallStack.Add(call);
        }
    }

    if(CallStack.Count > 0) {
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }
}