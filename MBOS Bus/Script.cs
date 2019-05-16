const String VERSION = "1.2.0";
const String DATA_FORMAT = "1.1";

public class Module {
    public IMyProgrammableBlock Block;
    public int CurrentCount = 0;
    public int LastCount = -1;
    
    public Module(IMyProgrammableBlock block) {
        Block = block;
    }
    
    public override String ToString() 
    { 
        return Block.NumberInGrid.ToString() + "|" + Block.BlockDefinition.SubtypeId;
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
    public IMyProgrammableBlock Block;
    public String Argument;
    
    public Call(IMyProgrammableBlock block, String argument) {
        Block = block;
        Argument = argument;
    }
    
    public bool Run()
    {
        return Block.TryRun(Argument);
    }
    
    public String GetBlockId() 
    { 
        return Block.NumberInGrid.ToString() + "|" + Block.BlockDefinition.SubtypeId;
    } 
}

List<Module> RegisteredCores = new List<Module>();
List<EventList> RegisteredEvents = new List<EventList>(); 
List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
IMyTextSurface ComputerDisplay;

public Program()
{
    ComputerDisplay = Me.GetSurface(0);
    ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
    ComputerDisplay.ClearImagesFromSelection();
    ComputerDisplay.ChangeInterval = 0;

    IMyTerminalBlock module; 
    Module core;

    if (Me.CustomData.Length == 0) return;
    String[] store = Me.CustomData.Split('\n');
    if(store[0] != "FORMAT v" + DATA_FORMAT) return;
    RegisteredCores.Clear();
    String[] cores = store[2].Split('#');
    foreach(String coreId in cores) {

        module = GetBlock(coreId);
        if(module == null) continue;

        core = new Module((IMyProgrammableBlock) module);
        RegisteredCores.Add(core);
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
        + "MODULE=Bus"
        +  FormatRegisteredCores() + "\n"
        + list
    ;
}
 public String FormatRegisteredCores()
{
    List<String> modules = new List<String>();
    foreach(Module core in RegisteredCores) modules.Add(core.ToString());
    return String.Join("#", modules.ToArray());
}

String lastArg = "";
public void Main(string argument)
{
    lastArg = argument;
    
    Blocks.Clear();
    
    if (argument.IndexOf("API://") == 0) {
        ApplyAPICommunication(argument);
    } else {
        if (argument == "UNINSTALL") {
            Uninstall();
        } else {
            Echo("Available Commands:\n * UNINSTALL\n");
        }
    }
    
    if (argument != "UNINSTALL"  && RegisteredCores.Count == 0) {
        RegisterOnCores();
    }

    List<String> coreIds = new List<String>();
    List<int> coreCounts = new List<int>();
    foreach(Module module in RegisteredCores) {
        coreIds.Add(module.Block.CustomName);
        coreCounts.Add(module.CurrentCount);
    }
    string output = "[MBOS]"
        + " [" + System.DateTime.Now.ToLongTimeString() + "]" 
        + " [" + (coreCounts.Count > 0 ? String.Join("] [", coreCounts.ToArray()) : "0") + "]" 
        + "\n\n"
        + "[Bus v" + VERSION + "] " 
        + "Registered on cores: \n" 
        + (coreIds.Count > 0 ? ("    " + String.Join("\n    ", coreIds.ToArray()) + "\n") : "")
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
    Module core;
    
    switch(arg[0]) {
        case "Registered": // core validated
            IMyProgrammableBlock block = GetBlock(arg[1]) as IMyProgrammableBlock;
            if(block == null) break;
            if(RegisteredCores.Exists(x => x.Block == block)) break;
            core = new Module(block);
            RegisteredCores.Add(core);
            break;
        case "Removed": // external core removal
            Uninstall();
            break;
        case "ScheduleEvent": // core call
            foreach(Module i in RegisteredCores) {
                if (i.ToString() == arg[1]) {
                    i.CurrentCount = Int32.Parse(arg[2]);
                    break;
                }
            }
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

public IMyTerminalBlock GetBlock(string id)
{
    string[] parts = id.Split('|');
    if (parts.Length != 2) return null;
    string subTypeId = parts[1].Trim();
    int gridNumber = Int32.Parse(parts[0].Trim());
    
    List<IMyTerminalBlock> blocks = Blocks;
    if (blocks.Count == 0) GridTerminalSystem.GetBlocks(blocks);
    
    for(int i = 0; i < blocks.Count; i++) {
        if (
            blocks[i].NumberInGrid == gridNumber 
            && blocks[i].BlockDefinition.SubtypeId == subTypeId
            && blocks[i].CubeGrid  == Me.CubeGrid
        ) {
            return blocks[i];
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

public string GetId(IMyTerminalBlock block)
{
    return block.NumberInGrid.ToString() + "|" + block.BlockDefinition.SubtypeId;
}

public void RegisterOnCores()
{
    List<Module> cores = FindCores();
    foreach(Module core in cores) {
        core.Block.TryRun("API://RegisterModule/" + GetId(Me));
    }  
}

public void Uninstall()
{
    foreach(EventList list in RegisteredEvents) {
        foreach(Module observer in list.Observers) 
            AddCall(observer.ToString(), "API://ListenerRemoved/" + list.Key + "/" + GetId(Me));
    }
    RegisteredEvents.Clear();
    foreach(Module core in RegisteredCores) {
        core.Block.TryRun("API://RemoveModule/" + GetId(Me));
    }
    RegisteredCores.Clear(); // Don't wait for answer
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
    AddCall(GetId(observer), "API://ListenerAdded/" + eventName + "/" + GetId(Me));
}

public void RemoveListener(string eventName, string listener)
{
    IMyProgrammableBlock observer = GetBlock(listener) as IMyProgrammableBlock;
    if(observer == null) return;

    EventList list = RegisteredEvents.Find(x => x.Key == eventName);
    if(list == null) {
        AddCall(GetId(observer), "API://ListenerRemoved/" + eventName + "/" + GetId(Me));
        return;
    };

    Module registeredObserver = list.Observers.Find(x => x.Block == observer);
    if(registeredObserver != null) {
        list.Observers.Remove(registeredObserver);
    }
    AddCall(GetId(observer), "API://ListenerRemoved/" + eventName + "/" + GetId(Me));
}

public void DispatchEvent(string eventName, string sender, string data)
{
    EventList list = RegisteredEvents.Find(x => x.Key == eventName);
    if(list == null) return;
    foreach(Module module in list.Observers) {
        Echo(eventName + " -> " + sender);
        AddCall(module.ToString(), "API://Dispatched/" + eventName + "/" + sender + "/" + GetId(Me) + "/" + data);
    }
}

public void AddCall(String blockId, String argument) {
    foreach(Module core in RegisteredCores) {
        String configText = core.Block.CustomData;

        if (configText.Length > 0) { 
            String data = "";
            String[] configs = configText.Split('\n');

            foreach(String line in configs) {
                if (line.Length > 0) {
                    string[] parts = line.Split('=');
                    if (parts[0] == "CallStack") {
                        String stack = String.Empty;
                        // read config of stack
                        if(parts.Length == 2) stack = parts[1];

                        // Add to stack
                        if(stack == String.Empty) {
                            stack = blockId + "~" + argument;
                        } else {
                            stack += "#" + blockId + "~" + argument;
                        }

                        // Write stack to config
                        data += "CallStack=" + stack + "\n";
                    } else {
                        data += line + "\n";
                    }
                }
            } 

            core.Block.CustomData = data;
            core.Block.TryRun("");
        } else {
            Echo("Missing config in Custom Data of core:" + core.ToString());
        }
    }
}