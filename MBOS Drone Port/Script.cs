// Module Name
const String NAME = "DronePort";
// Module version
const String VERSION = "1.0.11";
// The data format version.
const String DATA_FORMAT = "1.0";

/**
* A Module.
*/
public class Module {
    public IMyProgrammableBlock Block;
    // Only for type core.
    public IMyTextPanel Display;
    // Only for type busses.
    public Module Core;
    
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
 * Connector port for drone.
 */
public class DronePort
{
    public int Number;
    public String Action;
    public String Type = "ANY";
    public long Amount = 0L;
    public IMyShipConnector Connector;
    public Vector3I Offset = new Vector3I();
    protected String usedBy = String.Empty;
    public String CustomName;
    public String LastAction;

    /**
     * Create droneport from config string.
     */
    public DronePort(String configData, IMyGridTerminalSystem GridTerminalSystem, IMyCubeGrid CubeGrid)
    {
        string[] config = configData.Split(':');
        CustomName = config[2];

        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors);
        foreach (IMyShipConnector connector in connectors) {
            if (connector.CubeGrid == CubeGrid && connector.CustomName == CustomName) {
                Add(config, connector);
                break;
            }
        }
    }

    /**
     * Restore drone port.
     */
    public DronePort(IMyShipConnector connector)
    {
        Add(connector.CustomData.Split(':'), connector);
    }

    /**
     * Add data.
     */
    protected void Add(string[] config, IMyShipConnector connector)
    {
        Connector = connector;
        
        Number = Int32.Parse(config[1]);
        Action = config[3].Trim();
        Type = config[4].Trim();
        Amount = (long)Int32.Parse(config[5]);

        string[] offset = config[6].Split(',');
        Offset = new Vector3I(
            Int32.Parse(offset[0]), 
            Int32.Parse(offset[1]), 
            Int32.Parse(offset[2])
        );

        if (config.Length >= 8) {
            usedBy = config[7];
        }
        if (config.Length >= 9) {
            LastAction = config[8];
        }

        Save();
    }

    public void Save()
    {
        Connector.CustomData = ToString();
    }

    public String UsedBy {
        get { return usedBy; }
        set {
            usedBy = value;
            Save();
        }
    }

    /**
     * Return the config.
     */
    public override String ToString() 
    { 
        return "PORT:" + Number + ":" + Connector.CustomName + ":" + Action 
            + ":" + Type + ":" + Amount
            + ":" + Offset.X + "," + Offset.Y + "," + Offset.Z
            + ":" + usedBy + ":" + LastAction
        ;
    } 

}

// Registered bus.
Module Bus  = null;
// Block cache.
List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
List<DronePort> Ports = new List<DronePort>();

/**
* Store data.
*/
public void Save()
{   
    String data = "FORMAT v" + DATA_FORMAT + "\n"
        + (
            Bus != null ? (
                "Bus=" + Bus + "*" + (
                    Bus.Core + "*" + (
                        Bus.Core.Display != null ? GetId(Bus.Core.Display) : ""
                    )
                ) 
            ) : ""
        ) + "\n"
    ;
    Me.CustomData = data;
}

/**
* Storage loader.
*/
public Program()
{
    if (Me.CustomData.Length == 0) return;
    String[] store = Me.CustomData.Split('\n');
    foreach(String line in store) {
        String[] args = line.Split('=');
        if (line.IndexOf("FORMAT") == 0) {
            if(line != "FORMAT v" + DATA_FORMAT) return;
        }
        if (line.IndexOf("Bus=") == 0) {
            LoadBusFromConfig(line);
        }
    }

    LoadConfigs();

    Ports.Clear();
    List<IMyShipConnector> connectors = new List<IMyShipConnector>();
    GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors);
    foreach(IMyShipConnector connector in connectors) {
        if (
            connector.CubeGrid == Me.CubeGrid &&
            connector.CustomData.Length > 0 &&
            connector.CustomData.Substring(0, 4) == "PORT"
        ) {
            Ports.Add(new DronePort(connector));
        }
    }
    int i;
    for(i = Ports.Count -1 ; i >= 0; i--) {
        DronePort port = Ports[i];
        if (port.Connector == null) {
            Echo("Can not find connector: " + port.CustomName);
            Ports.Remove(port);
        }
    }
    Main("");
}

