const String VERSION = "0.3.1";
const String DATA_FORMAT = "0.3";

// The Block inventory.
List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();

/**
* Key value memory.
*/
public class ConfigValue { 
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
     
    public String ToString() 
    { 
        return Key + '=' + Value; 
    } 
}

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
// Registered modules.
List<Module> Modules = new List<Module>();

// The central configuration.
List<ConfigValue> Config = new List<ConfigValue>(); 
 
/**
* Main program ;)
*/
public void Main(String argument) { 
    LoadConfigFromConfigLCD();
    StopTimer();
    LoadModules();
    ReadArgument(argument); 
    
    CountRun(); 
    InvokeModules();
    UpdateModulesConfig();
    
    OutputToConfigLcd();
    OutputDebugToConfigLcd();
    OutputToConsole();
    
    StartTimer();
} 
 
/**
* Load storage into config memory.
*/
public Program() { 
    if (Storage.Length > 0) { 
        Config.Clear(); 
        String[] configs = Storage.Split('\n'); 
        
        if(configs[0] != "FORMAT v" + DATA_FORMAT) return;
        
        for(int i = 1; i < configs.Length; i++) {
            String data = configs[i]; 
            if (data.Length > 0) Config.Add(new ConfigValue(data)); 
        } 
    } 
} 
 
/**
* Store config memory.
*/
public void Save() { 
    Storage = FormatConfig(Config); 
} 

/**
* Convert config to storable string.
*/
public String FormatConfig(List<ConfigValue> config) 
{ 
    List<String> store = new List<String>();  
    int i; 
     
    for(i = 0; i < config.Count; i++) { 
        store.Add(config[i].ToString()); 
    } 
     
    return "FORMAT v" + DATA_FORMAT + "\n" + String.Join("\n", store.ToArray()); 
}
 
/**
* Search/Create a config memory block.
*/
public ConfigValue GetConfig(String key) { 
    int i; 
    for(i = 0; i < Config.Count; i++) { 
        if(Config[i].Key == key) return Config[i]; 
    } 
     
    ConfigValue newValue = new ConfigValue(key, String.Empty); 
    Config.Add(newValue); 
    return newValue; 
} 
 
/**
* Read the given arguments.
*/
public void ReadArgument(String args) 
{
    if (args == String.Empty) return;
    
    if (args.IndexOf("API://") == 0) {
        ApplyAPICommunication(args);
        return;
    }
     
    // Standard run of Core with arguments.
    String[] parts = args.Split(','); 
    if (parts.Length >= 1 && parts[0].Length > 0) GetConfig("ConfigLCD").Value = parts[0].Trim(); 
    if (parts.Length >= 2 && parts[1].Length > 0) GetConfig("MainTimer").Value = parts[1].Trim(); 
}

/**
* Increase the run counter.
*/
public void CountRun() 
{ 
    int runcount = 0; 
    ConfigValue count = GetConfig("RunCount"); 
    
    if(count.Value != String.Empty) { 
        runcount = Int32.Parse(count.Value) + 1; 
    } 
    if (runcount >= 1000) runcount = 0; 
    count.Value = runcount.ToString(); 
}

/**
* Start the core time.
*
* The timer will invoke the next loop.
*/
public void StartTimer()
{
    if(GetConfig("MainTimer").Value == String.Empty) return;
     
    ConfigValue runMode = GetConfig("RunMode");
    if (runMode.Value == String.Empty) runMode.Value = "normal";
    IMyTimerBlock timer = GridTerminalSystem.GetBlockWithName(GetConfig("MainTimer").Value) as IMyTimerBlock; 
    if(timer != null) {
        timer.ApplyAction(
            runMode.Value == "fast" ? "TriggerNow" : "Start"
        );
    } 
} 

/**
* Start the core time.
*
* The timer will invoke the next loop.
*/
public void StopTimer()
{
    if(GetConfig("MainTimer").Value == String.Empty) return; 

    IMyTimerBlock timer = GridTerminalSystem.GetBlockWithName(GetConfig("MainTimer").Value) as IMyTimerBlock; 
    if(timer != null) {
        timer.ApplyAction("Stop");
    } 
} 

/**
* Stores the config memory on the config lcd.
*/
public void OutputToConfigLcd()
{
    IMyTextPanel lcd = GridTerminalSystem.GetBlockWithName(GetConfig("ConfigLCD").Value) as IMyTextPanel; 
    if(lcd == null) {
        Echo ("No config screen found.");
        return;
    }
    
    List<ConfigValue> configs = new List<ConfigValue>();
    foreach(ConfigValue i in Config) {
        if (i.Key != "RunCount")
            configs.Add(i);
    }
    
    lcd.WritePrivateText(FormatConfig(configs), false);
}

