const String VERSION = "1.1.1";
const String DATA_FORMAT = "1.1";

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
    public bool EventRegistered = false;
    
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
List<Module> Busses = new List<Module>();
// Registed cores
List<Module> RegisteredCores = new List<Module>();
// Grid infos
int LocalGridCount = 0;
int GlobalGridCount = 0;

/**
* Store data.
*/
public void Save()
{   
    Storage = "FORMAT v" + DATA_FORMAT + "\n"
        + FormatRegisteredCores() + "\n"
        + FormatBusses()
    ;
}

/**
* Storage loader.
*/
public Program()
{
    IMyTerminalBlock module; 
    Module core;
    Module bus;

    if (Storage.Length == 0) return;
    String[] store = Storage.Split('\n');
    if(store[0] != "FORMAT v" + DATA_FORMAT) return;
    RegisteredCores.Clear();
    String[] cores = store[1].Split('#');
    foreach(String j in cores) {
        String[] ids = j.Split('*');
        module = GetBlock(ids[0]);
        if(module != null) {
            core = new Module((IMyProgrammableBlock) module, "Core");
            if(ids.Length > 1) core.ConfigLCD = GetBlock(ids[1]) as IMyTextPanel;
            RegisteredCores.Add(core);
        }
    }
    Busses.Clear();
    if (store.Length >= 3 && store[2].Length > 0) {
        Echo("Load "+store[2].Length);
        String[] busses = store[2].Split('#');
        foreach(String j in busses) {
            String[] ids = j.Split('*');
            module = GetBlock(ids[0]);
            bool registered = ids[1].Trim() == "true" ? true : false;
            cores = ids[2].Split('+');
            if(module != null) {
                bus = new Module((IMyProgrammableBlock) module, "Bus") { EventRegistered = registered };
                Busses.Add(bus);
                foreach(String i in cores) {
                    core = RegisteredCores.Find(x => x.ToString() == i);
                    if (core != null) {
                        bus.Cores.Add(core);
                    }
                }
            }
        }
    }
    DetailedInfo();
}

// Format Cores to storable form (serialize).
public String FormatRegisteredCores()
{
    List<String> modules = new List<String>();
    foreach(Module core in RegisteredCores) modules.Add(
        core.ToString()
        + "*" + (core.ConfigLCD != null ? GetId(core.ConfigLCD) : "")
    );
    return String.Join("#", modules.ToArray());
}

public String FormatBusses()
{
    List<String> modules = new List<String>();
    foreach(Module bus in Busses) {
        List<String> cores = new List<String>();
        foreach(Module core in bus.Cores) cores.Add(core.ToString());
        modules.Add(
            bus.ToString()
            + "*" + (bus.EventRegistered ? "true" : "false")
            + "*" + String.Join("+", cores.ToArray())
        );
    }
    return String.Join("#", modules.ToArray());
}

