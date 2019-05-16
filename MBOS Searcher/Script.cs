/**
* A Module.
*/

using System;

public class Module {
    public IMyProgrammableBlock Block;
    public String Type;
    // Only for type core.
    public IMyTextPanel ConfigLCD;
    // Only for type busses.
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
    List<Module> busses = FindBusses(cores);
    
    if(busses.Count == 0) Echo("No cores found.");
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

    if(busses.Count == 0) Echo("No busses found.");
    else Echo("Found busses:");
    
    foreach(Module bus in busses) {  
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