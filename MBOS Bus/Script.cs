const String VERSION = "0.1.0";
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
        return Block.NumberInGrid.ToString() + "|" + Block.CustomName;
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
}

List<Call> CallStack = new List<Call>();
List<Module> Registered = new List<Module>();
List<EventList> RegisteredEvents = new List<EventList>(); 

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
    
    InvokeCalls();
    
    if (argument.IndexOf("API://") == 0) {
        ApplyAPICommunication(argument);
    } else {
        if (argument == "UNINSTALL") {
            Uninstall();
        }
    }
    
    if (argument != "UNINSTALL"  && Registered.Count == 0) {
        RegisterOnCores();
    }
    
    Echo(
        "MODULE=Bus\n"
        + "VERSION=" + VERSION + "\n"
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
        case "Registered":
            IMyTerminalBlock block = GetBlock(arg[1]);
            if(block != null && block is IMyProgrammableBlock) {
                core = new Module((IMyProgrammableBlock)block);
                Registered.Add(core);
                core.ConfigLCD = GetBlock(arg[2]) as IMyTextPanel;
            }
            break;
        case "Removed":
            foreach(Module i in Registered) {
                if (i.ToString() == arg[1]) {
                    Registered.Remove(i);
                    break;
                }
            }
            break;
        case "ConfigLCD":
            AddLcdOfCore(arg[2], arg[1]);
            break;
        case "ScheduleEvent":
            foreach(Module i in Registered) {
                if (i.ToString() == arg[1]) {
                    i.CurrentCount = Int32.Parse(arg[2]);
                    break;
                }
            }
            break;
        default:
            Echo("Unknown request: " + apiInput);
            break;
    }
}

/**
* Add the LCD panel to the core.
*/
public void AddLcdOfCore(string coreId, string lcdId)
{
    IMyTextPanel lcd = GetBlock(lcdId) as IMyTextPanel;
    if (lcd == null) return;
    foreach(Module i in Registered) {
        if (i.ToString() == coreId) {
            i.ConfigLCD = lcd;
            return;
        }
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
    string name = parts[1].Trim();
    int gridNumber = Int32.Parse(parts[0].Trim());
    
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(name, blocks);
    
    for(int i = 0; i < blocks.Count; i++) {
        if (
            blocks[i].NumberInGrid == gridNumber 
            && blocks[i].CustomName == name
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
            result.Add(new Module((IMyProgrammableBlock)blocks[i]));
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
    return block.NumberInGrid.ToString() + "|" + block.CustomName;
}

/**
* Register on cores.
*/
public void RegisterOnCores()
{
    List<Module> cores = FindCores();
    String myId = Me.NumberInGrid.ToString() + "|" + Me.CustomName;
    
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