/**
* Main program ;)
*/
public void Main(String argument)
{
    Echo(argument + "\n");

    if (argument != "UNINSTALL") {
        if(Busses.Count == 0) {
            UpdateBusses();
        }

        if (Busses.Count == 0) {
            Echo("No Busses or Cores found.");
        }
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
    Busses = FindBusses(FindCores());
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
    
    if(cores.Count == 0) {
        return result;
    }
    
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
    var regCount = 0;
    foreach(Module m in Busses) if(m.EventRegistered) regCount++;
    Echo(
        "MODULE=GridObserver\n"
        + "ID=" +GetId(Me) + "\n"
        + "VERSION=" + VERSION + "\n"
        + "Core Count: " + RegisteredCores.Count + "\n"
        + "Bus Count: " + Busses.Count + " (Registed: " + regCount + ")\n"
        + "Local Grid: " + LocalGridCount + "\n"
        + "Global Grid: " + GlobalGridCount + "\n"
    );
}

/**
* API handler.
*/
public void ApplyAPICommunication(String apiInput)
{
    string[] arg = apiInput.Replace("API://", "").Split('/');
    Module core;
    Module module;
    
    switch(arg[0]) {
        case "Registered": // core validated
            IMyTerminalBlock block = GetBlock(arg[1]);
            if(block != null && block is IMyProgrammableBlock) {
                core = new Module((IMyProgrammableBlock)block, "Core");
                RegisteredCores.Add(core);
                core.ConfigLCD = GetBlock(arg[2]) as IMyTextPanel;

                // Add bus event now 
                foreach(Module bus in Busses) {
                    if (bus.Cores.Exists(x => x.Block == block) && bus.EventRegistered == false) {
                        Echo("Send AddListener to " + bus.ToString());
                        foreach(Module c in bus.Cores) {
                            AddCall(c, bus.ToString(), "API://AddListener/GridRefresh/" + GetId(Me));
                        }
                    }
                }
                //*/
            }
            break;
        case "Removed": // external core removal
            foreach(Module i in RegisteredCores) {
                if (i.ToString() == arg[1]) {
                    /**
                    * TODO: It could be, that the core was from a second bus used.
                    *       In that case should the second bus also removed by
                    *       remove listener call.
                    */

                    RegisteredCores.Remove(i);
                    break;
                }
            }
            LocalGridCount = 0;
            GlobalGridCount = 0;
            break;
        case "ScheduleEvent": // core call
            Blocks.Clear(); // clean cache
            foreach(Module i in RegisteredCores) {
                if (i.ToString() == arg[1]) {
                    i.CurrentCount = Int32.Parse(arg[2]);
                    break;
                }
            }
            UpdateBlockCount();
            Output("[Grid Observer v" + VERSION + "] Blocks: " + LocalGridCount + " : " + GlobalGridCount);
            break;
        case "ListenerAdded": // Listener event added
            module = Busses.Find(x => x.ToString() == arg[2]);
            if (module != null) {
                module.EventRegistered = true;
            }
            DispatchGridChanged();
            break;
        case "ListenerRemoved": // Listener event removed
            module = Busses.Find(x => x.ToString() == arg[2]);
            if (module != null) {
                foreach(Module c in module.Cores) AddCall(c, c.ToString(), "API://RemoveModule/" + GetId(Me));
                Busses.Remove(module); // remove bus from list.
            }
            LocalGridCount = 0;
            GlobalGridCount = 0;
            break;
        case "Dispatched":
            switch(arg[1]) {
                case "GridRefresh":
                    DispatchGridChanged();
                    break;
                default:
                    Echo("Unknown received event: " + apiInput);
                    break;
            }
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
    foreach(Module bus in Busses) {
        foreach(Module core in bus.Cores) {
            bool found = false;
            foreach(Module registered in RegisteredCores) {
                if (registered.ToString() == core.ToString()) {
                    found = true;
                    return;
                }
            }
            if (found == false) {
                AddCall(core, core.ToString(), "API://RegisterModule/" + GetId(Me));
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
    foreach(Module bus in Busses) {
        Echo("Send RemoveListener to " + bus.ToString() + " "+ bus.Cores.Count);
        foreach(Module core in bus.Cores) {
            AddCall(core, bus.ToString(), "API://RemoveListener/GridRefresh/" + GetId(Me));
        }
    }
    /* Should via Event Removed be done. * /
    foreach(Module core in RegisteredCores) {
        AddCall(core, core.ToString(), "API://RemoveModule/" + GetId(Me));
    }
    //*/
    LocalGridCount = 0;
    GlobalGridCount = 0;
}

/**
* Send info to LCD's and Console.
*/
public void Output(String text)
{
    foreach(Module core in RegisteredCores) {
        if (core.ConfigLCD == null) {
            continue;
        }
        if (core.LastCount != core.CurrentCount) {
            core.ConfigLCD.WritePublicText(text + "\n", true);
            core.LastCount = core.CurrentCount;
        }
    }
}

/**
* Update block count and message to bus on changes.
*/ 
public void UpdateBlockCount()
{
    int oldLocal = LocalGridCount;
    int oldGlobal = GlobalGridCount;

    if (Blocks.Count == 0) GridTerminalSystem.GetBlocks(Blocks);

    LocalGridCount = 0;
    GlobalGridCount = 0;

    foreach(IMyTerminalBlock block in Blocks) {
        GlobalGridCount++;
        if (block.CubeGrid  == Me.CubeGrid) {
            LocalGridCount++;
        }
    }

    if (GlobalGridCount != oldGlobal) {
        DispatchGridChanged();
    }
}

/**
* Create the GridChanged event.
*/
public void DispatchGridChanged()
{
    DispatchEvent("GridChanged", GlobalGridCount + "|" + LocalGridCount);
}

/**
* Dispatch event to all busses.
*/
public void DispatchEvent(String type, String data)
{
    foreach(Module bus in Busses) {
        if(bus.EventRegistered) {
            Echo("Send " + type + " to " + bus.ToString());
            foreach(Module core in bus.Cores) {
                AddCall(core, bus.ToString(), "API://Dispatch/" + type + "/" + GetId(Me) + "/" + data);
            }
        }
    }
}

/**
* Add a call request to core's call stacks.
*/
public void AddCall(Module core, String blockId, String argument) {
    if (core.ConfigLCD == null) {
        Echo(core.ToString() + " has no LCD.");
        return;
    }
    String configText = core.ConfigLCD.GetPrivateText();

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
                    Echo("Send " + blockId + "~" + argument);
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

        core.ConfigLCD.WritePrivateText(data, false);
    } else {
        Echo("Missing config data in LCD of core:" + core.ToString());
    }
}