const String VERSION = "1.0.1";
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
        return Block.NumberInGrid.ToString() + "|" + Block.BlockDefinition.SubtypeId;
    } 
}
// Registered modules.
List<Module> Modules = new List<Module>();

// The central configuration.
List<ConfigValue> Config = new List<ConfigValue>(); 

// Block Buffer
List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();

public class Call {
    public IMyProgrammableBlock Block;
    public String Argument;
    
    public Call(IMyProgrammableBlock block, String argument) {
        Block = block;
        Argument = argument;
    }
    
    public bool Run()
    {
        return Block.TryRun(Argument);
    }
}
List<Call> CallStack = new List<Call>();
 
String lastArg = "";
/**
* Program logic.
*/
public void Main(string argument)
{
    //Echo("> " + lastArg);
    //Echo("> " + argument);
    lastArg = argument;
    
    // clear buffer
    Blocks.Clear();
    
    LoadConfigFromConfigLCD();
    StopTimer();
    if(Modules.Count == 0) LoadModules();
    
    InvokeCalls();
    
    ReadArgument(argument); 
    UpdateModulesConfig();
    
    CountRun(); 
    
    OutputToConsole();
    OutputToConfigLcd();
    OutputDebugToConfigLcd();
    
    InvokeModules();
    
    StartTimer();
} 
 
/**
* Load storage into config memory.
*/
public Program()
{ 
    if (Storage.Length > 0) { 
        Config.Clear(); 
        String[] configs = Storage.Split('\n'); 
        
        if(configs[0] != "FORMAT v" + DATA_FORMAT) return;
        
        for(int i = 1; i < configs.Length; i++) {
            String data = configs[i]; 
            if (data.Length > 0) Config.Add(new ConfigValue(data)); 
        } 
        StartTimer();
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
    IMyTerminalBlock block;
    String[] parts = args.Split(','); 
    if (parts.Length >= 1 && parts[0].Length > 0) {
        block = GetBlockByName(parts[0].Trim());
        if(block != null) GetConfig("ConfigLCD").Value = GetId(block);
    } 
    if (parts.Length >= 2 && parts[1].Length > 0) {
        block = GetBlockByName(parts[1].Trim());
        if(block != null) GetConfig("MainTimer").Value = GetId(block);
    } 
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
    IMyTimerBlock timer = GetBlock(GetConfig("MainTimer").Value) as IMyTimerBlock; 
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

    IMyTimerBlock timer = GetBlock(GetConfig("MainTimer").Value) as IMyTimerBlock; 
    if(timer != null) {
        timer.ApplyAction("Stop");
    } 
} 

/**
* Stores the config memory on the config lcd.
*/
public void OutputToConfigLcd()
{
    IMyTextPanel lcd = GetBlock(GetConfig("ConfigLCD").Value) as IMyTextPanel; 
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
    IMyTextPanel lcd = GetBlock(GetConfig("ConfigLCD").Value) as IMyTextPanel; 
    if(lcd == null) {
        return;
    }
    
    string output = "[MBOS]"
        + " [" + System.DateTime.Now.ToLongTimeString() + "]" 
        + " [" + GetConfig("RunCount").Value + "]" 
        + "\n\n"
    ;
    
    output += "[Core v" + VERSION + "] " 
        + "Registered Modules:\n";
    foreach(Module mod in Modules) {
        output += "    " + mod.ToString() + "\n";
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
    string lcd = GetConfig("ConfigLCD").Value; 

    Echo(
        "MODULE=Core\n"
        + "VERSION=" + VERSION + "\n"
        + "ID=" + GetMyId() + "\n"
        + "Count=" + GetConfig("RunCount").Value + "\n"
        + (lcd != String.Empty ? "ConfigLCD=" + lcd + "\n" : "")
        + "RegisteredModules=" + Modules.Count + "\n"
    );
}

 
/**
* Load storage into config memory.
*/
public void LoadConfigFromConfigLCD()
{ 
    IMyTextPanel lcd = GetBlock(GetConfig("ConfigLCD").Value) as IMyTextPanel; 
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
    foreach(Module module in Modules.ToArray()) modules.Add(module.ToString());
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
    IMyTerminalBlock block;
    string lcd = GetConfig("ConfigLCD").Value;
    
    switch(stack[0]) {
        case "RegisterModule":
            receiver = RegisterModule(stack[1]);
            if (receiver != null) {
                CallStack.Add(
                    new Call(
                        receiver.Block, 
                        "API://Registered/" + GetMyId() + "/"
                        + lcd
                    )
                );
            }
            break;
        case "RemoveModule":
            receiver = RemoveModule(stack[1]);
            if (receiver != null) {
                CallStack.Add(
                    new Call(receiver.Block, "API://Removed/" + GetMyId())
                );
            }
            break;
        case "GetConfigLCD":
            block = GetBlock(stack[1]);
            if (block is IMyProgrammableBlock && lcd != String.Empty) {
                CallStack.Add(
                    new Call(
                        (IMyProgrammableBlock) block
                        , "API://ConfigLCD/" + lcd + "/" + GetMyId()
                    )
                );
            }
            break;
        default:
            Echo("Unknown request: " + apiInput);
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
    foreach(Module i in Modules) if(i.ToString() == blockId) return i;
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
* Run calls from stack.
*/
public void InvokeCalls()
{
    Call[] calls = CallStack.ToArray();
    CallStack.Clear();
    foreach(Call call in calls) 
    {
        call.Run();
    } 
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
* Get specific block.
* <param name="id">The block identifier.</param>
*/
public IMyTerminalBlock GetBlock(string id)
{
    string[] parts = id.Split('|');
    if (parts.Length != 2) return null;
    string subTypeId = parts[1].Trim();
    int gridNumber = Int32.Parse(parts[0].Trim());
    
    List<IMyTerminalBlock> blocks = Blocks;
    if (blocks.Count == 0) GridTerminalSystem.GetBlocks(blocks);
    
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
* Generate my id.
*/
public string GetMyId()
{
    return GetId(Me);
}

/**
* Generate id.
*/
public string GetId(IMyTerminalBlock block)
{
    return block.NumberInGrid.ToString() + "|" + block.BlockDefinition.SubtypeId;
}