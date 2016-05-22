﻿const String VERSION = "0.2.0";
const String DATA_FORMAT = "0.1";

/**
* A Module or core.
*/
public class Module {
    public IMyProgrammableBlock Block;
    public IMyTextPanel ConfigLCD;
    public int CurrentCount = 0;
    public int LastCount = -1;
    
    public Module(IMyProgrammableBlock block) {
        Block = block;
    }
    
    public String ToString() 
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
}

// List of calls for next round.
List<Call> CallStack = new List<Call>();
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

    if (Storage.Length == 0) return;
    String[] store = Storage.Split('\n');
    if(store[0] != "FORMAT v" + DATA_FORMAT) return;
    Registered.Clear();
    String[] cores = store[1].Split('#');
    foreach(String j in cores) {
        String[] ids = j.Split('*');
        module = GetBlock(ids[0]);
        if(module != null) {
            core = new Module((IMyProgrammableBlock) module);
            if(ids.Length > 1) core.ConfigLCD = GetBlock(ids[1]) as IMyTextPanel;
            Registered.Add(core);
        }
    }
    
    RegisteredEvents.Clear();
    for(int i = 2; i < store.Length; i++) {
        String data = store[i]; 
        if (data.Length == 0) continue;
        String[] parts = data.Split('='); 
        EventList l = new EventList(parts[0]);
        String[] ids = parts[1].Split('#');
        foreach(String id in ids) {
            module = GetBlock(id);
            if(module != null) 
                l.Observers.Add(new Module((IMyProgrammableBlock) module));
        }
    } 
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
        list += String.Join("#", modules.ToArray());
    }
    
    Storage = "FORMAT v" + DATA_FORMAT + "\n"
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
        + "*" + (core.ConfigLCD != null ? GetId(core.ConfigLCD) : "")
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
    //Echo("> " + argument);
    lastArg = argument;
    
    // clear buffer
    Blocks.Clear();
    
    // Apply buffered calls
    InvokeCalls();
    
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
    Echo(
        "MODULE=Bus\n"
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
        dump += "  " + list.Key + ": " +list.Observers.Count + "\n";
    }
    
    return dump;
}

/**
* API handler.
*
* Calls:
*    API://Registered/<BlockId>
*/
public void ApplyAPICommunication(String apiInput)
{
    string[] arg = apiInput.Replace("API://", "").Split('/');
    Module receiver = null;
    Module core;
    
    switch(arg[0]) {
        case "Registered": // core validated
            IMyTerminalBlock block = GetBlock(arg[1]);
            if(block != null && block is IMyProgrammableBlock) {
                core = new Module((IMyProgrammableBlock)block);
                Registered.Add(core);
                core.ConfigLCD = GetBlock(arg[2]) as IMyTextPanel;
            }
            break;
        case "Removed": // external core removal
            foreach(Module i in Registered) {
                if (i.ToString() == arg[1]) {
                    Registered.Remove(i);
                    break;
                }
            }
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
            RemoveListener(arg[1], arg[2], data);
            break;
        default:
            Echo("Unknown request: " + apiInput);
            break;
    }
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
    IMyTerminalBlock block;
    List<Module> result = new List<Module>();
    
    GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(blocks);
    
    for(int i = 0; i < blocks.Count; i++) {
        if(blocks[i].CubeGrid != Me.CubeGrid) continue;
        
        if(blocks[i].DetailedInfo.IndexOf("MODULE=Core") != -1) {
            String[] info = (blocks[i].DetailedInfo).Split('\n');
            Module core = new Module((IMyProgrammableBlock)blocks[i]);
            foreach(String j in info) {
                if(j.IndexOf("ConfigLCD=") == 0) {
                    core.ConfigLCD = GetBlock((j.Split('='))[1]) as IMyTextPanel;
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
        if (core.ConfigLCD == null) {
            Echo(core.ToString() + " has no LCD.");
            continue;
        }
        if (core.LastCount != core.CurrentCount) {
            core.ConfigLCD.WritePublicText(text, true);
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
* Run calls from stack.
*/
public void InvokeCalls()
{
    Call[] calls = CallStack.ToArray();
    CallStack.Clear();
    foreach(Call call in calls) 
    {
        call.Run();
    } 
}

/**
* Removes BUS from system.
*/
public void Uninstall()
{
    foreach(Module core in Registered) {
        core.Block.TryRun("API://RemoveModule/" + GetId(Me));
    }
    Registered.Clear(); // Don't wait for answer
}

public void AddListener(string eventName, string listener)
{
    IMyProgrammableBlock observer = GetBlock(listener) as IMyProgrammableBlock;
    if(observer == null) return;
}

public void RemoveListener(string eventName, string listener)
{
    IMyProgrammableBlock observer = GetBlock(listener) as IMyProgrammableBlock;
    if(observer == null) return;
}

public void RemoveListener(string eventName, string sender, string[] data)
{
}