﻿const String VERSION = "2.3.0";
const String DATA_FORMAT = "1.1";

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

public class Module {
    public IMyProgrammableBlock Block;
    
    public Module(IMyProgrammableBlock block) {
        Block = block;
    }

    public override String ToString() 
    { 
        return Block.NumberInGrid.ToString() + "|" + Block.BlockDefinition.SubtypeId;
    }

    public String GetName() 
    { 
        return Block.CustomName;
    } 
}

List<Module> Modules = new List<Module>();
List<ConfigValue> Config = new List<ConfigValue>();
List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
List<IMyProgrammableBlock> LastCalled = new List<IMyProgrammableBlock>();
IMyTextSurface ComputerDisplay;

public class Call {
    public IMyProgrammableBlock Block;
    public String Argument;
    
    public Call(IMyProgrammableBlock block, String argument) {
        Block = block;
        Argument = argument;
    }

    public String GetId() 
    { 
        return Block.NumberInGrid.ToString() + "|" + Block.BlockDefinition.SubtypeId;
    }
}
 
/**
* Program logic.
*/
public void Main(string argument)
{
    //Echo(argument);
    
    // clear buffer
    Blocks.Clear();
    LastCalled.Clear();
    
    LoadFromCustomData();
    if(Modules.Count == 0) LoadModules();
    
    InvokeCalls();

    if (argument == "UNINSTALL") {
        foreach(Module module in Modules) {
            AddCall(GetId(Me), "API://RemoveModule/"+module.ToString());
        }
    }  else if (argument == "clean") {
        GetConfig("CallStack").Value = String.Empty;
    } else {
        ReadArgument(argument);
    } 
    UpdateModulesConfig();
    
    CountRun(); 
    
    OutputToConsole();
    StoreToCustomData();
    OutputDebug();
    
    InvokeModules();
    
    StartTimer();
} 
 
/**
* Load storage into config memory.
*/
public Program()
{ 
    ComputerDisplay = Me.GetSurface(0);
    ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
    ComputerDisplay.ClearImagesFromSelection();
    ComputerDisplay.ChangeInterval = 0;
    ComputerDisplay.WriteText("Initialized.", false);

    Config.Clear(); 
    LoadFromCustomData();
    StartTimer(); 
    OutputToConsole();
} 
 
/**
* Store config memory.
*/
public void Save() { 
    Me.CustomData = FormatConfig(Config); 
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

public ConfigValue GetConfig(String key) {
    ConfigValue config = Config.Find(x => x.Key == key);
    if(config != null) return config;
     
    ConfigValue newValue = new ConfigValue(key, String.Empty); 
    Config.Add(newValue); 
    return newValue; 
} 

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
        if(block != null) GetConfig("Display").Value = GetId(block);
    }
}

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

public void StartTimer()
{
    ConfigValue runMode = GetConfig("RunMode");
    if (runMode.Value == String.Empty) runMode.Value = "normal";
    
    switch(runMode.Value) {
        case "fast":
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            break;
        case "call":
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            break;
        case "callFast":
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            break;
        default:
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            break;
    }
} 

public void StoreToCustomData()
{
    List<ConfigValue> configs = new List<ConfigValue>();
    foreach(ConfigValue i in Config) {
        if (i.Key != "RunCount")
            configs.Add(i);
    }
    
    Me.CustomData = FormatConfig(configs);
}

public void OutputDebug()
{
    IMyTextPanel lcd = GetBlock(GetConfig("Display").Value) as IMyTextPanel; 
    
    string output = "[MBOS]"
        + " [" + System.DateTime.Now.ToLongTimeString() + "]" 
        + " [" + GetConfig("RunCount").Value + "]" 
        + "\n\n"
    ;
    
    output += "[Core v" + VERSION + "] " 
        + "Registered Modules:\n";
    foreach(Module mod in Modules) {
        output += "    " + mod.GetName() + "\n";
    }
    output += "\n";
    
    if(lcd != null) {
        lcd.ContentType = ContentType.TEXT_AND_IMAGE;
        lcd.ClearImagesFromSelection();
        lcd.ChangeInterval = 0;
        lcd.WriteText(output, false);
    }
    ComputerDisplay.WriteText(output, false);
}

