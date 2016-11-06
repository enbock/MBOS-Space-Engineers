/**
* A Module.
*/
public class Module {
    public IMyProgrammableBlock Block;
    public String Type;
    // Only for type core.
    public IMyTextPanel ConfigLCD;
    // Only for type buses.
    public List<Module> Cores = new List<Module>();
    
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

/**
* Main program ;)
*/
public void Main(String argument)
{
    Echo("Arg:  " + argument);
    Echo("MyID: " + GetId(Me));
    
    List<Module> cores = FindCores();
    List<Module> buses = FindBuses(cores);
    
    if(buses.Count == 0) Echo("No cores found.");
    else Echo("Found cores:");

    foreach(Module i in cores) 
        Echo(
            " * " + GetId(i.Block)
            + (
                i.ConfigLCD != null 
                ? " with LCD " + GetId(i.ConfigLCD)
                : ""
            )
            + "\n"
        ); 

    if(buses.Count == 0) Echo("No buses found.");
    else Echo("Found buses:");
    
    foreach(Module bus in buses) {  
        List<String> coresInBus = new List<String>();
        foreach(Module core in bus.Cores) {
            coresInBus.Add(core.ToString());
        }
        Echo(
            " * " + GetId(bus.Block)
            + " with cores " + string.Join(", ", coresInBus.ToArray())
            + "\n"
        );
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
    
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(blocks);
    
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
* Find buses on the grid.
*/
public List<Module> FindBuses(List<Module> cores) {
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
                            if(core.ToString() == coreId) bus.Cores.Add(core);
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