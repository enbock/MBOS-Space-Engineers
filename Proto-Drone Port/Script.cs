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

IMyRadioAntenna antenna;
IMyTextPanel debug;
IMyTerminalBlock connector;
String InfoName = "";
Vector3I RelativeFlightTarget = new Vector3I(6, -20, 0);

public Program() {}

public void initProgram() {
    List<IMyRadioAntenna> Antennas = new List<IMyRadioAntenna>();
    GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(Antennas);
    int i;
    for(i = 0; i < Antennas.Count; i++) {
        if (Antennas[i].CubeGrid == Me.CubeGrid) {
            antenna = Antennas[i];
            i = Antennas.Count;
        }
    }
    LoadConfigFromCustomData();
    debug = GetBlockByName(GetConfig("Debug").Value) as IMyTextPanel;
    connector = GetBlockByName(GetConfig("Connector").Value) as IMyTerminalBlock;
    InfoName = GetConfig("InfoName").Value;

    String FlightTarget = GetConfig("FlightTarget").Value;
    if (FlightTarget != "") {
        string[] coords = FlightTarget.Split(',');
        RelativeFlightTarget = new Vector3I(float.Parse(coords[0]),float.Parse(coords[1]),float.Parse(coords[2]));
    }
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
    Echo("RUN:"+argument);

    if(connector == null || InfoName == "") {
        initProgram();
        if(connector == null || InfoName == "") {
            Echo ("Missing correct config of 'Connector' and 'InfoName'.");
            return;
        }
    }

    Vector3D  pos = Me.CubeGrid.GridIntegerToWorld(connector.Position - RelativeFlightTarget);
    
    Vector3D  pos2 = connector.GetPosition();

    var sendString = InfoName + "|"+pos.X+"|"+pos.Y+"|"+pos.Z+ "|"+pos2.X+"|"+pos2.Y+"|"+pos2.Z;
    bool sent = antenna.TransmitMessage(sendString); 
    if(debug != null) debug.WritePublicText(sendString);
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