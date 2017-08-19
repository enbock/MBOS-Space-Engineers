// Module version
const String VERSION = "1.0.0";
// The data format version.
const String DATA_FORMAT = "1.0";

/**
* A Module.
*/
public class Module {
    public IMyProgrammableBlock Block;
    // Only for type core.
    public IMyTextPanel ConfigLCD;
    // Only for type busses.
    public Module Core;
    // Bus registered
    public bool Registered = false;
    
    /**
    * Construct object and store block reference.
    */
    public Module(IMyProgrammableBlock block) {
        Block = block;
    }

    /**
    * Return the string id.
    */
    public override String ToString() 
    { 
        return Block.NumberInGrid.ToString() + "|" + Block.BlockDefinition.SubtypeId;
    } 
}

/**
* A solar panel grid.
*/
public class PanelGrid {
    static Int32 NextId = 1;

    // Panel grid id.
    public Int32 Id;

    // Panel liste
    public List<IMySolarPanel> Panels = new List<IMySolarPanel>();

    // The grid
    public IMyCubeGrid CubeGrid;

    // Motor for yawing
    public IMyMotorBase Yaw;

    // Motor for pitching.
    public IMyMotorBase Pitch;

    /**
    * Create grid storage and build identifier.
    */
    public PanelGrid()
    {
        Id = NextId++;
    } 

    public override string ToString()
    {
        return Id.ToString();
    }
}

// Registered bus.
Module Bus  = null;
// Block cache.
List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
// Panel cache
List<PanelGrid> Panels = new List<PanelGrid>();

/**
* Store data.
*/
public void Save()
{   
    Storage = "FORMAT v" + DATA_FORMAT + "\n"
        + (
            Bus != null ? (
                "Bus=" + Bus + "*" + (
                    Bus.Core 
                    + "*" + (Bus.Core.ConfigLCD != null ? GetId(Bus.Core.ConfigLCD) : "") 
                    + "*" + (Bus.Registered == true ? "true" : "false")
                )
            ) : ""
        ) + " \n"
    ;
}

/**
* Storage loader.
*/
public Program()
{
    if (Storage.Length == 0) return;
    String[] store = Storage.Split('\n');
    foreach(String line in store) {
        if (line.IndexOf("FORMAT") == 0) {
            if(line != "FORMAT v" + DATA_FORMAT) return;
        }
        if (line.IndexOf("Bus=") == 0) {
            LoadBusFromConfig(line);
        }
    }
    DetailedInfo();
}

/**
* Load registered bus and core from config.
*/
public void LoadBusFromConfig(String config)
{
    Bus = null;

    String[] args = config.Split('=');
    if (args.Length != 2) return;
    String[] blocks = (args[1]).Split('*');
    if (blocks.Length != 4) return;

    IMyProgrammableBlock bus = GetBlock(blocks[0]) as IMyProgrammableBlock;
    IMyProgrammableBlock core = GetBlock(blocks[1]) as IMyProgrammableBlock;
    IMyTextPanel lcd = GetBlock(blocks[2]) as IMyTextPanel;

    if(bus == null || core == null) return;

    Bus = new Module(bus) { 
        Core = new Module(core) { 
            ConfigLCD = lcd 
        },
        Registered = blocks[3].Trim().Equals("true")
    };
    
    if (Bus.Registered) DispatchEvent("GridRefresh", "");
}

