module MeadowProgram

open System
open System.Resources
open Meadow
open Meadow.Devices
open Meadow.Foundation
open Meadow.Foundation.Sensors.Atmospheric
open Meadow.Foundation.Graphics
open Meadow.Foundation.Displays
open Meadow.Foundation.Leds
open Meadow.Hardware
open SimpleJpegDecoder
open Elmish

[<AutoOpen>]
module Constants = 
    let onboardLEDColor = Color.Red
    let triggerThreshold = Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion)
    let reductionThreshold = Units.Concentration(650.0, Units.Concentration.UnitType.PartsPerMillion)
    let nominalCO2Value = Units.Concentration(400.0, Units.Concentration.UnitType.PartsPerMillion)
    let maximumCO2Value = Units.Concentration(4000.0, Units.Concentration.UnitType.PartsPerMillion)

type Model = 
    {
        LatestCO2Value: Units.Concentration
        PreviousCO2Value: Units.Concentration
        ProjectedCO2Value: Units.Concentration
        VentilationIsOn: bool
    }

type Msg = 
    | SetC02Values of current: Units.Concentration option * previous: Units.Concentration option

let init () = 
    {
        LatestCO2Value = Units.Concentration(0.0, Units.Concentration.UnitType.PartsPerMillion)
        PreviousCO2Value = Units.Concentration(0.0, Units.Concentration.UnitType.PartsPerMillion)
        ProjectedCO2Value = Units.Concentration(0.0, Units.Concentration.UnitType.PartsPerMillion)
        VentilationIsOn = false
    }, Cmd.none

let update (msg: Msg) (model : Model) = 
    match msg with
    | SetC02Values (newValue, oldValue) ->
        match newValue, oldValue with
        | Some newValue, None -> 
            { model with
                LatestCO2Value = newValue
                VentilationIsOn = false
            }, Cmd.none
        | Some newValue, Some oldValue -> 
            { model with 
                LatestCO2Value = newValue
                PreviousCO2Value = oldValue
                ProjectedCO2Value = 
                    Units.Concentration((newValue.PartsPerMillion + (newValue.PartsPerMillion - oldValue.PartsPerMillion)), Units.Concentration.UnitType.PartsPerMillion)
                    |> fun value -> Units.Concentration(Math.Max(value.PartsPerMillion, nominalCO2Value.PartsPerMillion), Units.Concentration.UnitType.PartsPerMillion)
                VentilationIsOn = newValue.PartsPerMillion > reductionThreshold.PartsPerMillion
                    
            }, Cmd.none
        | _ -> 
           model, Cmd.none

type MeadowApp() =
    inherit App<F7FeatherV1>()

    let i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)
    let led = RgbPwmLed(MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen, MeadowApp.Device.Pins.OnboardLedBlue)
    let config = new SpiClockConfiguration((Units.Frequency(48.0, Units.Frequency.UnitType.Kilohertz)), SpiClockConfiguration.Mode.Mode3);
    let spiBus = MeadowApp.Device.CreateSpiBus(MeadowApp.Device.Pins.SCK, MeadowApp.Device.Pins.MOSI, MeadowApp.Device.Pins.MISO, config)
    let display = new St7789 (spiBus, MeadowApp.Device.Pins.D02, MeadowApp.Device.Pins.D01, MeadowApp.Device.Pins.D00, 240, 240, ColorMode.Format16bppRgb565)

    let displaywidth = Convert.ToInt32(display.Width)
    let displayheight = Convert.ToInt32(display.Height)
    let originx = displaywidth / 2
    let originy = displayheight / 2

    let graphics = MicroGraphics(display)
    let relayOne = Relays.Relay(MeadowApp.Device.Pins.D05)

    member this.Sensor = new Ccs811 (i2c)

    member this.UpdateDisplay (model: Model) (dispatch: Msg -> unit) = 
        // Update display only if CO2 value has changed
        if model.LatestCO2Value.PartsPerMillion <> model.PreviousCO2Value.PartsPerMillion then
            let outerCircleColor = 
                match model.ProjectedCO2Value.PartsPerMillion with
                | i when i >= 2000.0 -> Color.Red
                | i when i >= 1000.0 && i < 2000.0 -> Color.DarkOrange
                | i when i >= 650.0 && i < 1000.0 -> Color.BurlyWood
                | _ -> Color.LightSteelBlue

            let centerCircleColor = 
                match model.LatestCO2Value.PartsPerMillion with
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
            graphics.DrawText(120, 98, $"{model.LatestCO2Value}", Color.WhiteSmoke, ScaleFactor.X3, HorizontalAlignment.Center)
            graphics.DrawRoundedRectangle(63, 68, 115, 24, 6, Color.Black, true)
            graphics.DrawRoundedRectangle(63, 145, 55, 24, 6, Color.Black, true)
            graphics.DrawRoundedRectangle(121, 145, 55, 24, 6, Color.Black, true)
            graphics.DrawRoundedRectangle(104, 172, 32, 32, 8, Color.Black, true)
            graphics.CurrentFont <- Font6x8()
            graphics.DrawText(67, 73, $"Breathe", Color.LightSeaGreen, ScaleFactor.X2, HorizontalAlignment.Left)            
            graphics.DrawText(175, 73, $"EZ", Color.DeepPink, ScaleFactor.X2, HorizontalAlignment.Right)
            graphics.DrawText(115, 150, $"{model.PreviousCO2Value}", Color.WhiteSmoke, ScaleFactor.X2, HorizontalAlignment.Right)
            graphics.DrawText(172, 150, $"{model.ProjectedCO2Value}", Color.WhiteSmoke, ScaleFactor.X2, HorizontalAlignment.Right)
            graphics.Show()


    member this.ToggleVentilator enabled =
        if enabled then
            printfn "Ventilator ON..."
            if not relayOne.IsOn then 
                relayOne.Toggle()
            if not led.IsOn then
                led.SetColor(onboardLEDColor, 0.25f)               
        else 
            printfn "Ventilator OFF..."
            if relayOne.IsOn then
                relayOne.Toggle()
            if led.IsOn then
                led.SetColor(onboardLEDColor, 0.0f)



[<EntryPoint>]
let main argv =
    Console.WriteLine "Starting main..."
    let meadow = new MeadowApp()

    let subscriptions (model: Model) : Sub<Msg> =      
        let sensorSubscription (dispatch: Msg -> unit) = 
            
            let consumer = Ccs811.CreateObserver(fun result ->
                let struct (newValue, _) = result.New
                let struct (oldValue, _) = result.Old.Value
                
                // Feed new values to the model
                dispatch (SetC02Values (Option.ofNullable newValue, Option.ofNullable oldValue))

                // Toggle ventilator on/off based on the model
                meadow.ToggleVentilator model.VentilationIsOn
            )

            meadow.Sensor.StartUpdating(TimeSpan.FromSeconds(2.0))
            meadow.Sensor.Subscribe(consumer)

        [
            [ nameof sensorSubscription ], sensorSubscription
        ]

    Program.mkProgram init update meadow.UpdateDisplay
    |> Program.withSubscription subscriptions
    |> Program.run

    Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
    0 // return an integer exit code