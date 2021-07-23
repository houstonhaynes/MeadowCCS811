open System
open System.Threading
open Meadow
open Meadow.Devices
open Meadow.Foundation
open Meadow.Foundation.Sensors.Atmospheric
open Meadow.Foundation.Graphics
open Meadow.Foundation.Displays.TftSpi

type MeadowApp() =
    inherit App<F7Micro, MeadowApp>()

    let i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)
    let sensor = new Ccs811 (i2c)

    let triggerThreshold = Nullable (Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion))
    let reductionThreshold = Nullable (Units.Concentration(650.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable latestCO2Value =  Nullable (Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion))

    let mutable display = new Gc9a01 (MeadowApp.Device, MeadowApp.Device.CreateSpiBus(48000L), 
                            MeadowApp.Device.Pins.D02, MeadowApp.Device.Pins.D01, MeadowApp.Device.Pins.D00)
    let mutable displayColor : Color = Color.DarkGreen.WithBrightness(25.0)

    let mutable graphics = GraphicsLibrary(display)

    let updateDisplay newValue = 
        async {
            graphics.Clear(true)
            graphics.CurrentFont <- Font12x20()
            graphics.DrawText(120, 92, $"{newValue}", displayColor.WithBrightness(25.0), 
                GraphicsLibrary.ScaleFactor.X4, GraphicsLibrary.TextAlignment.Center)
            graphics.Show()
        }

    let mutable relayOne = Relays.Relay(MeadowApp.Device, MeadowApp.Device.Pins.D05)
    let mutable ventilationIsOn = false

    let toggleRelay duration =
        async {
            printfn "Ventilator ON..."
            while latestCO2Value.Value.PartsPerMillion > reductionThreshold.Value.PartsPerMillion do
                ventilationIsOn <- true
                if relayOne.IsOn = false then 
                    relayOne.Toggle()
                Thread.Sleep(int (duration))
            ventilationIsOn <- false
            relayOne.Toggle()
            printfn "Ventilator OFF..." |> ignore
        }

    let consumer = Ccs811.CreateObserver(fun result -> 
        let mutable newValue = match result.New with | (co2, _) -> co2
        latestCO2Value <- newValue
        printfn $"New CO2 value: {latestCO2Value}" |> ignore
        displayColor <- match newValue.Value.PartsPerMillion with
                        | i when i >= 2000.0 -> Color.DarkRed
                        | i when i >= 1000.0 && i < 2000.0 -> Color.DarkOrange
                        | i when i >= 650.0 && i < 1000.0 -> Color.BurlyWood
                        | _ -> Color.DeepSkyBlue
        updateDisplay newValue |> Async.StartAsTask |> ignore
        if newValue.Value.PartsPerMillion > triggerThreshold.Value.PartsPerMillion && ventilationIsOn = false then 
            do toggleRelay 3000 |> Async.StartAsTask |> ignore)

    do sensor.StartUpdating(TimeSpan.FromSeconds(2.0))
    let mutable s = sensor.Subscribe(consumer)

[<EntryPoint>]
let main argv =
    Console.WriteLine "Starting main..."
    let app = MeadowApp()
    Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
    0 // return an integer exit code