public Program() {
    // The constructor, called only once every session and
    // always before any other method is called. Use it to
    // initialize your script. 
    //     
    // The constructor is optional and can be removed if not
    // needed.
}

public void Save() {
    // Called when the program needs to save its state. Use
    // this method to save your state to the Storage field
    // or some other means. 
    // 
    // This method is optional and can be removed if not
    // needed.
}

public void Main(string argument) {
    // The main entry point of the script, invoked every time
    // one of the programmable block's Run actions are invoked.
    // 
    // The method itself is required, but the argument above
    // can be removed if not needed.
    IMyRadioAntenna ant = GetBlockByName("MX") as IMyRadioAntenna;
    IMyTerminalBlock connector = GetBlockByName("[Drop] Connector");
    Vector3D  pos = Me.CubeGrid.GridIntegerToWorld(connector.Position - new Vector3I(0,-10,0));
    Vector3D  pos2 = connector.GetPosition();
    ant.TransmitMessage("PORT|"+pos.X+"|"+pos.Y+"|"+pos.Z+ "|"+pos2.X+"|"+pos2.Y+"|"+pos2.Z, MyTransmitTarget.Default);
}


/**
* Get specific block.
* <param name="name">Name of block.</param>
*/
public IMyTerminalBlock GetBlockByName(string name)
{
    // The Block inventory.
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(name, blocks);
    
    for(int i = 0; i < blocks.Count; i++) {
        IMyTerminalBlock block = blocks[i];
        if (block.CubeGrid  == Me.CubeGrid && block.CustomName == name) {
            return block;
        }
    }
    
    return null;
}