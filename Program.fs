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

let ppb value = Units.Concentration(value, Units.Concentration.UnitType.PartsPerBillion)

[<AutoOpen>]
module Constants = 
    let onboardLEDColor = RgbLedColors.Cyan
    let triggerThreshold = ppb 50.0
    let reductionThreshold = ppb 25.0
    let nominalVOCValue = ppb 0.0

type Model = 
    {
        LatestVOCValue: Units.Concentration
        PreviousVOCValue: Units.Concentration
        ProjectedVOCValue: Units.Concentration
        VentilationIsOn: bool
    }

type Msg = 
    | SetVOCValues of current: Units.Concentration option * previous: Units.Concentration option * toggleVentilator: (bool -> unit)

let init () = 
    {
        LatestVOCValue = ppb 0.0
        PreviousVOCValue = ppb 0.0
        ProjectedVOCValue = ppb 0.0
        VentilationIsOn = false
    }, Cmd.none

let update (msg: Msg) (model : Model) = 
    match msg with
    | SetVOCValues (newValue, oldValue, toggleVentilator) ->
        
        let calculateVentEnabled (newValue: Units.Concentration) = 
            let startVent = newValue.PartsPerBillion > triggerThreshold.PartsPerBillion
            let continueVent = model.VentilationIsOn && newValue.PartsPerBillion > reductionThreshold.PartsPerBillion
            startVent || continueVent

        match newValue, oldValue with
        | Some newValue, None -> 
            let ventEnabled = calculateVentEnabled newValue
            { model with
                LatestVOCValue = newValue
                VentilationIsOn = ventEnabled
            }, Cmd.ofEffect (fun _ -> toggleVentilator ventEnabled)

        | Some newValue, Some oldValue -> 
            let ventEnabled = calculateVentEnabled newValue
            { model with 
                LatestVOCValue = newValue
                PreviousVOCValue = oldValue
                ProjectedVOCValue = 
                    let projectedValue = 
                        match oldValue.PartsPerBillion with 
                        | i when i = 0.0 -> nominalVOCValue
                        | _ -> ppb ((newValue.PartsPerBillion + (newValue.PartsPerBillion - oldValue.PartsPerBillion)))
                    ppb (Math.Max(projectedValue.PartsPerBillion, nominalVOCValue.PartsPerBillion))
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
            | i when i >= 500.0 -> Color.Red
            | i when i >= 200.0 && i < 500.0 -> Color.DarkOrange
            | i when i >= 50.0 && i < 200.0 -> Color.BurlyWood
            | _ -> Color.LightSteelBlue

        let updateDisplay (model: Model) (dispatch: Msg -> unit) = 
            // Update canvas if the VOC value has changed
            if model.PreviousVOCValue.PartsPerBillion <> model.LatestVOCValue.PartsPerBillion then
                Resolver.Log.Info $"New VOC value: {model.LatestVOCValue.PartsPerBillion}" |> ignore
                
                let outerCircleColor = concentrationColor model.ProjectedVOCValue.PartsPerBillion
                let centerCircleColor = concentrationColor model.LatestVOCValue.PartsPerBillion
                let previousValueColor = concentrationColor model.PreviousVOCValue.PartsPerBillion
                let directionImage = 
                    if model.LatestVOCValue.PartsPerBillion > model.PreviousVOCValue.PartsPerBillion
                    then upBmpImage
                    else dnBmpImage

                canvas.IgnoreOutOfBoundsPixels <- true
                canvas.CurrentFont <- Font12x16()
                canvas.Clear(false)
                canvas.DrawCircle(originX, originY, 115, outerCircleColor, true, true)
                canvas.DrawCircle(originX, originY, 90, Color.Black, true, true)
                canvas.DrawCircle(originX, originY, 80, centerCircleColor, true, true)
                canvas.DrawRoundedRectangle(48, 97, 145, 45, 8, Color.Black, true)
                canvas.DrawText(120, 98, $"{model.LatestVOCValue.PartsPerBillion}", centerCircleColor, ScaleFactor.X3, HorizontalAlignment.Center)
                canvas.DrawRoundedRectangle(62, 68, 115, 24, 6, Color.Black, true)
                canvas.DrawRoundedRectangle(62, 145, 55, 24, 6, Color.Black, true)
                canvas.DrawRoundedRectangle(120, 143, 55, 24, 6, Color.Black, true)
                canvas.DrawRoundedRectangle(102, 172, 40, 36, 8, Color.Black, true)
                canvas.CurrentFont <- Font6x8()
                canvas.DrawText(67, 73, $"Breathe", Color.LightSeaGreen, ScaleFactor.X2, HorizontalAlignment.Left)            
                canvas.DrawText(175, 73, $"EZ", Color.DeepPink, ScaleFactor.X2, HorizontalAlignment.Right)
                canvas.DrawText(115, 150, $"{model.PreviousVOCValue.PartsPerBillion}", previousValueColor, ScaleFactor.X2, HorizontalAlignment.Right)
                canvas.DrawText(172, 150, $"{model.ProjectedVOCValue.PartsPerBillion}", outerCircleColor, ScaleFactor.X2, HorizontalAlignment.Right)
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
                    led.IsOn <- false
                            
        let subscriptions (model: Model) : Sub<Msg> =      
            let sensorSubscription (dispatch: Msg -> unit) = 
                let consumer = Ccs811.CreateObserver(fun result ->
                    let newValue = 
                        match result.New with 
                        | struct (_, newValue) -> newValue |> Option.ofNullable

                    let oldValue = 
                        match result.Old |> Option.ofNullable with
                        | Some struct (_, oldValue) -> oldValue |> Option.ofNullable
                        | None -> None

                    // Feed new values and side-effect function to model
                    dispatch (SetVOCValues (newValue, oldValue, toggleVentilator))
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