/*
The X-World Solar Power Status Script.

*/
const String VERSION = "1.0.0";

IMyTextSurface ComputerDisplay;
IMyTextSurface StatusPanel;
List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
List<IMyLightingBlock> StatusLights = new List<IMyLightingBlock>();

public Program()
{

    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
    foreach (IMyBatteryBlock battery in batteries) {
        if (battery.CubeGrid != Me.CubeGrid) continue;
        Batteries.Add(battery);
    }

    List<IMyLightingBlock> lights = new List<IMyLightingBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights);
    foreach (IMyLightingBlock light in lights) {
        if (light.CubeGrid != Me.CubeGrid /*|| light.CustomName.IndexOf("Spot") == -1*/) continue;
        StatusLights.Add(light);
    }

    StatusPanel = GridTerminalSystem.GetBlockWithName("Status Panel") as IMyTextSurface;

    ComputerDisplay = Me.GetSurface(0);
    ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
    ComputerDisplay.ClearImagesFromSelection();
    ComputerDisplay.ChangeInterval = 0;
    ComputerDisplay.WriteText("Initialized.", false);

    StatusPanel.ContentType = ContentType.TEXT_AND_IMAGE;
    StatusPanel.ClearImagesFromSelection();
    StatusPanel.ChangeInterval = 0;
    StatusPanel.WriteText("Initialized.", false);
    
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

public void Save()
{
}

public void Main(string argument, UpdateType updateSource)
{
    float charge = 0f;
    float max = 0f;
    float current = 0f;
    float input = 0f;
    float output = 0f;
    float charging = 0f;

    foreach (IMyBatteryBlock battery in Batteries) {
        max += battery.MaxStoredPower;
        current += battery.CurrentStoredPower;
        input += battery.CurrentInput;
        output += battery.CurrentOutput;
    }

    charge = (float)Math.Round(100f / max * current, 2);
    charging = input - output;

    if (charging > 0f) {
        SetColor(Color.LawnGreen);
        SetOn();
    } else if (charge < 25f && charging < 0f) {
        SetColor(Color.IndianRed);
        SetOnOff();
    } else {
        SetColor(Color.LightGoldenrodYellow);
        SetOn();
    }

    string powerExtenstion = "M";
    string mode = charging > 0f ? "charging" : "depleting";
    charging = charging < 0f ? charging * -1f : charging;
    if (charging < 0.000025f) {
        charging = charging * 1000000f;
        powerExtenstion = "";
    } else if (charging < 0.25f) {
        charging = charging * 1000f;
        powerExtenstion = "k";
    } 
    charging = (float)Math.Round(charging, 2);

    WriteText("Battery " + mode + " by " + charge + "%.\n", false);
    WriteText("\nPower usage: " + charging + powerExtenstion + "W\n", true);
    WriteText("Stored energy: " + (float)Math.Round(current, 2) + "MW\n", true);
}

public void WriteText(string text, bool append = true) {
    ComputerDisplay.WriteText(text, append);
    StatusPanel.WriteText(text, append);
}

public void SetColor(Color color) {
    foreach (IMyLightingBlock light in StatusLights) {
        light.SetValue<Color>("Color", color);
    }
}
public void SetOnOff() {
    int now = (int)(System.DateTime.Now.ToBinary() / 10000000L);
    foreach (IMyLightingBlock light in StatusLights) {
        light.Enabled = (now % 3) != 0;
    }
}
public void SetOn() {
    foreach (IMyLightingBlock light in StatusLights) {
        light.Enabled = true;
    }
}

public void SetBatteryMode(ChargeMode mode) 
{
    foreach (IMyBatteryBlock battery in Batteries) {
        battery.ChargeMode = mode;
    }
}