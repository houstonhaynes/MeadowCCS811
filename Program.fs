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

    // set up relays

    let relayGreen = Relays.Relay(MeadowApp.Device, MeadowApp.Device.Pins.D05)
    let relayOrange = Relays.Relay(MeadowApp.Device, MeadowApp.Device.Pins.D06)

    // set up sensor
    let i2c = MeadowApp.Device.CreateI2cBus(Hardware.I2cBusSpeed.Standard)

    let sensor = new Ccs811 (i2c)
    let mutable latestCO2Value = System.Nullable()
    
    let consumer = Ccs811.CreateObserver(fun result -> 
        let newValue = match result.New with | (new_val, _) -> new_val
        latestCO2Value <- newValue
        printfn $"New CO2 value: {latestCO2Value}")

    let s = sensor.Subscribe(consumer)

    do sensor.StartUpdating(TimeSpan.FromSeconds(2.0))

    // set up display
    //let display = new Gc9a01 (MeadowApp.Device, 
    //                            MeadowApp.Device.CreateSpiBus(48000L),  
    //                            MeadowApp.Device.Pins.D02,  
    //                            MeadowApp.Device.Pins.D01,  
    //                            MeadowApp.Device.Pins.D00)

    //let displaywidth = Convert.ToInt32(display.Width)
    //let displayheight = Convert.ToInt32(display.Height)
    //let originx = displaywidth / 2
    //let originy = displayheight / 2

    //let graphics = GraphicsLibrary(display)
    //do Console.WriteLine "Initializing display"
    //do graphics.Clear(true)

    //let loadscreen (firstcolor: Color) (secondcolor: Color) = 
    //            graphics.CurrentFont <- Font12x16()
    //            graphics.DrawCircle(originx, originy, 120, firstcolor, true)
    //            graphics.DrawCircle(originx, originy, 105, Color.Black, true)
    //            graphics.DrawCircle(originx, originy, 100, secondcolor, true)
    //            graphics.DrawRoundedRectangle(32, 98, 175, 44, 8, Color.Black, true)
    //            graphics.DrawText(40, 102, $"{}", Color.White, GraphicsLibrary.ScaleFactor.X1)
    //            Console.WriteLine $"{consumer.OnNext}"

    //do Console.WriteLine "loading screen..."
    //do loadscreen Color.Orange Color.Blue
    //do graphics.Show()

    //do Console.WriteLine "loading screen second time..."
    //do loadscreen Color.Blue Color.Orange
    //do graphics.Show()

    // boilerplate LED stuff
    let led =
        RgbPwmLed(MeadowApp.Device, MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen,
                      MeadowApp.Device.Pins.OnboardLedBlue, 3.3f, 3.3f, 3.3f,
                      Peripherals.Leds.IRgbLed.CommonType.CommonAnode)


    //let showColorBlinks color duration =
    //    led.StartBlink(color, (duration / 2), (duration / 2), 0.75f, 0.0f) |> ignore
    //    Threading.Thread.Sleep(int duration) |> ignore
    //    led.Stop |> ignore

    let warningThreshold = Nullable (Units.Concentration(750.0, Units.Concentration.UnitType.PartsPerMillion))
    let dangerThreshold = Nullable (Units.Concentration(1500.0, Units.Concentration.UnitType.PartsPerMillion))

    let toggleRelays duration =
        while latestCO2Value.Value.PartsPerMillion > warningThreshold.Value.PartsPerMillion do 
            relayGreen.IsOn |> ignore
            relayOrange.IsOn |> ignore
            Thread.Sleep(int (duration / 2)) |> ignore
            relayGreen.Toggle() |> ignore
            relayOrange.Toggle() |> ignore
            Thread.Sleep(int (duration / 2)) |> ignore

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