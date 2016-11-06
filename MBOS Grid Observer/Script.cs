const String VERSION = "1.0.0";
const String DATA_FORMAT = "0.1";

/**
* A Module.
*/
public class Module {
    public IMyProgrammableBlock Block;
    public String Type;
    // Only for type core.
    public IMyTextPanel ConfigLCD;
    // Only for type busses.
    public List<Module> Cores = new List<Module>();
    // Core counts
    public int CurrentCount = 0;
    public int LastCount = -1;
    
    /**
    * Construct object and store block reference.
    */
    public Module(IMyProgrammableBlock block, String type) {
        Block = block;
        Type = type;
    }

    /**
    * Return the string id.
    */
    public override String ToString() 
    { 
        return Block.NumberInGrid.ToString() + "|" + Block.BlockDefinition.SubtypeId;
    } 
}

// Block Buffer
List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
// Bus list
List<Module> busses = new List<Module>();
// Registed cores
List<Module> registeredCores = new List<Module>();
// Grid infos
int localGridCount = 0;
int globalGridCount = 0;

/**
* Store data.
*/
public void Save()
{   
    Storage = "FORMAT v" + DATA_FORMAT + "\n"
        +  FormatRegisteredCores()
    ;
}

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
    registeredCores.Clear();
    String[] cores = store[1].Split('#');
    foreach(String j in cores) {
        String[] ids = j.Split('*');
        module = GetBlock(ids[0]);
        if(module != null) {
            core = new Module((IMyProgrammableBlock) module, "Core");
            if(ids.Length > 1) core.ConfigLCD = GetBlock(ids[1]) as IMyTextPanel;
            registeredCores.Add(core);
        }
    }
}

// Format Cores to storable form (serialize).
public String FormatRegisteredCores()
{
    List<String> modules = new List<String>();
    foreach(Module core in registeredCores) modules.Add(
        core.ToString()
        + "*" + (core.ConfigLCD != null ? GetId(core.ConfigLCD) : "")
    );
    return String.Join("#", modules.ToArray());
}

/**
* Main program ;)
*/
public void Main(String argument)
{
    if(busses.Count == 0) {
        UpdateBusses();
    }

    if (busses.Count == 0) {
        Echo("No Busses or Cores found. Module aborted.");
        Echo("Please run again after bus and core is installed.");
    }

    // Appply API interaction
    if (argument.IndexOf("API://") == 0) {
        ApplyAPICommunication(argument);
    } else {
        if (argument == "UNINSTALL") {
            Uninstall();
        }
    }
    
    DetailedInfo();
}

