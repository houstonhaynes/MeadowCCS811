open System
open Meadow
open Meadow.Devices
open Meadow.Foundation
open Meadow.Foundation.Sensors.Atmospheric
open Meadow.Foundation.Graphics
open Meadow.Foundation.Displays.TftSpi
open Meadow.Foundation.Leds

type MeadowApp() =
    inherit App<F7Micro, MeadowApp>()

    let mutable i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)
    let mutable sensor = new Ccs811 (i2c)

    let led = RgbPwmLed(MeadowApp.Device, MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen,
                    MeadowApp.Device.Pins.OnboardLedBlue, 3.3f, 3.3f, 3.3f, Peripherals.Leds.IRgbLed.CommonType.CommonAnode)
    let mutable onboardLEDColor : Color = Color.Green

    let ventThreshold = Nullable (Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion))
    let clearThreshold = Nullable (Units.Concentration(650.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable latestCO2Value = Nullable (Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable previousCO2Value = Nullable (Units.Concentration(0.0, Units.Concentration.UnitType.PartsPerMillion))

    let mutable display = new Gc9a01 (MeadowApp.Device, MeadowApp.Device.CreateSpiBus(48000L), 
                            MeadowApp.Device.Pins.D02, MeadowApp.Device.Pins.D01, MeadowApp.Device.Pins.D00)

    let displaywidth = Convert.ToInt32(display.Width)
    let displayheight = Convert.ToInt32(display.Height)
    let originx = displaywidth / 2
    let originy = displayheight / 2

    let mutable displayColor : Color = Color.White.WithBrightness(25.0)
    let mutable graphics = GraphicsLibrary(display)
    let updateDisplay (firstcolor: Color) (secondcolor: Color) newValue = 
        async {
            graphics.Clear(false)
            graphics.CurrentFont <- Font12x16()
            graphics.DrawCircle(originx, originy, 120, firstcolor, true)
            graphics.DrawCircle(originx, originy, 105, Color.Black, true)
            graphics.DrawCircle(originx, originy, 100, secondcolor, true)
            graphics.DrawRoundedRectangle(32, 98, 175, 44, 8, Color.Black, true)
            graphics.DrawText(120, 98, $"{newValue}", displayColor.WithBrightness(1.0), 
                GraphicsLibrary.ScaleFactor.X3, GraphicsLibrary.TextAlignment.Center)
            graphics.Show()
        }

    let mutable relayOne = Relays.Relay(MeadowApp.Device, MeadowApp.Device.Pins.D05)
    let mutable ventilationIsOn = false

    let toggleVent duration =
        async {
            printfn "Ventilator ON..."
            while latestCO2Value.Value.PartsPerMillion > clearThreshold.Value.PartsPerMillion do
                ventilationIsOn <- true
                led.SetColor(onboardLEDColor, 0.25f)
                if not relayOne.IsOn then 
                    relayOne.Toggle()
                do! Async.Sleep(int (duration))
            ventilationIsOn <- false
            led.SetColor(onboardLEDColor, 0f) |> ignore
            relayOne.Toggle()
            printfn "Ventilator OFF..." |> ignore
        }

    let consumer = Ccs811.CreateObserver(fun result ->
        let newValue = match result.New with | (co2, _) -> co2
        latestCO2Value <- newValue
        let oldValue = match result.Old.Value with | (co2 , _) -> co2
        if oldValue.HasValue then
            previousCO2Value <- oldValue    
        displayColor <- match newValue.Value.PartsPerMillion with
                        | i when i >= 2000.0 -> Color.DarkRed
                        | i when i >= 1000.0 && i < 2000.0 -> Color.DarkOrange
                        | i when i >= 650.0 && i < 1000.0 -> Color.BurlyWood
                        | _ -> Color.DeepSkyBlue
        onboardLEDColor <- match newValue.Value.PartsPerMillion with
                            | i when i >= 2000.0 -> Color.Orange
                            | i when i >= 750.0 && i < 2000.0 -> Color.Yellow
                            | _ -> Color.White
        if previousCO2Value.Value.PartsPerMillion <> latestCO2Value.Value.PartsPerMillion then
            updateDisplay Color.Orange Color.Blue newValue |> Async.StartAsTask |> ignore 
            printfn $"New CO2 value: {newValue}" |> ignore
        if newValue.Value.PartsPerMillion > ventThreshold.Value.PartsPerMillion && not ventilationIsOn then 
            toggleVent 3000 |> Async.StartAsTask |> ignore)

    do sensor.StartUpdating(TimeSpan.FromSeconds(2.0))
    let mutable s = sensor.Subscribe(consumer)

[<EntryPoint>]
let main argv =
    Console.WriteLine "Starting main..."
    let app = MeadowApp()
    Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
    0 // return an integer exit code