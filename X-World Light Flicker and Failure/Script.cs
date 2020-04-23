/*
The X-World Light Flicker and Failure Script.

Makes the light on the grid randomly flickering.

Only flicker light can failure. After turn on they flickering continues before it goes stable.

*/
const String VERSION = "1.1.0";


float FlickerInterval = 0.03f;    // seconds
float FlickerLenght = 10f;        // 1f == 1%

// Chance of 1/x
int ChanceToSkip = 3;          
int ChanceToAddFlicker = 8;
int ChanceToRemoveFlicker = 5;
int ChanceToTurnOff = 3;
int ChanceTurnOn = 6;

int MinFlickerLights = 0;
int MinFlickerLigthsToTurnOff = 2;
int MinOfflineLights = 0;

List<IMyLightingBlock> Lights = new List<IMyLightingBlock>();
List<IMyLightingBlock> FlickerLights = new List<IMyLightingBlock>();
List<IMyLightingBlock> OffLights = new List<IMyLightingBlock>();
Random Random = new Random();

IMyTextSurface ComputerDisplay;

public Program()
{

    List<IMyLightingBlock> lights = new List<IMyLightingBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights);

    foreach (IMyLightingBlock light in lights) {
        if (light.CubeGrid != Me.CubeGrid) continue;
        Lights.Add(light);
        light.BlinkLength = 100f - FlickerLenght;
        light.BlinkIntervalSeconds = 0f;
        light.Enabled = true;
        light.BlinkOffset = (float)Random.NextDouble() * 100f;
    }

    ComputerDisplay = Me.GetSurface(0);
    ComputerDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
    ComputerDisplay.ClearImagesFromSelection();
    ComputerDisplay.ChangeInterval = 0;
    WriteText("Initialized.", false);

    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

public void WriteText(string text, bool append = true) {
    ComputerDisplay.WriteText(text +"\n", append);
    Echo(text);
}

public void Save()
{
}

public void Main(string argument, UpdateType updateSource)
{
    int exitRandom = Random.Next(0, ChanceToSkip);
    int addRandom = Random.Next(0, ChanceToAddFlicker);
    int removeRandom = Random.Next(0, ChanceToRemoveFlicker);
    int offRandom = Random.Next(0, ChanceToTurnOff);
    int onRandom = Random.Next(0, ChanceTurnOn);

    WriteText("Found lights: " + Lights.Count, false);
    WriteText("Flicker lights: " + FlickerLights.Count);
    WriteText("Offline lights: " + OffLights.Count);
    WriteText("Skip: " + (exitRandom != 0 ? "Yes" : "No"));
    WriteText("Add: " + (addRandom == 0 ? "Yes" : "No"));
    WriteText("Remove: " + (removeRandom == 0 ? "Yes" : "No"));
    WriteText("Off: " + (offRandom == 0 ? "Yes" : "No"));
    WriteText("On: " + (onRandom == 0 ? "Yes" : "No"));

    if (exitRandom != 0) return;


    if(onRandom == 0 && OffLights.Count() > MinOfflineLights) {
        int index = Random.Next(0, OffLights.Count);
        WriteText("Turn on light #" + index);
        IMyLightingBlock light = OffLights[index];
        OffLights.Remove(light);
        FlickerLights.Add(light);
        light.Enabled = true;
    }

    if(offRandom == 0 && FlickerLights.Count() > MinFlickerLigthsToTurnOff) {
        int index = Random.Next(0, FlickerLights.Count);
        WriteText("Turn off light #" + index);
        IMyLightingBlock light = FlickerLights[index];
        FlickerLights.Remove(light);
        OffLights.Add(light);
        light.Enabled = false;
    }

    if(removeRandom == 0 && FlickerLights.Count() > MinFlickerLights) {
        int index = Random.Next(0, FlickerLights.Count);
        WriteText("Remove light #" + index);
        IMyLightingBlock light = FlickerLights[index];
        FlickerLights.Remove(light);
        light.BlinkIntervalSeconds = 0f;
    }

    if (addRandom == 0) {
        int index = Random.Next(0, Lights.Count);
        WriteText("Add light #" + index);
        IMyLightingBlock light = Lights[index];
        FlickerLights.Add(light);
        light.BlinkIntervalSeconds = FlickerInterval;
        light.BlinkOffset = (float)Random.NextDouble() * 100f;
    } 
}