public void LoadConfigs()
{
    if (Me.CustomData.Length == 0) return;
    String[] store = Me.CustomData.Split('\n');
    foreach(String line in store) {
        String[] args = line.Split('=');
        if (line.IndexOf("FORMAT") == 0) {
            if(line != "FORMAT v" + DATA_FORMAT) return;
        }
    }

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
    if (blocks.Length != 3) return;

    IMyProgrammableBlock bus = GetBlock(blocks[0]) as IMyProgrammableBlock;
    IMyProgrammableBlock core = GetBlock(blocks[1]) as IMyProgrammableBlock;
    IMyTextPanel lcd = GetBlock(blocks[2]) as IMyTextPanel;

    if(bus == null || core == null) return;

    Bus = new Module(bus) { 
        Core = new Module(core) { 
            Display = lcd 
        } 
    };
}

/**
* Main program ;)
*/
public void Main(String argument)
{
    DronePort old;

    Blocks.Clear();
    LoadConfigs();

    //Echo("Arg: " + argument);

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
        } else if (argument == "DERESERVE") {
            foreach(DronePort port in Ports) {
                port.UsedBy = String.Empty;
            }
        } else if (argument != String.Empty) {
            String[] args = argument.Split(':');
            switch (args[0]) {
                case "SET":
                    old = GetPort(Int32.Parse(args[1]));
                    if (old != null) Ports.Remove(old);
                    Ports.Add(new DronePort(argument, GridTerminalSystem, Me.CubeGrid));
                    int i;
                    for(i = Ports.Count -1 ; i >= 0; i--) {
                        DronePort port = Ports[i];
                        if (port.Connector == null) {
                            Echo("Can not find connector: " + port.CustomName);
                            Ports.Remove(port);
                        }
                    }
                    break;
                case "CLEAR":
                    Ports.Clear();
                    break;
                case "REMOVE":
                    old = GetPort(Int32.Parse(args[1]));
                    if (old != null) 
                    {
                        old.Connector.CustomData = String.Empty;
                        Ports.Remove(old);
                    }
                    break;

            }
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
                    //Echo("Send " + blockId + "~" + argument);
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
        Echo("Missing config in CustomData of core:" + core.ToString());
    }
}

/**
* Output detail information.
*/
public void DetailedInfo()
{
    Echo(
        "\nMODULE=" + NAME + "\n"
        + "ID=" +GetId(Me) + "\n"
        + "VERSION=" + VERSION + "\n"
        + "Bus: " + (Bus != null ? Bus.ToString() : "unregistered") + "\n"
        + "NetID=" + MyName() + "\n"
    );

    string ports = "";
    foreach(DronePort port in Ports) {
        ports += "  " + port.ToString() + "\n";
        Echo(port.ToString());
    }

    Output("[" + NAME + " v" + VERSION + "] Registered ports:\n" + ports);
}

/**
* Search the first Bus and register on it.
*/
public void SearchBus()
{
    List<Module> busses = FindBusses(FindCores());
    if (busses.Count == 0) return;
    Bus = busses[0];
    //*/
    AddCall(Bus.Core, Bus.ToString(), "API://AddListener/RadioData/" + GetId(Me));
    /*/ // or if needed
    AddCall(Bus.Core, Bus.Core.ToString(), "API://RegisterModule/" + GetId(Me));
    //*/
}

/**
* Removes BUS from system.
*/
public void Uninstall()
{
    Echo("Uninstall...");
    Me.CustomData = String.Empty;
    if (Bus == null) return;
    //*/
    AddCall(Bus.Core, Bus.ToString(), "API://RemoveListener/RadioData/" + GetId(Me));
    /*/ // or if needed
    AddCall(Bus.Core, Bus.Core.ToString(), "API://RemoveModule/" + GetId(Me));
    //*/
}