/**
* Begin debug output in config screen.
*/
public void OutputDebugToConfigLcd()
{
    IMyTextPanel lcd = GridTerminalSystem.GetBlockWithName(GetConfig("ConfigLCD").Value) as IMyTextPanel; 
    if(lcd == null) {
        return;
    }
    
    string output = " [MBOS]"
        + " Core v" + VERSION 
        + " [" + System.DateTime.Now.ToLongTimeString() + "]" 
        + " [" + GetConfig("RunCount").Value + "]" 
        + "\n\n"
    ;
    
    output += "Registered Modules:\n";
    foreach(Module mod in Modules) {
        output += "    " + mod.ToString();
    }
    output += "\n";
    
    lcd.WritePublicText(output, false);
    lcd.ShowTextureOnScreen();
    lcd.ShowPublicTextOnScreen();
}

/**
* Output data which may read by other modules.
*/
public void OutputToConsole()
{
    Echo(
        "MODULE=Core\n"
        + "VERSION=" + VERSION + "\n"
        + "Count=" + GetConfig("RunCount").Value + "\n"
        + "ConfigLCD=" + GetConfig("ConfigLCD").Value + "\n"
    );
}

 
/**
* Load storage into config memory.
*/
public void LoadConfigFromConfigLCD() { 
    IMyTextPanel lcd = GridTerminalSystem.GetBlockWithName(GetConfig("ConfigLCD").Value) as IMyTextPanel; 
    if(lcd == null) {
        return;
    }
    
    string data = lcd.GetPrivateText();
    
    if (data.Length > 0) { 
        String[] configs = data.Split('\n'); 
        
        if(configs[0] != "FORMAT v" + DATA_FORMAT) return;
        
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
* Load module list from config.
*/
public void LoadModules()
{
    Modules.Clear();
    String list = GetConfig("RegisteredModules").Value;
    if (list == String.Empty) return;
    String[] moduleList = list.Split('#');
    
    foreach(string blockId in moduleList) {
        RegisterModule(blockId);
    }
}

public void UpdateModulesConfig()
{
    List<string> modules = new List<string>();
    foreach(Module id in Modules.ToArray()) modules.Add(id.ToString());
    GetConfig("RegisteredModules").Value = String.Join("#", modules.ToArray());
}

/**
* API handler.
*
* Calls:
*    API://RegisterModule/<BlockId>
*    API://RemoveModule/<BlockId>
*/
public void ApplyAPICommunication(String apiInput)
{
    string[] stack = apiInput.Replace("API://", "").Split('/');
    Module receiver = null;
    
    switch(stack[0]) {
        case "RegisterModule":
            receiver = RegisterModule(stack[1]);
            if (receiver != null) {
                receiver.Block.TryRun("API://Registered/"+GetMyId());
            }
            break;
        case "RemoveModule":
            receiver = RemoveModule(stack[1]);
            if (receiver != null) {
                receiver.Block.TryRun("API://Removed/"+GetMyId());
            }
            break;
    }
}

/**
* Register another module to trigger.
*/
public Module RegisterModule(String blockId)
{
    IMyTerminalBlock block = GetBlock(blockId);
    if(block == null || !(block is IMyProgrammableBlock)) return null;
    Module module = new Module((IMyProgrammableBlock)block);
    Modules.Add(module);
    
    return module;
}

/**
* Remove a module.
*/
public Module RemoveModule(String blockId)
{
    for(int i = 0; i < Modules.Count; i++) {
        Module module = Modules[i];
        if (module.ToString() == blockId) {
            Modules.Remove(module);
            return module;
        }
    }
    return null;
}

/**
* Run registered modules.
*/
public void InvokeModules()
{
    string count = GetConfig("RunCount").Value;
    foreach(Module i in Modules.ToArray()) {
        i.Block.TryRun("API://ScheduleEvent/" + GetMyId() + "/" + count);
    }  
}

/**
* Get specific block.
* <param name="name">Name of block.</param>
* <param name="gridNumber">Number inside of grid.</param>
*/
public IMyTerminalBlock GetBlock(string name, int gridNumber)
{
    if (Blocks.Count == 0) {
        // Load blocks
        GridTerminalSystem.GetBlocks(Blocks);
    }
    
    for(int i = 0; i < Blocks.Count; i++) {
        IMyTerminalBlock block = Blocks[i];
        if (block.NumberInGrid == gridNumber && block.CustomName == name) {
            return block;
        }
    }
    
    return null;
}

/**
* Get specific block.
* <param name="blockId">Id of block.</param>
*/
public IMyTerminalBlock GetBlock(string blockId)
{
    string[] parts = blockId.Split('|');
    return GetBlock(parts[1].Trim(),  Int32.Parse(parts[0].Trim()));
}

/**
* Generate my id.
*/
public string GetMyId()
{
    return Me.NumberInGrid.ToString() + "|" + Me.CustomName;
}