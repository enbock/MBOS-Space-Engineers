
List<IMyGyro> Gyros = new List<IMyGyro>();
IMyRemoteControl ctrlFlight;

/**
 * Program start.
 */
public Program() {
    Gyros.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(Gyros);
    for(int i = Gyros.Count -1 ; i>= 0; i--) {
        if (Gyros[i].GyroOverride == false) {
            Gyros.Remove(Gyros[i]);
        }
    }
    ctrlFlight = GetBlockByName("[CtrlFlight]") as IMyRemoteControl;
}

/**
 * Program end.
 */
public void Save() {
    // nothing yet
}

/**
 * Stabilization.
 */
public void Main(string argument) {
    foreach (var g in Gyros)
    {
        MatrixD orientation = g.WorldMatrix.GetOrientation();

        Vector3D localGrav = Vector3D.Transform(
            ctrlFlight.GetTotalGravity(), 
            MatrixD.Transpose(ctrlFlight.WorldMatrix.GetOrientation())
        );

        ITerminalProperty<float> propGyroPitch = g.GetProperty("Pitch").AsFloat();
        ITerminalProperty<float> propGyroYaw   = g.GetProperty("Yaw"  ).AsFloat();
        ITerminalProperty<float> propGyroRoll  = g.GetProperty("Roll" ).AsFloat();

        propGyroRoll.SetValue(g, (float)localGrav.X / 10f * -1f);
        propGyroPitch.SetValue(g, (float)localGrav.Z / 10f * -1f);

    }
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