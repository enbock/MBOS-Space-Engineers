const String VERSION = "1.1.0";
const String DATA_FORMAT = "1.0";

/**
* A Module or core.
*/
public class Module {
    public IMyProgrammableBlock Block;
    public IMyTextPanel Display;
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

/**
* Event observer list.
*/
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

/**
* Call request.
*/
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

// List of registered cores.
List<Module> Registered = new List<Module>();
// List of registered events.
List<EventList> RegisteredEvents = new List<EventList>(); 
// Block Buffer
List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();

/**
* Storage loader.
*/
public Program()
{
    IMyTerminalBlock module; 
    Module core;

    if (Me.CustomData.Length == 0) return;
    String[] store = Me.CustomData.Split('\n');
    if(store[0] != "FORMAT v" + DATA_FORMAT) return;
    Registered.Clear();
    String[] cores = store[1].Split('#');
    foreach(String j in cores) {
        String[] ids = j.Split('*');
        module = GetBlock(ids[0]);
        if(module != null) {
            core = new Module((IMyProgrammableBlock) module);
            if(ids.Length > 1) core.Display = GetBlock(ids[1]) as IMyTextPanel;
            Registered.Add(core);
        }
    }
    
    RegisteredEvents.Clear();
    for(int i = 2; i < store.Length; i++) {
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

    DetailedInfo();
}

/**
* Store data.
*/
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
        +  FormatRegisteredCores() + "\n"
        + list
    ;
}
// Format Cores to storable form (serialize).
public String FormatRegisteredCores()
{
    List<String> modules = new List<String>();
    foreach(Module core in Registered) modules.Add(
        core.ToString()
        + "*" + (core.Display != null ? GetId(core.Display) : "")
    );
    return String.Join("#", modules.ToArray());
}

String lastArg = "";
/**
* Program logic.
*/
public void Main(string argument)
{
    //Echo("> " + lastArg);
    Echo(argument);
    lastArg = argument;
    
    // clear buffer
    Blocks.Clear();
    
    // Appply API interaction
    if (argument.IndexOf("API://") == 0) {
        ApplyAPICommunication(argument);
    } else {
        if (argument == "UNINSTALL") {
            Uninstall();
        }
    }
    
    // Autosearch for cores.
    if (argument != "UNINSTALL"  && Registered.Count == 0) {
        RegisterOnCores();
    }
    
    // Outputs
    DetailedInfo();
}

/**
* DetailedInfo output.
*/
public void DetailedInfo()
{
    Echo(
        "MODULE=" + (Registered.Count > 0 ? "Bus" : "") + "\n"
        + "VERSION=" + VERSION + "\n"
        + "ID=" + GetId(Me) + "\n"
        + "CORES=" +  FormatRegisteredCores() + "\n"
        + "RegisteredEvents=" + RegisteredEvents.Count + "\n"
    );
    Output(DumpEventList());
}

/**
* Event list status text.
*/
public String DumpEventList()
{
    string dump = "[Bus v" + VERSION + "] " 
        + "Registered Events:\n";
    foreach(EventList list in RegisteredEvents) {
        dump += "  " + list.Key + ": " +list.Observers.Count +"(";
        foreach(Module block in list.Observers) {
            dump += block.Block.CustomName + " ";
        }
        dump += ")\n";
    }
    
    return dump;
}

/**
* API handler.
*/
public void ApplyAPICommunication(String apiInput)
{
    string[] arg = apiInput.Replace("API://", "").Split('/');
    //Module receiver = null;
    Module core;
    
    switch(arg[0]) {
        case "Registered": // core validated
            IMyProgrammableBlock block = GetBlock(arg[1]) as IMyProgrammableBlock;
            if(block != null) {
                if (Registered.Exists(x => x.Block == block)) break;
                core = new Module(block);
                Registered.Add(core);
                core.Display = GetBlock(arg[2]) as IMyTextPanel;
            }
            break;
        case "Removed": // external core removal
            Uninstall();
            break;
        case "ScheduleEvent": // core call
            foreach(Module i in Registered) {
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

/**
* Get specific block.
* <param name="id">The block identifier.</param>
*/
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

/**
* Find cores on the grid.
*/
public List<Module> FindCores() {
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    List<Module> result = new List<Module>();
    
    GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(blocks);
    
    for(int i = 0; i < blocks.Count; i++) {
        if(blocks[i].CubeGrid != Me.CubeGrid) continue;
        
        if(blocks[i].DetailedInfo.IndexOf("MODULE=Core") != -1) {
            String[] info = (blocks[i].DetailedInfo).Split('\n');
            Module core = new Module((IMyProgrammableBlock)blocks[i]);
            foreach(String j in info) {
                if(j.IndexOf("Display=") == 0) {
                    core.Display = GetBlock((j.Split('='))[1]) as IMyTextPanel;
                }
            }
            result.Add(core);
        }
    }
    
    return result;
}

/**
* Send info to LCD's and Console.
*/
public void Output(String text)
{
    foreach(Module core in Registered) {
        if (core.Display == null) {
            Echo(core.ToString() + " has no LCD.");
            continue;
        }
        if (core.LastCount != core.CurrentCount) {
            core.Display.WritePublicText(text, true);
            core.LastCount = core.CurrentCount;
        }
    }
}

/**
* Generate id.
*/
public string GetId(IMyTerminalBlock block)
{
    return block.NumberInGrid.ToString() + "|" + block.BlockDefinition.SubtypeId;
}

/**
* Register on cores.
*/
public void RegisterOnCores()
{
    List<Module> cores = FindCores();
    foreach(Module core in cores) {
        //Echo("Request register on " + core.ToString());
        core.Block.TryRun("API://RegisterModule/" + GetId(Me));
    }  
}

/**
* Removes BUS from system.
*/
public void Uninstall()
{
    foreach(EventList list in RegisteredEvents) {
        foreach(Module observer in list.Observers) 
            AddCall(observer.ToString(), "API://ListenerRemoved/" + list.Key + "/" + GetId(Me));
    }
    RegisteredEvents.Clear();
    foreach(Module core in Registered) {
        core.Block.TryRun("API://RemoveModule/" + GetId(Me));
    }
    Registered.Clear(); // Don't wait for answer
}

/**
* Add event listener to observer list.
*/
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

/**
* Remove listener from observer list.
*/
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

/**
* Send a event.
*/
public void DispatchEvent(string eventName, string sender, string data)
{
    EventList list = RegisteredEvents.Find(x => x.Key == eventName);
    if(list == null) return;
    foreach(Module module in list.Observers) {
        AddCall(module.ToString(), "API://Dispatched/" + eventName + "/" + sender + "/" + GetId(Me) + "/" + data);
    }
}

/**
* Add a call request to core's call stacks.
*/
public void AddCall(String blockId, String argument) {
    foreach(Module core in Registered) {
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