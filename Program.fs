open System
open System.Threading
open Meadow.Devices
open Meadow
open Meadow.Foundation
open Meadow.Foundation.Leds
open Meadow.Foundation.Sensors.Atmospheric
open Meadow.Foundation.Graphics
open Meadow.Foundation.Displays.TftSpi

type MeadowApp() =
    inherit App<F7Micro, MeadowApp>()

    let led = RgbPwmLed(MeadowApp.Device, MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen,
                      MeadowApp.Device.Pins.OnboardLedBlue, 3.3f, 3.3f, 3.3f, Peripherals.Leds.IRgbLed.CommonType.CommonAnode)

    let i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)
    let sensor = new Ccs811 (i2c)

    let triggerThreshold = Nullable (Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion))
    let reductionThreshold = Nullable (Units.Concentration(650.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable latestCO2Value =  Nullable (Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion))

    let display = new Gc9a01 (MeadowApp.Device, MeadowApp.Device.CreateSpiBus(48000L), 
                        MeadowApp.Device.Pins.D02, MeadowApp.Device.Pins.D01, MeadowApp.Device.Pins.D00)

    let mutable graphics = GraphicsLibrary(display)

    let updateDisplay newValue = 
        async {
            graphics.Clear(true)
            graphics.CurrentFont <- Font12x16()
            graphics.DrawText(120, 92, $"{newValue}", Color.White, GraphicsLibrary.ScaleFactor.X4, GraphicsLibrary.TextAlignment.Center)
            graphics.Show()
        }

    let relayGreen = Relays.Relay(MeadowApp.Device, MeadowApp.Device.Pins.D05)
    let mutable ventilationIsOn : bool = false

    let toggleRelay duration =
        async {
            printfn $"Ventilator ON..."
            while latestCO2Value.Value.PartsPerMillion > reductionThreshold.Value.PartsPerMillion do
                ventilationIsOn <- true
                if relayGreen.IsOn = false then 
                    relayGreen.Toggle()
                Thread.Sleep(int (duration))
            ventilationIsOn <- false
            relayGreen.Toggle()
            printfn $"Ventilator OFF..."
        }

    let consumer = Ccs811.CreateObserver(fun result -> 
        let newValue = match result.New with | (new_val, _) -> new_val
        latestCO2Value <- newValue
        printfn $"New CO2 value: {latestCO2Value}"
        updateDisplay newValue |> Async.StartAsTask |> ignore
        if latestCO2Value.Value.PartsPerMillion > triggerThreshold.Value.PartsPerMillion && ventilationIsOn = false then 
            do toggleRelay 3000 |> Async.StartAsTask |> ignore)

    do sensor.StartUpdating(TimeSpan.FromSeconds(2.0))
    let s = sensor.Subscribe(consumer)

[<EntryPoint>]
let main argv =
    Console.WriteLine "Starting main..."
    let app = MeadowApp()
    Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
    0 // return an integer exit code