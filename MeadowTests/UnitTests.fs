module MeadowCCS811.UnitTests

open MeadowCCS811.Program
open NUnit.Framework
open Swensen.Unquote

let toggleVentilatorStub _ = ()

[<Test>]
let ``First value should enable ventilator`` () =
    let model, _ = init ()
    let latestCO2Value = ppm 800.0
    let previousCO2Value = None

    let m2, _ = update (SetC02Values (Some latestCO2Value, previousCO2Value, toggleVentilatorStub)) model

    m2.LatestCO2Value.PartsPerMillion =! 800.0
    m2.PreviousCO2Value.PartsPerMillion =! 0.0
    m2.VentilationIsOn =! true

[<Test>]
let ``Subsequent value enable ventilator`` () = 
    let model, _ = init ()     
    let latestCO2Value = ppm 1000.0
    let previousCO2Value = ppm 100.0

    let m2, _ = update (SetC02Values (Some latestCO2Value, Some previousCO2Value, toggleVentilatorStub)) model

    m2.LatestCO2Value.PartsPerMillion =! 1000.0
    m2.PreviousCO2Value.PartsPerMillion =! 100.0
    m2.ProjectedCO2Value.PartsPerMillion =! 1900.0
    m2.VentilationIsOn =! true

[<Test>]
let ``Should disable ventilator`` () = 
    let model, _ = init ()     
    let latestCO2Value = ppm 740.0
    let previousCO2Value = ppm 800.0
        
    let m2, _ = update (SetC02Values (Some latestCO2Value, Some previousCO2Value, toggleVentilatorStub)) model

    m2.LatestCO2Value.PartsPerMillion =! 740.0
    m2.PreviousCO2Value.PartsPerMillion =! 800.0
    m2.VentilationIsOn =! false
