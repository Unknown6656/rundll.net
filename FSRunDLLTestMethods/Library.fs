namespace RunDLL

type FSTestModule() = 
    static member Lang = "F#"
    static member Fibonacci (n : int) =
        Seq.unfold (fun (x, y) -> Some(x, (y, x + y))) (0I,1I)
        |> Seq.take n