open System
open System.Threading
open Meadow.Devices
open Meadow
open Meadow.Foundation
open Meadow.Foundation.Leds
open Meadow.Foundation.Sensors.Atmospheric


type MeadowApp() =
    inherit App<F7Micro, MeadowApp>()

    // boilerplate LED stuff
    let led =
        RgbPwmLed(MeadowApp.Device, MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen,
                      MeadowApp.Device.Pins.OnboardLedBlue, 3.3f, 3.3f, 3.3f,
                      Peripherals.Leds.IRgbLed.CommonType.CommonAnode)
    do led.Stop |> ignore

    // set up sensor
    let i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)
    let sensor = new Ccs811 (i2c)

    let warningThreshold = Nullable (Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion))
    
    let mutable latestCO2Value =  Nullable (Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion))

    let mutable onboardLEDColor = 
        match latestCO2Value.Value.PartsPerMillion with  
        | i when i > 1500.0 -> Color.Red
        | i when i > 750.0 && i <= 1500.0 -> Color.OrangeRed
        | i when i > 650.0 && i <= 750.0 -> Color.Yellow
        | _ -> Color.Green
    
    // set up ventilation
    let relayGreen = Relays.Relay(MeadowApp.Device, MeadowApp.Device.Pins.D05)
    let mutable ventilationIsOn : bool = false

    let toggleRelays duration =
        async {
            while latestCO2Value.Value.PartsPerMillion > warningThreshold.Value.PartsPerMillion do
                ventilationIsOn <- true
                led.SetColor(onboardLEDColor)
                relayGreen.Toggle()
                Thread.Sleep(int (duration))
                relayGreen.Toggle()
                Thread.Sleep(int (duration))
            ventilationIsOn <- false
            printfn $"Ventilator OFF..."
        }

    do sensor.StartUpdating(TimeSpan.FromSeconds(2.0))

    let consumer = Ccs811.CreateObserver(fun result -> 
        let newValue = match result.New with | (new_val, _) -> new_val
        latestCO2Value <- newValue
        printfn $"New CO2 value: {latestCO2Value}"
        led.SetColor(onboardLEDColor, 100f)
        if latestCO2Value.Value.PartsPerMillion > warningThreshold.Value.PartsPerMillion && ventilationIsOn = false then 
            do toggleRelays 2000 |> Async.StartAsTask |> ignore)

    let s = sensor.Subscribe(consumer)

[<EntryPoint>]
let main argv =
    Console.WriteLine "Starting main..."
    let app = MeadowApp()
    Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
    0 // return an integer exit code