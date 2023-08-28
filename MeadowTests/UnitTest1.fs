module MeadowCCS811.Tests

open MeadowProgram

open NUnit.Framework
open Meadow
open Swensen.Unquote

let toggleVentilatorStub _ = ()

[<Test>]
let ``Test First Value`` () =
    let model, _ = init ()
    let latestCO2Value = Units.Concentration(800.0, Units.Concentration.UnitType.PartsPerMillion) 
    let previousCO2Value = None

    let m2, _ = update (SetC02Values (Some latestCO2Value, previousCO2Value, toggleVentilatorStub)) model

    m2.LatestCO2Value.PartsPerMillion =! 800.0
    m2.PreviousCO2Value.PartsPerMillion =! 0.0
    m2.VentilationIsOn =! true

[<Test>]
let ``Should enable ventilator`` () = 
    let model, _ = init ()     
    let latestCO2Value = Units.Concentration(1000.0, Units.Concentration.UnitType.PartsPerMillion) 
    let previousCO2Value = Units.Concentration(100.0, Units.Concentration.UnitType.PartsPerMillion)

    let m2, _ = update (SetC02Values (Some latestCO2Value, Some previousCO2Value, toggleVentilatorStub)) model

    m2.LatestCO2Value.PartsPerMillion =! 1000.0
    m2.PreviousCO2Value.PartsPerMillion =! 100.0
    m2.ProjectedCO2Value.PartsPerMillion =! 1900.0
    m2.VentilationIsOn =! true

[<Test>]
let ``Should disable ventilator`` () = 
    let model, _ = init ()     
    let latestCO2Value = Units.Concentration(740.0, Units.Concentration.UnitType.PartsPerMillion) 
    let previousCO2Value = Units.Concentration(800.0, Units.Concentration.UnitType.PartsPerMillion)
        
    let m2, _ = update (SetC02Values (Some latestCO2Value, Some previousCO2Value, toggleVentilatorStub)) model

    m2.LatestCO2Value.PartsPerMillion =! 740.0
    m2.PreviousCO2Value.PartsPerMillion =! 800.0
    m2.VentilationIsOn =! false