/**
* Send info to LCD's and Console.
*/
public void Output(String text)
{
    if (Bus == null || Bus.Core == null || Bus.Core.Display == null) {
        Echo("No Core LCD to output.");
        return;
    }
    Bus.Core.Display.WritePublicText(text + "\n", true);
}

/**
* API handler.
*/
public void ApplyAPICommunication(String apiInput)
{
    String[] arg = apiInput.Replace("API://", "").Split('/');
    
    switch(arg[0]) {
        case "Registered": // core validated
            OnRegistration(arg);
            break;
        case "Removed": // external core removal
            OnRemoval(arg);
            break;
        case "ListenerAdded": // core validated
            OnRegistration(arg);
            break;
        case "ListenerRemoved": // external core removal
            OnRemoval(arg);
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
        default:
            Echo("Unknown request: " + apiInput);
            break;
    }
}

/**
* On Core registered.
*/
public void OnRegistration(String[] arg)
{
    // To something after core registered here ;)
}

/**
* From core removed.
*/
public void OnRemoval(String[] arg)
{
    Bus = null;
}

/**
* Core time call.
*/
public void OnTimeEvent(String[] arg)
{
    Output("[" + NAME + " v" + VERSION + "]");
}

/**
* Event handler
*/
public void OnEvent(String eventName, String sourceId, String data)
{
    IMyProgrammableBlock source = GetBlock(sourceId) as IMyProgrammableBlock;
    switch(eventName) {
        case "RadioData":
            string[] stack = data.Split('|');
            if (stack[0] == "NEED") {
                DoRequire(stack);
            } else if (stack[0] == MyName()) {
                if(stack[1] == "REQUEST") {
                    DoRequestPort(stack);
                } else if (stack[1] == "RELEASED") {
                    DoReleasePort(stack);
                } else if (stack[1] == "DOCKED") {
                    CleanDockPort(stack, false);
                    DoDockPort(stack);
                }
            } else if (stack[1] == "DOCKED") {
                CleanDockPort(stack, true);
            }
            break;
        default:
            Echo("Unknown received event: " + eventName);
            break;
    }
}

/**
 * Find port by number.
 */
public DronePort GetPort(int number)
{
    foreach(DronePort port in Ports) {
        if (port.Number == number) {
            return port;
        }
    }
    return null;
}

/**
 * Get my name (station grid id).
 */
public string MyName()
{
    return "" + Me.CubeGrid.EntityId;
}

/**
 * Answer for the action request.
 * Searches for free ports with action.
 */
public void DoRequire(string[] stack)
{
    Vector3D dronePosition = new Vector3D(
        Double.Parse(stack[2]),
        Double.Parse(stack[3]),
        Double.Parse(stack[4])
    );

    Echo("Receive question for " + stack[1] + ":");

    foreach(DronePort port in Ports) {
        if (port.UsedBy != String.Empty) {
            Echo("[-] Port "+port.Number+" is used.");
            continue;
        }
        if (port.Action != stack[1]) 
        {
            Echo("[-] Port "+port.Number+" is "+port.Action+".");
            continue;
        }

        if (port.Type != "ANY" ) {
            if (stack.Length < 7) {
                Echo ("[-] Port "+port.Number+" is not request without cargo type.");
                continue; // don't want all
            }
            string providedCargo = stack[6].Trim();
            if (port.Type != providedCargo) {
                Echo ("[-] Port "+port.Number+" do not provide "+providedCargo+".");
                continue; // dont want that cargo
            }
        }

        // Filters
        switch(port.Action) {
            case "LOAD": 
                if (CountCargoType(port.Type) <= port.Amount) {
                    // We dont have enough cargo
                    Echo("[-] Port "+port.Number+" no " + (port.Amount > 0 ? port.Amount + " of " : "" ) + port.Type + " in containers.");
                    continue;
                }
                break;

            case "CARGO":
                if (port.Amount != 0L && CountCargoType(port.Type) >= port.Amount) {
                    Echo ("[-] Port "+port.Number+" dont need "+port.Type+" yet.");
                    // no, we have enought
                    continue;
                }
                break;
        }

        
        Echo ("[+] Port "+port.Number+" can be used.");
        double distance = Vector3D.Distance(port.Connector.GetPosition(), dronePosition);
        string data = stack[5] + "|" + port.Action + "|" + port.Number + "|" + distance + "|" + MyName();
        AddCall(Bus.Core, Bus.ToString(), "API://Dispatch/SendRadio/" + GetId(Me) +  "/" + data);
        return;
    }
    Echo("This station has no ports for " + stack[1] + " yet.");
}

