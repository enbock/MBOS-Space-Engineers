/**
* A Module.
*/
public class Module {
    public IMyProgrammableBlock Block;
    
    public Module(IMyProgrammableBlock block) {
        Block = block;
    }
    public String ToString() 
    { 
        return Block.NumberInGrid.ToString() + "|" + Block.CustomName;
    } 
}

/**
* Main program ;)
*/
public void Main(String argument)
{
    List<Module> cores = FindCores();
    
    if(cores.Count == 0) Echo("No cores found.");
    else Echo("Found cores:");
    
    foreach(Module i in cores) Echo(i.Block.DetailedInfo + '\n'); 
}

/**
* Get specific block.
* <param name="id">The block identifier.</param>
*/
public IMyTerminalBlock GetBlock(string id)
{
    string[] parts = id.Split('|');
    if (parts.Length != 2) return null;
    string name = parts[0].Trim();
    int gridNumber = Int32.Parse(parts[1].Trim());
    
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
        
        string[] info = blocks[i].DetailedInfo.Split('\n');
        if(info[0] == "MODULE=Core")
            result.Add(new Module((IMyProgrammableBlock)blocks[i]));
            
    }
    
    return result;
}