public void OutputToConsole()
{
    string lcd = GetConfig("Display").Value; 

    Echo(
        "MODULE=Core\n"
        + "VERSION=" + VERSION + "\n"
        + "ID=" + GetMyId() + "\n"
        + "Count=" + GetConfig("RunCount").Value + "\n"
        + (lcd != String.Empty ? "Display=" + lcd + "\n" : "")
        + "RegisteredModules=" + Modules.Count + "\n"
    );
}

public void LoadFromCustomData()
{ 
    string data = Me.CustomData;
    
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

public void ApplyAPICommunication(String apiInput)
{
    string[] stack = apiInput.Replace("API://", "").Split('/');
    Module receiver = null;
    
    switch(stack[0]) {
        case "RegisterModule":
            receiver = RegisterModule(stack[1]);
            if (receiver != null) {
                AddCall(
                    receiver.ToString() 
                    , "API://Registered/" + GetMyId()
                );
            } else {
                Echo("Error: Block " + stack[1] + " is invalid.");
            }
            break;
        case "RemoveModule":
            receiver = RemoveModule(stack[1]);
            if (receiver != null) {
                AddCall(
                    receiver.ToString()
                    , "API://Removed/" + GetMyId()
                );
            }
            break;
        default:
            Echo("Unknown request: " + apiInput);
            break;
    }
}

public Module RegisterModule(String blockId)
{
    IMyTerminalBlock block = GetBlock(blockId);
    if(block == null || !(block is IMyProgrammableBlock)) return null;
    foreach(Module i in Modules) if(i.ToString() == blockId) return i;
    Module module = new Module((IMyProgrammableBlock)block);
    Modules.Add(module);
    
    return module;
}

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

public List<Call> BuildCallStaskFromConfig() {
    List<Call> CallStack = new List<Call>();
    
    String configValue = GetConfig("CallStack").Value;
    if (configValue == String.Empty) return CallStack;
    String[] calls = configValue.Split('#');
    foreach(String callData in calls) {
        String[] callParts = callData.Split('~');
        IMyProgrammableBlock block = GetBlock(callParts[0]) as IMyProgrammableBlock;
        if(block == null) continue; // block is not anymore in grid
        CallStack.Add(new Call(block, callParts[1]));
    }

    return CallStack;
}

public void InvokeCalls()
{
    List<Call> CallStack = BuildCallStaskFromConfig();

    if (CallStack.Count == 0) return;

    GetConfig("CallStack").Value = String.Empty;
    StoreToCustomData();

    Call call = CallStack[0];
    if (call.Block == Me) {
        //Echo("Run Me with '" + call.Argument + "'");
        // I can't call my self ;) ... so call the core direct.
        ReadArgument(call.Argument);
    } else {
        //Echo("Run " + call.GetId() + " with '" + call.Argument + "'");
        if (call.Block.TryRun(call.Argument)) {
            LastCalled.Add(call.Block);
            CallStack.Remove(call);
        }

        // Append new calls of other blocks
        LoadFromCustomData();
    }
    // Append calls created by Me or other blocks
    List<Call> newCalls = BuildCallStaskFromConfig();
    foreach(Call nCall in newCalls) CallStack.Add(nCall);

    // Update config
    List<String> callList = new List<String>();
    foreach(Call call4String in CallStack) {
        callList.Add(call4String.GetId()+"~"+call4String.Argument);
    }
    GetConfig("CallStack").Value = String.Join("#", callList);
    Echo("CallStack=" + callList.Count);
}

public void InvokeModules()
{
    string count = GetConfig("RunCount").Value;
    foreach(Module i in Modules.ToArray()) {
        if (!LastCalled.Exists(x => x == i.Block)) {
            i.Block.TryRun("API://ScheduleEvent/" + GetMyId() + "/" + count);
        }
    }  
}

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

public string GetMyId()
{
    return GetId(Me);
}

public string GetId(IMyTerminalBlock block)
{
    return block.NumberInGrid.ToString() + "|" + block.BlockDefinition.SubtypeId;
}

public void AddCall(String blockId, String argument)
{
    ConfigValue config = GetConfig("CallStack");
    
    if(config.Value == String.Empty) {
        config.Value = blockId + "~" + argument;
    } else {
        config.Value += "#" + blockId + "~" + argument;
    }
}
