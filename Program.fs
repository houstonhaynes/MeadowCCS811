module MeadowCCS811.Program

open System
open System.IO
open Meadow
open Meadow.Devices
open Meadow.Hardware
open Meadow.Foundation
open Meadow.Foundation.Graphics
open Meadow.Foundation.Displays
open Meadow.Foundation.Sensors.Atmospheric
open Meadow.Foundation.Leds
open Elmish

let ppm value = Units.Concentration(value, Units.Concentration.UnitType.PartsPerMillion)

[<AutoOpen>]
module Constants = 
    let onboardLEDColor = RgbLedColors.Cyan
    let triggerThreshold = ppm 750.0
    let reductionThreshold = ppm 650.0
    let nominalCO2Value = ppm 400.0

type Model = 
    {
        LatestCO2Value: Units.Concentration
        PreviousCO2Value: Units.Concentration
        ProjectedCO2Value: Units.Concentration
        VentilationIsOn: bool
    }

type Msg = 
    | SetC02Values of current: Units.Concentration option * previous: Units.Concentration option * toggleVentilator: (bool -> unit)

let init () = 
    {
        LatestCO2Value = ppm 0.0
        PreviousCO2Value = ppm 0.0
        ProjectedCO2Value = ppm 0.0
        VentilationIsOn = false
    }, Cmd.none

let update (msg: Msg) (model : Model) = 
    match msg with
    | SetC02Values (newValue, oldValue, toggleVentilator) ->
        
        let calculateVentEnabled (newValue: Units.Concentration) = 
            let startVent = newValue.PartsPerMillion > triggerThreshold.PartsPerMillion
            let continueVent = model.VentilationIsOn && newValue.PartsPerMillion > reductionThreshold.PartsPerMillion
            startVent || continueVent

        match newValue, oldValue with
        | Some newValue, None -> 
            let ventEnabled = calculateVentEnabled newValue
            { model with
                LatestCO2Value = newValue
                VentilationIsOn = ventEnabled
            }, Cmd.ofEffect (fun _ -> toggleVentilator ventEnabled)

        | Some newValue, Some oldValue -> 
            let ventEnabled = calculateVentEnabled newValue
            { model with 
                LatestCO2Value = newValue
                PreviousCO2Value = oldValue
                ProjectedCO2Value = 
                    let projectedValue = 
                        match oldValue.PartsPerMillion with 
                        | i when i = 0.0 -> nominalCO2Value
                        | _ -> ppm ((newValue.PartsPerMillion + (newValue.PartsPerMillion - oldValue.PartsPerMillion)))
                    ppm (Math.Max(projectedValue.PartsPerMillion, nominalCO2Value.PartsPerMillion))
                VentilationIsOn = ventEnabled
            }, Cmd.ofEffect (fun _ -> toggleVentilator ventEnabled)
        | _ -> 
           model, Cmd.none


