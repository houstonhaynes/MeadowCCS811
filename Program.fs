namespace MeadowApp


open System
open System.Resources
open System.IO
open Meadow
open Meadow.Devices
open Meadow.Foundation
open Meadow.Foundation.Sensors.Atmospheric
open Meadow.Foundation.Graphics
open Meadow.Foundation.Graphics.Buffers
open Meadow.Foundation.Displays
open Meadow.Foundation.Leds
open SimpleJpegDecoder

type MeadowApp() =
    inherit App<F7FeatherV1>()
    let i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)
    let sensor = new Ccs811 (i2c)
    let led = RgbPwmLed(MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen,
                MeadowApp.Device.Pins.OnboardLedBlue)
    let mutable onboardLEDColor : Color = Color.Red
    let triggerThreshold = Nullable (Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion))
    let reductionThreshold = Nullable (Units.Concentration(650.0, Units.Concentration.UnitType.PartsPerMillion))
    let nominalCO2Value = Nullable (Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion))
    let maximumCO2Value = Nullable (Units.Concentration(4000.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable latestCO2Value = Nullable (Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable previousCO2Value = Nullable (Units.Concentration(0.0, Units.Concentration.UnitType.PartsPerMillion))
    let mutable projectedCO2Value = Nullable (Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion))


    let spiBus = MeadowApp.Device.CreateSpiBus(MeadowApp.Device.Pins.SCK, MeadowApp.Device.Pins.MOSI, MeadowApp.Device.Pins.MISO)

    let display = new St7789 (spiBus,  
                                MeadowApp.Device.Pins.D02,  
                                MeadowApp.Device.Pins.D01,  
                                MeadowApp.Device.Pins.D00,
                                240,
                                240,
                                ColorMode.Format16bppRgb565)

    let displaywidth = Convert.ToInt32(display.Width)
    let displayheight = Convert.ToInt32(display.Height)
    
    let originX = displaywidth / 2
    let originY = displayheight / 2
    let originY = displayheight / 2

    let decoder = new JpegDecoder()

    let upArrowLocation = Path.Combine(MeadowOS.FileSystem.UserFileSystemRoot, $"arrow-up.jpg")
    let upArrowBytes = File.ReadAllBytes(upArrowLocation)
    let upArrowDecoded = decoder.DecodeJpeg(upArrowBytes)
    let upArrowBuffer = new BufferRgb888(32, 32, upArrowDecoded)
    
    let dnArrowLocation = Path.Combine(MeadowOS.FileSystem.UserFileSystemRoot, $"arrow-down.jpg")
    let dnArrowBytes = File.ReadAllBytes(dnArrowLocation)
    let dnArrowDecoded = decoder.DecodeJpeg(dnArrowBytes)
    let dnArrowBuffer = new BufferRgb888(32, 32, dnArrowDecoded)

    let mutable canvas = MicroGraphics(display)
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

            let previousValueColor = match previousCO2Value.Value.PartsPerMillion with
                                        | i when i >= 2000.0 -> Color.Red
                                        | i when i >= 1000.0 && i < 2000.0 -> Color.DarkOrange
                                        | i when i >= 650.0 && i < 1000.0 -> Color.BurlyWood
                                        | _ -> Color.LightSteelBlue

            let directionImage = match latestCO2Value.Value.PartsPerMillion with
                                    | i when i > previousCO2Value.Value.PartsPerMillion -> upArrowBuffer
                                    | _ -> dnArrowBuffer

            canvas.CurrentFont <- Font12x16()
            canvas.Clear(false)
            canvas.DrawCircle(originX, originY, 115, outerCircleColor, true, true)
            canvas.DrawCircle(originX, originY, 90, Color.Black, true, true)
            canvas.DrawCircle(originX, originY, 80, centerCircleColor, true, true)
            canvas.DrawRoundedRectangle(48, 97, 145, 45, 8, Color.Black, true)
            canvas.DrawText(120, 98, $"{latestCO2Value}", centerCircleColor, ScaleFactor.X3, HorizontalAlignment.Center)
            canvas.DrawRoundedRectangle(63, 68, 115, 24, 6, Color.Black, true)
            canvas.DrawRoundedRectangle(63, 145, 55, 24, 6, Color.Black, true)
            canvas.DrawRoundedRectangle(121, 145, 55, 24, 6, Color.Black, true)
            canvas.DrawRoundedRectangle(102, 172, 36, 34, 8, Color.Black, true)
            canvas.CurrentFont <- Font6x8()
            canvas.DrawText(67, 73, $"Breathe", Color.LightSeaGreen, ScaleFactor.X2, HorizontalAlignment.Left)            
            canvas.DrawText(175, 73, $"EZ", Color.DeepPink, ScaleFactor.X2, HorizontalAlignment.Right)
            canvas.DrawText(115, 150, $"{previousCO2Value}", previousValueColor, ScaleFactor.X2, HorizontalAlignment.Right)
            canvas.DrawText(172, 150, $"{projectedCO2Value}", outerCircleColor, ScaleFactor.X2, HorizontalAlignment.Right)
            canvas.DrawBuffer (104, 174, directionImage)
            canvas.Show()
        }


    let mutable relayOne = Relays.Relay(MeadowApp.Device.Pins.D05)
    let mutable ventilationIsOn = false

    let toggleRelay duration =
        async {
            Resolver.Log.Info "Ventilator ON..."
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
            Resolver.Log.Info "Ventilator OFF..." |> ignore
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
            Resolver.Log.Info $"New CO2 value: {latestCO2Value}" |> ignore
        if newValue.Value.PartsPerMillion > triggerThreshold.Value.PartsPerMillion && not ventilationIsOn then 
            toggleRelay 3000 |> Async.Start |> ignore)

    do sensor.StartUpdating(TimeSpan.FromSeconds(2.0))
    let mutable s = sensor.Subscribe(consumer)

    override this.Initialize() =
        do Resolver.Log.Info "Initialize... (F#)"

        base.Initialize()
    
    override this.Run () =
        do Resolver.Log.Info "Run... (F#)"

        do Resolver.Log.Info "Hello, Meadow!"

        base.Run()