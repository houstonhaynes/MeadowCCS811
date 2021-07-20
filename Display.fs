namespace CCS811

open Meadow.Foundation
open Meadow.Foundation.Graphics
open Meadow.Foundation.Displays.TftSpi

module Display

    // set up display
    let display = new Gc9a01 (MeadowApp.Device, 
                                MeadowApp.Device.CreateSpiBus(48000L),  
                                MeadowApp.Device.Pins.D02,  
                                MeadowApp.Device.Pins.D01,  
                                MeadowApp.Device.Pins.D00)

    let displaywidth = Convert.ToInt32(display.Width)
    let displayheight = Convert.ToInt32(display.Height)
    let originx = displaywidth / 2
    let originy = displayheight / 2

    let graphics = GraphicsLibrary(display)
    do Console.WriteLine "Initializing display"
    do graphics.Clear(true)

    let loadscreen (firstcolor: Color) (secondcolor: Color) = 
                graphics.CurrentFont <- Font12x16()
                graphics.DrawCircle(originx, originy, 120, firstcolor, true)
                graphics.DrawCircle(originx, originy, 105, Color.Black, true)
                graphics.DrawCircle(originx, originy, 100, secondcolor, true)
                graphics.DrawRoundedRectangle(32, 98, 175, 44, 8, Color.Black, true)
                graphics.DrawText(40, 102, $"{}", Color.White, GraphicsLibrary.ScaleFactor.X1)
                Console.WriteLine $"{consumer.OnNext}"

    do Console.WriteLine "loading screen..."
    do loadscreen Color.Orange Color.Blue
    do graphics.Show()

    do Console.WriteLine "loading screen second time..."
    do loadscreen Color.Blue Color.Orange
    do graphics.Show()