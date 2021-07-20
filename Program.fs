open System
open System.Threading
open Meadow.Devices
open Meadow
open Meadow.Foundation
open Meadow.Foundation.Leds
open Meadow.Foundation.Sensors.Atmospheric


type MeadowApp() =
    inherit App<F7Micro, MeadowApp>()

    // set up sensor
    let i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)
    let sensor = new Ccs811 (i2c)

    // set up thresholds to compare against observer readings
    let warningThreshold = Nullable (Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion))
    let dangerThreshold = Nullable (Units.Concentration(1500.0, Units.Concentration.UnitType.PartsPerMillion))
    
    let mutable latestCO2Value = System.Nullable()

    let consumer = Ccs811.CreateObserver(fun result -> 
        let newValue = match result.New with | (new_val, _) -> new_val
        latestCO2Value <- newValue
        printfn $"New CO2 value: {latestCO2Value}")

    let s = sensor.Subscribe(consumer)

    do sensor.StartUpdating(TimeSpan.FromSeconds(2.0))

    // boilerplate LED stuff
    let led =
        RgbPwmLed(MeadowApp.Device, MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen,
                      MeadowApp.Device.Pins.OnboardLedBlue, 3.3f, 3.3f, 3.3f,
                      Peripherals.Leds.IRgbLed.CommonType.CommonAnode)


    //let showColorBlinks color duration =
    //    led.StartBlink(color, (duration / 2), (duration / 2), 0.75f, 0.0f) |> ignore
    //    Threading.Thread.Sleep(int duration) |> ignore
    //    led.Stop |> ignore

    // set up relays
    let relayGreen = Relays.Relay(MeadowApp.Device, MeadowApp.Device.Pins.D05)

    let toggleRelays duration =
        while latestCO2Value.Value.PartsPerMillion > warningThreshold.Value.PartsPerMillion do 
            //Console.WriteLine $"latest CO2 Value: {latestCO2Value.Value.PartsPerMillion}, warning theshold {warningThreshold.Value.PartsPerMillion}"
            relayGreen.Toggle()
            Thread.Sleep(int (duration / 2))
            relayGreen.Toggle()
            Thread.Sleep(int (duration / 2))

    // TODO: put this on an event
    do Thread.Sleep(5000)
    do Console.WriteLine $"latest CO2 Value: {latestCO2Value.Value.PartsPerMillion}, warning theshold {warningThreshold.Value.PartsPerMillion}"
    do toggleRelays 2000
    do Thread.Sleep(10000)
    do Console.WriteLine $"latest CO2 Value: {latestCO2Value.Value.PartsPerMillion}, warning theshold {warningThreshold.Value.PartsPerMillion}"
    do toggleRelays 2000

    //let cycleColors  (firstColor: Color) duration =
    //    while true do
    //        showColorBlinks firstColor duration

    //do cycleColors Color.Blue 2000

[<EntryPoint>]
let main argv =
    Console.WriteLine "Starting main..."
    let app = MeadowApp()
    Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
    0 // return an integer exit code