type MeadowApp() =
    inherit App<F7FeatherV1>()
    
    override this.Run () =
        let i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)
        let sensor = new Ccs811(i2c)
        let led = 
            RgbLed(MeadowApp.Device.Pins.OnboardLedRed, 
                MeadowApp.Device.Pins.OnboardLedGreen,
                MeadowApp.Device.Pins.OnboardLedBlue)        
        let config = 
            new SpiClockConfiguration(new Meadow.Units.Frequency(48.0, 
                Meadow.Units.Frequency.UnitType.Kilohertz), 
                SpiClockConfiguration.Mode.Mode3)
        let spiBus = 
            MeadowApp.Device.CreateSpiBus(
                MeadowApp.Device.Pins.SCK,
                MeadowApp.Device.Pins.MOSI,
                MeadowApp.Device.Pins.MISO,
                config)
        let display = 
            new Gc9a01 (
                spiBus, 
                MeadowApp.Device.Pins.D02, 
                MeadowApp.Device.Pins.D01, 
                MeadowApp.Device.Pins.D00)
        let displaywidth = Convert.ToInt32(display.Width)
        let displayheight = Convert.ToInt32(display.Height)
        let originX = displaywidth / 2
        let originY = displayheight / 2
        let loadBmp filename = 
            let filePath = Path.Combine(MeadowOS.FileSystem.UserFileSystemRoot, $"{filename}.bmp");
            let image = Image.LoadFromFile(filePath)
            image
        let upBmpImage = loadBmp "arrow-up"
        let dnBmpImage = loadBmp "arrow-down"
        
        let canvas = MicroGraphics(display)
        let concentrationColor value = 
            match value with
            | i when i >= 2000.0 -> Color.Red
            | i when i >= 1000.0 && i < 2000.0 -> Color.DarkOrange
            | i when i >= 650.0 && i < 1000.0 -> Color.BurlyWood
            | _ -> Color.LightSteelBlue

        let updateDisplay (model: Model) (dispatch: Msg -> unit) = 
            // Update canvas if the CO2 value has changed
            if model.PreviousCO2Value.PartsPerMillion <> model.LatestCO2Value.PartsPerMillion then
                Resolver.Log.Info $"New CO2 value: {model.LatestCO2Value.PartsPerMillion}" |> ignore
                
                let outerCircleColor = concentrationColor model.ProjectedCO2Value.PartsPerMillion
                let centerCircleColor = concentrationColor model.LatestCO2Value.PartsPerMillion
                let previousValueColor = concentrationColor model.PreviousCO2Value.PartsPerMillion
                let directionImage = 
                    if model.LatestCO2Value.PartsPerMillion > model.PreviousCO2Value.PartsPerMillion
                    then upBmpImage
                    else dnBmpImage

                canvas.IgnoreOutOfBoundsPixels <- true
                canvas.CurrentFont <- Font12x16()
                canvas.Clear(false)
                canvas.DrawCircle(originX, originY, 115, outerCircleColor, true, true)
                canvas.DrawCircle(originX, originY, 90, Color.Black, true, true)
                canvas.DrawCircle(originX, originY, 80, centerCircleColor, true, true)
                canvas.DrawRoundedRectangle(48, 97, 145, 45, 8, Color.Black, true)
                canvas.DrawText(120, 98, $"{model.LatestCO2Value}", centerCircleColor, ScaleFactor.X3, HorizontalAlignment.Center)
                canvas.DrawRoundedRectangle(62, 68, 115, 24, 6, Color.Black, true)
                canvas.DrawRoundedRectangle(62, 145, 55, 24, 6, Color.Black, true)
                canvas.DrawRoundedRectangle(120, 143, 55, 24, 6, Color.Black, true)
                canvas.DrawRoundedRectangle(102, 172, 40, 36, 8, Color.Black, true)
                canvas.CurrentFont <- Font6x8()
                canvas.DrawText(67, 73, $"Breathe", Color.LightSeaGreen, ScaleFactor.X2, HorizontalAlignment.Left)            
                canvas.DrawText(175, 73, $"EZ", Color.DeepPink, ScaleFactor.X2, HorizontalAlignment.Right)
                canvas.DrawText(115, 150, $"{model.PreviousCO2Value}", previousValueColor, ScaleFactor.X2, HorizontalAlignment.Right)
                canvas.DrawText(172, 150, $"{model.ProjectedCO2Value}", outerCircleColor, ScaleFactor.X2, HorizontalAlignment.Right)
                canvas.DrawImage (104, 174, directionImage)
                canvas.Show()
        
        let relayOne = Relays.Relay(MeadowApp.Device.Pins.D05)

        let toggleVentilator enabled =
            if enabled then
                if not relayOne.IsOn then 
                    Resolver.Log.Info "Ventilator ON..."
                    relayOne.Toggle()
                if not led.IsOn then
                    led.SetColor(onboardLEDColor)               
            else 
                if relayOne.IsOn then
                    Resolver.Log.Info "Ventilator OFF..."
                    relayOne.Toggle()
                if led.IsOn then
                    led.SetColor(onboardLEDColor)
                            
        let subscriptions (model: Model) : Sub<Msg> =      
            let sensorSubscription (dispatch: Msg -> unit) = 
                let consumer = Ccs811.CreateObserver(fun result ->
                    let newValue = 
                        match result.New with 
                        | struct (newValue, _) -> newValue |> Option.ofNullable

                    let oldValue = 
                        match result.Old |> Option.ofNullable with
                        | Some struct (oldValue, _) -> oldValue |> Option.ofNullable
                        | None -> None

                    // Feed new values and side-effect function to model
                    dispatch (SetC02Values (newValue, oldValue, toggleVentilator))
                )

                sensor.StartUpdating(TimeSpan.FromSeconds(2.0))
                sensor.Subscribe(consumer)

            [
                [ nameof sensorSubscription ], sensorSubscription
            ]

        Program.mkProgram init update updateDisplay
        |> Program.withSubscription subscriptions
        |> Program.run

        base.Run()