namespace ReBuzzTests;

public class Tests
{
    [Test]
    public void ReadsGearFilesOnCreation([Values(1, 2)] int x)
    {
        using var driver = new Driver();

        driver.Start();

        driver.AssertRequiredPropertiesAreInitialized();  //bug what to do with this one? Maybe add to the initial state assertion?
        driver.AssertGearMachinesConsistOf([ //bug what to do with this one? Maybe add to the initial state assertion?
            "Jeskola Pianoroll",
            "Modern Pattern Editor",
            "Jeskola Pattern XP",
            "Jeskola Pattern XP mod",
            "Modern Pianoroll",
            "Polac VST 1.1",
            "Polac VSTi 1.1",
            "Jeskola XS-1",
            "CyanPhase Buzz OverLoader",
            "CyanPhase DX Instrument Adapter",
            "CyanPhase DX Effect Adapter",
            "CyanPhase DMO Effect Adapter",
            "11-MidiCCout",
            "Rymix*",
            "FireSledge ParamEQ",
            "BTDSys Pulsar"
        ]);
        
        driver.AssertInitialStateAfterAppStart();

        driver.NewFile();

        driver.AssertInitialStateAfterNewFile();
    }
}