/**
* Update bus list.
*/
public void UpdateBusses()
{
    busses = FindBusses(FindCores());
    RegisterOnBusCores();
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
            Module core = new Module((IMyProgrammableBlock)blocks[i], "Core");
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
* Find busses on the grid.
* <param name="cores">Existant core list.</param>
*/
public List<Module> FindBusses(List<Module> cores) {
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    List<Module> result = new List<Module>();
    
    GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(blocks);
    
    for(int i = 0; i < blocks.Count; i++) {
        if(blocks[i].CubeGrid != Me.CubeGrid) continue;
        
        if(blocks[i].DetailedInfo.IndexOf("MODULE=Bus") != -1) {
            String[] info = (blocks[i].DetailedInfo).Split('\n');
            Module bus = new Module((IMyProgrammableBlock)blocks[i], "Bus");
            foreach(String j in info) {
                if(j.IndexOf("CORES=") == 0) {
                    String[] rows = ((j.Split('='))[1]).Split('#');
                    foreach(String r in rows) {
                        String coreId = r.Split('*')[0];
                        foreach(Module core in cores) {
                            if(core.ToString() == coreId) {
                                bus.Cores.Add(core);
                            }
                        }
                    }
                }
            }
            result.Add(bus);
        }
    }
    
    return result;
}

/**
* Generate id.
*/
public string GetId(IMyTerminalBlock block)
{
    return block.NumberInGrid.ToString() + "|" + block.BlockDefinition.SubtypeId;
}

/**
* Output detail information.
*/
public void DetailedInfo()
{
    Echo(
        "MODULE=GRIDOBSERVER\n"
        + "ID=" +GetId(Me) + "\n"
        + "VERSION=" + VERSION + "\n"
        + "Bus Count: " + busses.Count + "\n"
        + "Local Grid: " + localGridCount + "\n"
        + "Global Grid: " + globalGridCount + "\n"
    );
}


/**
* API handler.
*/
public void ApplyAPICommunication(String apiInput)
{
    string[] arg = apiInput.Replace("API://", "").Split('/');
    Module core;
    
    switch(arg[0]) {
        case "Registered": // core validated
            IMyTerminalBlock block = GetBlock(arg[1]);
            if(block != null && block is IMyProgrammableBlock) {
                core = new Module((IMyProgrammableBlock)block, "Core");
                registeredCores.Add(core);
                core.ConfigLCD = GetBlock(arg[2]) as IMyTextPanel;
            }
            break;
        case "Removed": // external core removal
            foreach(Module i in registeredCores) {
                if (i.ToString() == arg[1]) {
                    registeredCores.Remove(i);
                    UpdateBusses();
                    break;
                }
            }
            break;
        case "ScheduleEvent": // core call
            Blocks.Clear(); // clean cache
            foreach(Module i in registeredCores) {
                if (i.ToString() == arg[1]) {
                    i.CurrentCount = Int32.Parse(arg[2]);
                    break;
                }
            }
            UpdateBlockCount();
            Output("[Grid Observer v" + VERSION + "] Blocks: " + localGridCount + " : " + globalGridCount);
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
    
    if (Blocks.Count == 0) GridTerminalSystem.GetBlocks(Blocks);
    
    for(int i = 0; i < Blocks.Count; i++) {
        if (
            Blocks[i].NumberInGrid == gridNumber 
            && Blocks[i].BlockDefinition.SubtypeId == subTypeId
            && Blocks[i].CubeGrid  == Me.CubeGrid
        ) {
            return Blocks[i];
        }
    }
    
    return null;
}

/**
* Register on cores if not already registered.
*/
public void RegisterOnBusCores()
{
    foreach(Module bus in busses) {
        foreach(Module core in bus.Cores) {
            bool found = false;
            foreach(Module registered in registeredCores) {
                if (registered.ToString() == core.ToString()) {
                    found = true;
                    return;
                }
            }
            if (found == false) {
                core.Block.TryRun("API://RegisterModule/" + GetId(Me));
            }
        }
    }
}

/**
* Removes BUS from system.
*/
public void Uninstall()
{
    Echo("Uninstall...");
    foreach(Module core in registeredCores) {
        core.Block.TryRun("API://RemoveModule/" + GetId(Me));
    }
    busses.Clear();
    localGridCount = 0;
    globalGridCount = 0;
}

/**
* Send info to LCD's and Console.
*/
public void Output(String text)
{
    foreach(Module core in registeredCores) {
        if (core.ConfigLCD == null) {
            continue;
        }
        if (core.LastCount != core.CurrentCount) {
            core.ConfigLCD.WritePublicText(text, true);
            core.LastCount = core.CurrentCount;
        }
    }
}

/**
* Update block count and message to bus on changes.
*/ 
public void UpdateBlockCount()
{
    int oldLocal = localGridCount;
    int oldGlobal = globalGridCount;

    if (Blocks.Count == 0) GridTerminalSystem.GetBlocks(Blocks);

    localGridCount = 0;
    globalGridCount = 0;

    foreach(IMyTerminalBlock block in Blocks) {
        globalGridCount++;
        if (block.CubeGrid  == Me.CubeGrid) {
            localGridCount++;
        }
    }

    if (globalGridCount != oldGlobal) {
        DispatchEvent("GridChanged", "global");
    } else if (localGridCount != oldLocal) {
        DispatchEvent("GridChanged", "local");
    }
}

/**
* Dispatch event to all busses.
*/
public void DispatchEvent(String type, String data)
{
    foreach(Module bus in busses) {
        bus.Block.TryRun("API://Dispatch/" + type + "/" + GetId(Me) + "/" + data);
    }
}