/**
* Main program ;)
*/
public void Main(String argument)
{
    Blocks.Clear();

    Echo("Arg: " + argument);

    if (argument != "UNINSTALL" && Bus == null) {
        Echo("Search bus...");
        SearchBus();
    }

    // Appply API interaction
    if (argument.IndexOf("API://") == 0) {
        ApplyAPICommunication(argument);
    } else {
        if (argument == "UNINSTALL") {
            Uninstall();
        } else {
            // Manually Updates
            UpdatePanels();
        }
    }
    
    DetailedInfo();
    
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
    
    List<IMyTerminalBlock> blocks = GetBlocks();
    
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
* Actualize and return the grid block list.
*/
public List<IMyTerminalBlock> GetBlocks() {
    if (Blocks.Count == 0) GridTerminalSystem.GetBlocks(Blocks);
    return Blocks;
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
            Module bus = new Module((IMyProgrammableBlock)blocks[i]);
            foreach(String j in info) {
                if(j.IndexOf("CORES=") == 0) {
                    String[] rows = (j.Split('=')[1]).Split('#');
                    foreach(String r in rows) {
                        String coreId = r.Split('*')[0];
                        foreach(Module core in cores) {
                            if(bus.Core == null && core.ToString() == coreId) {
                                bus.Core = core;
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

/**
* Output detail information.
*/
public void DetailedInfo()
{
    Echo(
        "MODULE=SolarPanelFinder\n"
        + "ID=" +GetId(Me) + "\n"
        + "VERSION=" + VERSION + "\n"
        + "Bus: " + (Bus != null? "found " + (Bus.Registered ? "and registered" : ", but in registration") : "unregistered") + "\n"
    );
}

/**
* Search the first Bus and register on it.
*/
public void SearchBus()
{
    List<Module> busses = FindBusses(FindCores());
    if (busses.Count == 0) return;
    Bus = busses[0];
    //
    AddCall(Bus.Core, Bus.ToString(), "API://AddListener/GridChanged/" + GetId(Me));
    /* / // or if needed
    AddCall(Bus.Core, Bus.Core.ToString(), "API://RegisterModule/" + GetId(Me));
    //*/
}

/**
* Removes BUS from system.
*/
public void Uninstall()
{
    Echo("Uninstall...");
    if (Bus == null) return;
    //
    AddCall(Bus.Core, Bus.ToString(), "API://RemoveListener/GridChanged/" + GetId(Me));
    /* / // or if needed
    AddCall(Bus.Core, Bus.Core.ToString(), "API://RemoveModule/" + GetId(Me));
    //*/
}

/**
* Send info to LCD's and Console.
*/
public void Output(String text)
{
    if (Bus == null || Bus.Core == null || Bus.Core.ConfigLCD == null) {
        Bus = null;
        Echo("No Core LCD to output: "+text);
        return;
    }
    Bus.Core.ConfigLCD.WritePublicText(text + "\n", true);
}

/**
* API handler.
*/
public void ApplyAPICommunication(String apiInput)
{
    String[] arg = apiInput.Replace("API://", "").Split('/');
    
    switch(arg[0]) {
        case "ListenerAdded":
            OnBusRegistration();
            break;
        case "ListenerRemoved":
            OnBusRemoval();
            break;
        case "Dispatched":
            if (arg[3] == Bus.ToString()) {
                string[] data = new string[arg.Length - 4];
                Array.Copy(arg, 4, data, 0, arg.Length - 4);
                OnEvent(arg[1], arg[2], String.Join("/", data));
            }
            break;
        default:
            Echo("Unknown request: " + apiInput);
            break;
    }
}

/**
* Bus event registered.
*/
public void OnBusRegistration()
{
    Bus.Registered = true;
    DispatchEvent("GridRefresh", "");
}

/**
* Bus event removed.
*/
public void OnBusRemoval()
{
    Bus = null;
}

/**
* Event handler
*/
public void OnEvent(String eventName, String sourceId, String data)
{
    IMyProgrammableBlock source = GetBlock(sourceId) as IMyProgrammableBlock;
    switch(eventName) {
        case "GridChanged":
            UpdatePanels();
            break;
        default:
            Echo("Unknown received event: " + eventName);
            break;
    }
}

/**
* Dispatch event to all busses.
*/
public void DispatchEvent(String type, String data)
{
    if(Bus.Registered) {
        Echo("Send " + type + " to " + Bus.ToString());
        AddCall(Bus.Core, Bus.ToString(), "API://Dispatch/" + type + "/" + GetId(Me) + "/" + data);
    }
}

/**
* Searches for panels
*/
public void UpdatePanels()
{
    List<IMyTerminalBlock> blocks = GetBlocks().FindAll(x => x is IMySolarPanel);
    List<IMyCubeGrid> blackList = new List<IMyCubeGrid>();
    blackList.Add(Me.CubeGrid);

    GetBlocks(); // update cache

    foreach (IMyTerminalBlock block in blocks) {
        // Skip solar panel on root grid.
        if (blackList.Exists(x => x == block.CubeGrid)) continue;
        // Find grid data
        PanelGrid grid = Panels.Find(x => x.CubeGrid == block.CubeGrid);
        if (grid == null) {
            grid = new PanelGrid() {
                CubeGrid = block.CubeGrid
            };
            Panels.Add(grid);
        }
        if(! grid.Panels.Exists(x => x == (IMySolarPanel)block)) {
            grid.Panels.Add((IMySolarPanel)block);
        }
        // Update custom name.
        block.SetCustomName(block.CustomName.Split('[')[0].Trim() + " [SolarGrid#" + grid.Id + "]");
    }

    foreach (PanelGrid grid in Panels) {
            grid.Pitch = Blocks.Find(x => x.CustomName.Contains("SolarPitch#" + grid.Id)) as IMyMotorBase;
            grid.Yaw = Blocks.Find(x => x.CustomName.Contains("SolarYaw#" + grid.Id)) as IMyMotorBase;
            if (grid.Pitch == null || grid.Yaw == null) {
                Echo("[SolarPitch#" + grid.Id + "] or [SolarYaw#" + grid.Id + "] Motor not found.");
            }
    }

    Echo("Found " + Panels.Count + " solar grids. " + Panels.FindAll(x => x.Yaw == null || x.Pitch == null).Count + " panel(s) are incomplete.");
    AddCall(Bus.Core, Bus.ToString(), "API://Dispatch/SolarGrids/" + GetId(Me) + "/" + String.Join(",", Panels.FindAll(x => x.Yaw != null && x.Pitch != null)));
}