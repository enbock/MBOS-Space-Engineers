const String VERSION = "1.0.0";
const String DATA_FORMAT = "1.0";

/**
* Key value memory.
*/
public class ConfigValue
{ 
    public String Key; 
    public String Value; 
     
    public ConfigValue(String data)  
    { 
        var parts = data.Split('='); 
        Key = parts[0]; 
        Value = parts[1]; 
    } 
    public ConfigValue(String key, string value)  
    { 
        Key = key; 
        Value = value; 
    } 
     
    public override String ToString()
    { 
        return Key + '=' + Value; 
    } 
}
/**
* Load storage into config memory.
*/
public void LoadConfigFromCustomData()
{ 
    
    string data = Me.CustomData;
    
    if (data.Length > 0) { 
        String[] configs = data.Split('\n'); 
        
        if(configs[0] != "FORMAT v" + DATA_FORMAT) {
            Echo("Error: Config is not in Format: FORMAT v" + DATA_FORMAT);
            return;
        }
        
        for(int i = 1; i < configs.Length; i++) {
            String line = configs[i]; 
            if (line.Length > 0) {
                string[] parts = line.Split('=');
                if(parts.Length != 2) continue;
                GetConfig(parts[0].Trim()).Value = parts[1].Trim();
            }
        } 
    } 
} 
 
/**
* Search/Create a config memory block.
*/
public ConfigValue GetConfig(String key) {
    ConfigValue config = Config.Find(x => x.Key == key);
    if(config != null) return config;
     
    ConfigValue newValue = new ConfigValue(key, String.Empty); 
    Config.Add(newValue); 
    return newValue; 
} 

// The central configuration.
List<ConfigValue> Config = new List<ConfigValue>(); 

List<IMyGyro> Gyros = new List<IMyGyro>();
IMyRemoteControl ctrlFlight = null;

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
    initProgram();
}

public void initProgram()
{
    LoadConfigFromCustomData();
    ctrlFlight = GetBlockByName(GetConfig("RemoteControl").Value) as IMyRemoteControl;
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
    if (ctrlFlight == null) {
        initProgram();
        if (ctrlFlight == null) {
            Echo("Error: Missing configuration 'RemoteControl'.");
            return;
        }
    }

    Echo("Stabilization program is operating.");

    foreach (var g in Gyros)
    {
        // Skip not overrided gyro
        if (!g.GyroOverride) {
            continue;
        }

        MatrixD orientation = g.WorldMatrix.GetOrientation();

        Vector3D localGrav = Vector3D.Transform(
            ctrlFlight.GetTotalGravity(), 
            MatrixD.Transpose(ctrlFlight.WorldMatrix.GetOrientation())
        );

        ITerminalProperty<float> propGyroPitch = g.GetProperty("Pitch").AsFloat();
        //ITerminalProperty<float> propGyroYaw   = g.GetProperty("Yaw"  ).AsFloat();
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