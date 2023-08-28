module MeadowCCS811.Tests

open MeadowProgram

open NUnit.Framework
open Meadow

let toggleVentilatorStub _ = ()

[<Test>]
let ``Test First Value`` () =
    let model, cmd = init ()
    let latestCO2Value = Units.Concentration(800.0, Units.Concentration.UnitType.PartsPerMillion) 
    let previousCO2Value = None

    let newModel, newCmd = update (SetC02Values (Some latestCO2Value, previousCO2Value, toggleVentilatorStub)) model

    Assert.AreEqual (800.0, newModel.LatestCO2Value.PartsPerMillion)
    Assert.AreEqual (0.0, newModel.PreviousCO2Value.PartsPerMillion)
    Assert.AreEqual (true, newModel.VentilationIsOn)

[<Test>]
let ``Should enable ventilator`` () = 
    let model, cmd = init ()     
    let latestCO2Value = Units.Concentration(1000.0, Units.Concentration.UnitType.PartsPerMillion) 
    let previousCO2Value = Units.Concentration(100.0, Units.Concentration.UnitType.PartsPerMillion)

    let newModel, newCmd = update (SetC02Values (Some latestCO2Value, Some previousCO2Value, toggleVentilatorStub)) model

    Assert.AreEqual (1000.0, newModel.LatestCO2Value.PartsPerMillion)
    Assert.AreEqual (100.0, newModel.PreviousCO2Value.PartsPerMillion)
    Assert.AreEqual (1900.0, newModel.ProjectedCO2Value.PartsPerMillion)
    Assert.AreEqual (true, newModel.VentilationIsOn)

[<Test>]
let ``Should disable ventilator`` () = 
    let model, cmd = init ()     
    let latestCO2Value = Units.Concentration(740.0, Units.Concentration.UnitType.PartsPerMillion) 
    let previousCO2Value = Units.Concentration(800.0, Units.Concentration.UnitType.PartsPerMillion)
        
    let newModel, newCmd = update (SetC02Values (Some latestCO2Value, Some previousCO2Value, toggleVentilatorStub)) model

    Assert.AreEqual (740.0, newModel.LatestCO2Value.PartsPerMillion)
    Assert.AreEqual (800.0, newModel.PreviousCO2Value.PartsPerMillion)
    Assert.AreEqual (false, newModel.VentilationIsOn)
