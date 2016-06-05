namespace RunDLL

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns
open Microsoft.FSharp.Linq.RuntimeHelpers

type Operations =
    static member Add (x, y) : float = x + y
    static member Sub (x, y) : float = x - y
    static member Mul (x, y) : float = x * y
    static member Div (x, y) : float = x / y


type FSTestModule() = 
    static member Lang = "F#"
    static member Fibonacci (n : int) =
        Seq.unfold (fun (x, y) -> Some(x, (y, x + y))) (0I,1I)
        |> Seq.take n
    static member Derivative (func : float -> float) =
        let addMi = (typeof<Operations>).GetMethod("Add")
        let subMi = (typeof<Operations>).GetMethod("Sub")
        let mulMi = (typeof<Operations>).GetMethod("Mul")
        let divMi = (typeof<Operations>).GetMethod("Div")
        let rec deriv (func : Expr) =
            match func with
            | Lambda(arg, body) -> Expr.Lambda(arg, deriv body)
            | Call(None, MethodWithReflectedDefinition(methBody), [ arg ]) -> deriv methBody
            | PropertyGet(None, PropertyGetterWithReflectedDefinition(body), []) -> deriv body
            | SpecificCall <@ (+) @> (_, _, [f; g]) ->
                Expr.Call(addMi, [deriv f; deriv g])
            | SpecificCall <@ (-) @> (_, _, [f; g]) ->
                Expr.Call(subMi, [deriv f; deriv g])
            | SpecificCall <@ ( * ) @> (_, _, [f; g]) ->
                let f' = deriv f
                let g' = deriv g
                Expr.Call(addMi, [ Expr.Call(mulMi, [f'; g])
                                   Expr.Call(mulMi, [f; g']) ])
            | SpecificCall <@ ( / ) @> (_, _, [f; g]) ->
                let f' = deriv f
                let g' = deriv g
                let numerator = Expr.Call(subMi, [ Expr.Call(mulMi, [f'; g])
                                                   Expr.Call(mulMi, [f; g']) ])
                let denominator = Expr.Call(mulMi, [g; g])
                Expr.Call(divMi, [numerator; denominator])
            | Var(x) -> Expr.Value(1.0, typeof<double>)
            | Double(_) -> Expr.Value(0.0, typeof<double>)
            | _ -> failwithf "Unrecognized Expr form: %A" func
        let func' =
            let quote : Expr<float -> float> = deriv <@ func @>
                                               |> Expr.Cast
            // TODO: quote.Compile()
            (fun x -> x) // <---prevent errors
        func'
    static member DerivativeTest =
        let f (x : float) : float =
            (1.5 * (x ** 3.0)) + (3.0 * (x ** 2.0)) - (80.0 * x) + 5.0  // 1.5x^3 + 3x^2 - 80x + 5
        let f' = FSTestModule.Derivative f
        for i in [-10.0 .. 0.1 .. 10.0] do
            let f = f i
            let df = f' i
            printf "f(%f) = %f \t f'(%f) = %f\n" i f i df
        ()