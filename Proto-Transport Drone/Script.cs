string mode = "none";
Vector3D lastTarget = new Vector3D(0,0,0);
IMyShipConnector connector;
IMyRemoteControl ctrlFlight;
IMyRemoteControl ctrlDock;
string need = "NONE";

public Program() {
    connector = GetBlockByName("[Connector]") as IMyShipConnector;
    ctrlFlight = GetBlockByName("[CtrlFlight]") as IMyRemoteControl;
    ctrlDock = GetBlockByName("[CtrlDock]") as IMyRemoteControl;
}

public void Save() {
    // Nothing yet
}

public void Main(string argument) {
    Echo("RUN:"+argument);

    string[] args = argument.Split('|');

    bool isLocked = connector.Status == MyShipConnectorStatus.Connected;

    switch(args[0])
    {
        case "PORT":
            if (!isLocked && need=="PORT") {
                Echo("Run PORT with " + mode);
                DoPort(args);
            }
            break;
        case "NEED":
            need = args[1];
            ctrlDock.SetAutoPilotEnabled(false);
            ctrlFlight.SetAutoPilotEnabled(false);
            break;
        case "reset":
            mode = "none";
            lastTarget = new Vector3D();
            ctrlDock.SetAutoPilotEnabled(false);
            ctrlFlight.SetAutoPilotEnabled(false);
            break;
        default:
            break;

    }
}

public void DoPort(string[] args)
{
    Vector3D vec1 = new Vector3D(Double.Parse(args[1]),Double.Parse(args[2]),Double.Parse(args[3]));
    Vector3D vec2 = new Vector3D(Double.Parse(args[4]),Double.Parse(args[5]),Double.Parse(args[6]));
    Vector3D mePos = Me.GetPosition();
    Vector3D offsetFlight = connector.GetPosition() - ctrlFlight.GetPosition();
    Vector3D offsetDock = connector.GetPosition() - ctrlDock.GetPosition();

    Echo("PORT:"+(ctrlFlight.GetPosition() - (vec1 - offsetFlight)));
    Echo("DOCK:"+(ctrlDock.GetPosition() - (vec2 - offsetDock)));
    if (vec1 != lastTarget) {
        mode = "none";
        lastTarget = vec1; 
    }
    bool flightOn = false;
    bool dockOn   = false;
    switch(mode) 
    {
        case "none":
            Echo ("Go in flight..."); 
            ctrlFlight.ClearWaypoints();
            ctrlFlight.AddWaypoint(vec1 - offsetFlight, "Port");
            flightOn = true;
            mode = "flight";
            break;
        case "flight":
            /*if (ctrlFlight.IsAutoPilotEnabled == false) {
                Echo ("Go in docking..."); 
                ctrlDock.ClearWaypoints();
                ctrlDock.AddWaypoint(vec2 - offsetDock, "Dock");
                dockOn = true;
                mode = "docking";
            }*/
            break;
        case "docking":
            if (connector.Status == MyShipConnectorStatus.Connectable) {
                Echo ("Locking"); 
                if(connector.Status != MyShipConnectorStatus.Connected) connector.GetActionWithName("SwitchLock").Apply(connector);
                mode = "none";
                need = "NONE";
            } else {
                dockOn = true;
            }
            break;
    }
    ctrlFlight.SetAutoPilotEnabled(flightOn);
    ctrlDock.SetAutoPilotEnabled(dockOn);
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

/**
 * Turn off auto pilot
 */
public void DisableAutoPilot()
{
    ctrlFlight.SetAutoPilotEnabled(false);
    ctrlDock.SetAutoPilotEnabled(false);
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