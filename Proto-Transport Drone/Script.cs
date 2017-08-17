string mode = "none";
IMyShipConnector connector;
IMyRemoteControl ctrlFlight;
IMyRemoteControl ctrlDock;
string need = "NONE";
string targetPosition = "NONE";
Vector3D targetVector = new Vector3D();
bool isLocked = false;

public Program() {
    connector = GetBlockByName("[Connector]") as IMyShipConnector;
    ctrlFlight = GetBlockByName("[CtrlFlight]") as IMyRemoteControl;
    ctrlDock = GetBlockByName("[CtrlDock]") as IMyRemoteControl;
}

public void Save() {
    // Nothing yet
}

public void Main(string argument) {

    string[] args = argument.Split('|');
    Echo("RUN:"+args[0]);

    isLocked = connector.Status == MyShipConnectorStatus.Connected;

    switch(args[0])
    {
        case "NEED":
            need = args[1];
            DisableAutoPilot();
            break;
        case "reset":
            mode = "none";
            need = "NONE";
            targetPosition = "NONE";
            DisableAutoPilot();
            break;

        // Undock test
        case "u":
            mode = "none";
            need = "UNDOCK";
            if(args.Length > 1) targetPosition = args[1];
            DisableAutoPilot();
            break;
        // Port dock test
        case "d":
            mode = "none";
            need = "DOCK";
            if(args.Length > 1) targetPosition = args[1];
            DisableAutoPilot();
            break;

        default:
            if (targetPosition == args[0]) {
                switch(need)
                {
                    case "DOCK":
                        DoFlightAndDock(args);
                        break;
                    case "UNDOCK":
                        DoUndock(args);
                        break;
                }
            }
            break;

    }

    if (mode == "done") {
        mode = "none";
        DisableAutoPilot();

        // Nächster befehl
        switch(need)
        {
            case "UNDOCK":
                need = "NONE";
                break;
            default:
                need = "NONE";
                break;
        }
    }

    Echo("Distance:" + (ctrlFlight.GetPosition() - targetVector));
    Echo("Need:" + need); 
    Echo("Mode:" + mode);
    Echo("Locked:" + (isLocked ? "yes" : "no"));
    Echo("Pos:" + targetPosition);
}

public void DoFlightAndDock(string[] args)
{
    Vector3D vec1 = new Vector3D(Double.Parse(args[1]),Double.Parse(args[2]),Double.Parse(args[3]));
    Vector3D vec2 = new Vector3D(Double.Parse(args[4]),Double.Parse(args[5]),Double.Parse(args[6]));
    Vector3D offsetFlight = connector.GetPosition() - ctrlFlight.GetPosition();
    Vector3D offsetDock = connector.GetPosition() - ctrlDock.GetPosition();

    bool flightOn = false;
    bool dockOn   = false;
    bool isReached;
    switch(mode) 
    {
        case "none":
            if (!isLocked) {
                Echo ("Go in flight..."); 
                ctrlFlight.ClearWaypoints();
                targetVector = vec1 - offsetFlight;
                ctrlFlight.AddWaypoint(targetVector, "Flight to");
                flightOn = true;
                mode = "flight";
            }
            break;
        case "flight":
            isReached = SmallThan(ctrlFlight.GetPosition() - targetVector, new Vector3D(6.0,6.0,6.0));
            flightOn = !isReached;

            if (isReached) {
                Echo ("Go in docking..."); 
                ctrlDock.ClearWaypoints();
                targetVector = vec2 - offsetDock;
                ctrlDock.AddWaypoint(targetVector, "Dock");
                dockOn = true;
                mode = "docking";
            }
            break;
        case "docking":
            //isReached = SmallThan(ctrlFlight.GetPosition() - (vec2 - offsetDock), new Vector3D(2.0,2.0,2.0));
            if (connector.Status == MyShipConnectorStatus.Connectable) {
                Echo ("Locking"); 
                if(connector.Status != MyShipConnectorStatus.Connected) connector.GetActionWithName("SwitchLock").Apply(connector);
                mode = "goCharge";
            } else {
                dockOn = true;
            }
            break;
        case "goCharge":
            Echo("Charge:"+(isLocked?1:0));
            if (isLocked) {
                switchEngines(false);
                switchBatteries();
                mode = "done";
            } else {Echo("NÖ");}
            break;
    }
    ctrlFlight.SetAutoPilotEnabled(flightOn);
    ctrlDock.SetAutoPilotEnabled(dockOn);
}

public bool SmallThan(Vector3D a, Vector3D b) {
    return 
        (a.X < 0 ? -a.X : a.X) < (b.X < 0 ? -b.X : b.X)
        && (a.Y < 0 ? -a.Y : a.Y) < (b.Y < 0 ? -b.Y : b.Y)
        && (a.Z < 0 ? -a.Z : a.Z) < (b.Z < 0 ? -b.Z : b.Z)
    ;
}

public void DoUndock(string[] args)
{
    Vector3D vec1 = new Vector3D(Double.Parse(args[1]),Double.Parse(args[2]),Double.Parse(args[3]));
    Vector3D offsetFlight = connector.GetPosition() - ctrlFlight.GetPosition();
    bool isReached;
    bool flightOn = false;
    switch(mode)
    {
        case "none":
            if(isLocked) {
                switchEngines(true);
                switchBatteries();
                connector.GetActionWithName("SwitchLock").Apply(connector);
                flightOn = false;
            } else {
                ctrlFlight.ClearWaypoints();
                targetVector = vec1 - offsetFlight;
                ctrlFlight.AddWaypoint(targetVector, "Undock");
                flightOn = true;
                mode="flight";
            }
            break;

        case "flight":
            isReached = SmallThan(ctrlFlight.GetPosition() - targetVector, new Vector3D(5.0,5.0,5.0));
            flightOn = !isReached;
            if(isReached) {
                mode = "done";
            }
            break;
    }
    ctrlFlight.SetAutoPilotEnabled(flightOn);
}

public void switchEngines(bool enabled)
{
    List<IMyThrust> thrusters = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters);
    for(var i = 0; i < thrusters.Count; i++) {
        IMyThrust thuster = thrusters[i];
        if (thuster.CubeGrid  == Me.CubeGrid) {
            thuster.GetActionWithName(enabled ? "OnOff_On" : "OnOff_Off").Apply(thuster);
        }
    }
}

public void switchBatteries()
{
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
    for(var i = 0; i < batteries.Count; i++) {
        IMyBatteryBlock battery = batteries[i];
        if (battery.CubeGrid  == Me.CubeGrid) {
            battery.GetActionWithName("Recharge").Apply(battery);
        }
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