/**
 * Reserve a port for the ship.
 */
public void DoRequestPort(string[] stack)
{
    string data;
    foreach(DronePort port in Ports) {
        if (port.Number != Int32.Parse(stack[3])) continue;
        if (port.UsedBy != String.Empty) {
            if(port.UsedBy == stack[4]) return; // allready assigned

            // Assigned by other
            data = stack[4] + "|" + stack[2] + "|DENIED|" + MyName();
            AddCall(Bus.Core, Bus.ToString(), "API://Dispatch/SendRadio/" + GetId(Me) +  "/" + data);
            return;
        }
        //if (port.Action != stack[2]) continue;

        port.UsedBy = stack[4]; // reserve
        port.LastAction = "reserved";

        Vector3D  flightPoint = Me.CubeGrid.GridIntegerToWorld(port.Connector.Position - port.Offset);
        Vector3D  connectPoint = port.Connector.GetPosition();

        data = port.UsedBy + "|" + port.Action 
            + "|" + port.Number + "|RESERVED" 
            + "|" + flightPoint.X + "|" + flightPoint.Y + "|" + flightPoint.Z 
            + "|" + connectPoint.X + "|" + connectPoint.Y + "|" + connectPoint.Z 
            + "|" + MyName();
        AddCall(Bus.Core, Bus.ToString(), "API://Dispatch/SendRadio/" + GetId(Me) +  "/" + data);

        return;
    }
}

/**
 * Remove reservation(s) of a ship.
 */
public void DoReleasePort(string[] stack)
{
    foreach(DronePort port in Ports) {
        if (port.UsedBy != stack[2] || port.LastAction != "dock") continue;
        Echo("[ ] Port "+port.Number+" freed.");
        port.LastAction = String.Empty;
        port.UsedBy = String.Empty;
    }
}

/**
 * Remove reservation(s) of a ship when docked anywhere.
 */
public void CleanDockPort(string[] stack, bool force)
{
    foreach(DronePort port in Ports) {
        if (port.UsedBy == stack[3] && (force == true || port.LastAction == "dock")) {
            Echo("[ ] Port "+port.Number+" freed.");
            port.LastAction = String.Empty;
            port.UsedBy = String.Empty;
        }
    }
}

/**
 * Readd reservation(s) of a ship after dock.
 */
public void DoDockPort(string[] stack)
{
    foreach(DronePort port in Ports) {
        if (port.Number != Int32.Parse(stack[2])) continue;
        port.LastAction = "dock";
        port.UsedBy = stack[3];
        return;
    }
}


public long CountCargoType(string type)
{
    long amount = 0L;

    List<IMyCargoContainer> cargo = new List<IMyCargoContainer>();
    GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargo);
    foreach(IMyCargoContainer container in cargo) {
        if(container.CubeGrid == Me.CubeGrid) {
            IMyInventory inventory = container.GetInventory(0);
            if((double)inventory.CurrentVolume > 0) {
                List<IMyInventoryItem> itemList = inventory.GetItems();
                foreach(IMyInventoryItem item in itemList) {
                
                    // https://forum.keenswh.com/threads/getting-display-names-of-imyinventoryitem-cleanly.7391442/
                    string[] rawData = item.ToString().Split(new string[] { "er_" }, StringSplitOptions.RemoveEmptyEntries);
                    string[] itemData = rawData[1].Split('/');

                    if (itemData[0].Trim() == type.Trim()) {
                        amount += (long) item.Amount;
                    }

                    //Echo(">"+itemData[0]); // for debugging
                }
            }
        }
    }

    return amount;
}
