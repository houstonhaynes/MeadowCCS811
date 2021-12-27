open System
open System.Resources
open Meadow
open Meadow.Devices
open Meadow.Foundation
open Meadow.Foundation.Sensors.Atmospheric
open Meadow.Foundation.Graphics
open Meadow.Foundation.Displays.TftSpi
open Meadow.Foundation.Leds
open Meadow.Hardware
open SimpleJpegDecoder

type MeadowApp() =
    inherit App<F7Micro, MeadowApp>()

    let i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)
    let sensor = new Ccs811 (i2c)

    let led = RgbPwmLed(MeadowApp.Device, MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen,
                MeadowApp.Device.Pins.OnboardLedBlue, 3.3f, 3.3f, 3.3f, Peripherals.Leds.IRgbLed.CommonType.CommonAnode)
    let mutable onboardLEDColor : Color = Color.Red

    let triggerThreshold = Nullable (Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion))
    let reductionThreshold = Nullable (Units.Concentration(650.0, Units.Concentration.UnitType.PartsPerMillion))
    let nominalCO2Value = Nullable (Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion))
    let maximumCO2Value = Nullable (Units.Concentration(4000.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable latestCO2Value = Nullable (Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable previousCO2Value = Nullable (Units.Concentration(0.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable projectedCO2Value = Nullable (Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion))

    let config = new SpiClockConfiguration((Units.Frequency(48.0, Units.Frequency.UnitType.Kilohertz)), SpiClockConfiguration.Mode.Mode3);
    let spiBus = MeadowApp.Device.CreateSpiBus(MeadowApp.Device.Pins.SCK, MeadowApp.Device.Pins.MOSI, MeadowApp.Device.Pins.MISO, config)
    let display = new St7789 (MeadowApp.Device, spiBus, MeadowApp.Device.Pins.D02, MeadowApp.Device.Pins.D01, MeadowApp.Device.Pins.D00, 240, 240, ColorType.Format16bppRgb565)

    let displaywidth = Convert.ToInt32(display.Width)
    let displayheight = Convert.ToInt32(display.Height)
    let originx = displaywidth / 2
    let originy = displayheight / 2

    let mutable graphics = MicroGraphics(display)
    let mutable updateDisplay = 
        async {

            let outerCircleColor = match projectedCO2Value.Value.PartsPerMillion with
                                    | i when i >= 2000.0 -> Color.Red
                                    | i when i >= 1000.0 && i < 2000.0 -> Color.DarkOrange
                                    | i when i >= 650.0 && i < 1000.0 -> Color.BurlyWood
                                    | _ -> Color.LightSteelBlue

            let centerCircleColor = match latestCO2Value.Value.PartsPerMillion with
                                    | i when i >= 2000.0 -> Color.Red
                                    | i when i >= 1000.0 && i < 2000.0 -> Color.DarkOrange
                                    | i when i >= 650.0 && i < 1000.0 -> Color.BurlyWood
                                    | _ -> Color.LightSteelBlue

            graphics.CurrentFont <- Font12x16()
            graphics.Rotation <- RotationType._180Degrees
            graphics.Clear(false)
            graphics.DrawCircle(originx, originy, 115, outerCircleColor, true, true)
            graphics.DrawCircle(originx, originy, 90, Color.Black, true, true)
            graphics.DrawCircle(originx, originy, 80, centerCircleColor, true, true)
            graphics.DrawRoundedRectangle(48, 97, 145, 45, 8, Color.Black, true)
            graphics.DrawText(120, 98, $"{latestCO2Value}", Color.WhiteSmoke, ScaleFactor.X3, TextAlignment.Center)
            graphics.DrawRoundedRectangle(63, 68, 115, 24, 6, Color.Black, true)
            graphics.DrawRoundedRectangle(63, 145, 55, 24, 6, Color.Black, true)
            graphics.DrawRoundedRectangle(121, 145, 55, 24, 6, Color.Black, true)
            graphics.DrawRoundedRectangle(104, 172, 32, 32, 8, Color.Black, true)
            graphics.CurrentFont <- Font6x8()
            graphics.DrawText(67, 73, $"Breathe", Color.LightSeaGreen, ScaleFactor.X2, TextAlignment.Left)            
            graphics.DrawText(175, 73, $"EZ", Color.DeepPink, ScaleFactor.X2, TextAlignment.Right)
            graphics.DrawText(115, 150, $"{previousCO2Value}", Color.WhiteSmoke, ScaleFactor.X2, TextAlignment.Right)
            graphics.DrawText(172, 150, $"{projectedCO2Value}", Color.WhiteSmoke, ScaleFactor.X2, TextAlignment.Right)
            graphics.Show()
        }

    let mutable relayOne = Relays.Relay(MeadowApp.Device, MeadowApp.Device.Pins.D05)
    let mutable ventilationIsOn = false

    let toggleRelay duration =
        async {
            printfn "Ventilator ON..."
            while latestCO2Value.Value.PartsPerMillion > reductionThreshold.Value.PartsPerMillion do
                ventilationIsOn <- true
                if not relayOne.IsOn then 
                    relayOne.Toggle()
                if not led.IsOn then
                    led.SetColor(onboardLEDColor, 0.25f)
                do! Async.Sleep(int (duration))
            ventilationIsOn <- false
            relayOne.Toggle()
            led.SetColor(onboardLEDColor, 0.0f)
            printfn "Ventilator OFF..." |> ignore
        }

    let consumer = Ccs811.CreateObserver(fun result ->
        let newValue = match result.New with | (co2, _) -> co2
        latestCO2Value <- newValue
        let oldValue = match result.Old.Value with | (co2 , _) -> co2
        if oldValue.HasValue then
            previousCO2Value <- oldValue
            let projectedValue = Nullable (Units.Concentration((latestCO2Value.Value.PartsPerMillion + 
                                                                (latestCO2Value.Value.PartsPerMillion - previousCO2Value.Value.PartsPerMillion)), 
                                                                Units.Concentration.UnitType.PartsPerMillion))
            projectedCO2Value <- Nullable (Units.Concentration(Math.Max(projectedValue.Value.PartsPerMillion, nominalCO2Value.Value.PartsPerMillion), 
                                                                Units.Concentration.UnitType.PartsPerMillion))

        if previousCO2Value.Value.PartsPerMillion <> latestCO2Value.Value.PartsPerMillion then
            updateDisplay |> Async.RunSynchronously |> ignore 
            printfn $"New CO2 value: {latestCO2Value}" |> ignore
        if newValue.Value.PartsPerMillion > triggerThreshold.Value.PartsPerMillion && not ventilationIsOn then 
            toggleRelay 3000 |> Async.Start |> ignore)

    do sensor.StartUpdating(TimeSpan.FromSeconds(2.0))
    let mutable s = sensor.Subscribe(consumer)

[<EntryPoint>]
let main argv =
    Console.WriteLine "Starting main..."
    let app = MeadowApp()
    Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
    0 // return an integer exit code