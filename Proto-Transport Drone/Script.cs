public Program() {
    // The constructor, called only once every session and
    // always before any other method is called. Use it to
    // initialize your script. 
    //     
    // The constructor is optional and can be removed if not
    // needed.
    Gyros.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(Gyros);
}

public void Save() {
    // Called when the program needs to save its state. Use
    // this method to save your state to the Storage field
    // or some other means. 
    // 
    // This method is optional and can be removed if not
    // needed.
}

string mode = "none";
Vector3D lastTarget = new Vector3D(0,0,0);
List<IMyGyro> Gyros = new List<IMyGyro>();

public void Main(string argument) {
    // The main entry point of the script, invoked every time
    // one of the programmable block's Run actions are invoked.
    // 
    // The method itself is required, but the argument above
    // can be removed if not needed.
    string[] args = argument.Split('|');

    switch(args[0])
    {
        case "PORT":
            Vector3D vec1 = new Vector3D(Double.Parse(args[1]),Double.Parse(args[2]),Double.Parse(args[3]));
            Vector3D vec2 = new Vector3D(Double.Parse(args[4]),Double.Parse(args[5]),Double.Parse(args[6]));
            IMyTerminalBlock connector = GetBlockByName("[Connector]");
            IMyRemoteControl ctrl = GetBlockByName("[Ctrl]") as IMyRemoteControl;
            Vector3D mePos = Me.GetPosition();
            Vector3D offset = ctrl.GetPosition() - connector.GetPosition();

            foreach (var g in Gyros)
            {
                Vector3D localGrav=Vector3D.Transform(ctrl.GetTotalGravity(), MatrixD.Transpose(g.WorldMatrix.GetOrientation()));

                ITerminalProperty<float> propGyroPitch = g.GetProperty("Pitch").AsFloat();
                ITerminalProperty<float> propGyroYaw   = g.GetProperty("Yaw"  ).AsFloat();
                ITerminalProperty<float> propGyroRoll  = g.GetProperty("Roll" ).AsFloat();

                propGyroYaw.SetValue(g, (float)localGrav.X / 10.0f);
                propGyroPitch.SetValue(g, (float)localGrav.Y / 10.0f);
            }

            Echo (""+(mePos - (vec2 + offset)));
            Echo("------------");
            if (vec1 != lastTarget) {
                mode = "none";
                lastTarget = vec1; 
                Echo ("Reset mode...");
            }
            switch(mode) 
            {
                case "none":
                    Echo ("Go in flight..."); 
                    ctrl.ClearWaypoints();
                    ctrl.AddWaypoint(vec1 + offset, "Port");
                    ctrl.SetAutoPilotEnabled(true);
                    mode = "flight";
                    break;
                case "flight":
                    if (ctrl.IsAutoPilotEnabled == false) {
                        Echo ("Go in docking..."); 
                        ctrl.ClearWaypoints();
                        ctrl.AddWaypoint(vec2 + offset, "Dock");
                        //ctrl.SetAutoPilotEnabled(true);
                        mode = "docking";
                    }
                    break;
                case "reset":
                    mode = "none";
                    lastTarget = new Vector3D();
                    break;
            }
            Echo ("MODE: " + mode);
            break;
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





// PRETTY
// prevent visual information overload, ease parsing
public static class Pretty 
{
    static readonly float[] tcache = new float[] { 0f, .1f, .01f, .001f, .0001f };
    public static float NoTiny(float x, int dig = 1)
    {
        return Math.Abs(x) < (dig < tcache.Length ? tcache[dig] : Math.Pow(.1, dig)) ? x*float.Epsilon : (float)Math.Round(x, dig);
    }
    public static string _(float  f) { return NoTiny(f, 1).ToString("g3"); }
    public static string _(double d) { return NoTiny((float)d, 1).ToString("g4"); }

    const string degUnit = " °"; // angular degrees 
    public static string Degrees(double a) { return _((float)a) + degUnit; }
    public static string Radians(double a) { return Degrees(MathHelper.ToDegrees(a)); }
    public static string Degrees(Vector3 a) { return _(a) + degUnit; }
    public static string Radians(Vector3 a) { return Degrees(a * MathHelper.ToDegrees(1)); }
    public static string MultiLine(string name, Vector3 v, string unit) 
    { 
        return     name + "x: " + Pretty._(v.X)
          + '\n' + name + "y: " + Pretty._(v.Y) + ' ' + unit 
          + '\n' + name + "z: " + Pretty._(v.Z); 
    }

    static string oAxSep = " ";
    static readonly char[] iAxSep = new[] { ' ', '\t', ',' };
    public static string _(Vector3 v)
    {
        return _(v.X) + oAxSep + _(v.Y) + oAxSep + _(v.Z);
    }
    public static string _(Vector3D v)
    {
        return _(v.X) + oAxSep + _(v.Y) + oAxSep + _(v.Z);
    }
    public static string _(Quaternion q)
    {
        return _(q.X) + oAxSep + _(q.Y) + oAxSep + _(q.Z) + oAxSep + _(q.W); //q.ToString(); //
    } 
} 