namespace Rallyresults

module Event = 
    let Code = "GS15"
    let Id = 10

module Types = 
    type ComptitorEvent = {
        carNumber : string
        driver : string
        codriver : string
        address : string
        car : string
        category : string
        stageData : seq<float * float>
    }


//module Logger = 
//
//    open System
//    open System.IO
//
//    let WriteToFile(format:string) =   
//        use streamWriter = new StreamWriter("C:\Users\soundstore1\Desktop\logger.log", true)
//        //let file = FileInfo("C:\Users\soundstore1\Desktop\logger.log")
//        streamWriter.WriteLine(format)
//
//    type Level =
//        | Error = 0
//        | Warn = 1
//        | Info = 2
//        | Debug = 3
//
//    let LevelToString level =
//      match level with
//        | Level.Error -> "Error"
//        | Level.Warn -> "Warn"
//        | Level.Info -> "Info"
//        | Level.Debug -> "Debug"
//        | _ -> "Unknown"
//
//    /// The current log level.
//    let mutable current_log_level = Level.Debug
//
//    /// The inteface loggers need to implement.
//    type ILogger = abstract Log : Level -> string -> unit
//
//    /// Writes to Console.
//    let ConsoleLogger = { 
//        new ILogger with
//            member __.Log level format =
//                printfn "[%s] [%A] %s" (LevelToString level) System.DateTime.Now format
//     }
//
//    /// Writes to File.
//    let FileLogger = { 
//        new ILogger with
//            member __.Log level format =
//                WriteToFile(sprintf "[%s] [%A] %s" (LevelToString level) System.DateTime.Now format)
//     }
//
//    /// Defines which logger to use.
//    let DefaultLogger = [| FileLogger; ConsoleLogger |]
//
//    /// Logs a message with the specified logger.
//    let logUsing (logger: ILogger) = logger.Log
//
//    /// Logs a message using the default logger.
//    let log level message = 
//        DefaultLogger |> Array.map(fun (logger:ILogger) -> logUsing logger level message) |> ignore

module Database = 
    
    open Npgsql
//    open Logger
    open System

    let connectionString = "Server = ec2-54-225-134-223.compute-1.amazonaws.com; Port = 5432; Database = d49543pfts5f0l; User Id = nwonqgeeruzeak; Password = eWrPXK_l6evEJ8p-OB1hh5hOGA ; CommandTimeout = 40; SSL=True; Sslmode=require;"


    let testDB(competitor:Types.ComptitorEvent, connection) = 
        printfn "%s" (competitor.driver)

        let stageTimes = 
            try
                let stageAndPenaltyTime =
                    competitor.stageData
                    |> Seq.map (fun (stage, penalty)-> "{" + stage.ToString() + ", " + penalty.ToString() + "}")
                    |> Seq.reduce (fun state item -> state + ", " + item)
                "{" + stageAndPenaltyTime.ToString() + "}"
            with
            | :? System.InvalidOperationException as ex -> 
                "{}"

        let superRally = competitor.carNumber.Contains("R")

        let parseCarNumber carNumber = 
            match carNumber.ToString().Contains("J") || carNumber.ToString().Contains("H") with
            | true when carNumber.ToString().Length = 3 && carNumber.ToString().Contains("J") -> carNumber.ToString().Replace("J","3")
            | true when carNumber.ToString().Length = 2 && carNumber.ToString().Contains("J") -> carNumber.ToString().Replace("J","30")
            | true when carNumber.ToString().Length = 3 && carNumber.ToString().Contains("H") -> carNumber.ToString().Replace("H","2")
            | true when carNumber.ToString().Length = 2 && carNumber.ToString().Contains("H") -> carNumber.ToString().Replace("H","20")
            | _ -> carNumber.ToString()
            
        
        let queryString = sprintf "INSERT INTO \"Entrants\" VALUES ('%i',%i,'%s','%s','%s','%s','%s','%s','%b')" (Event.Id)
                                                                                                                 (Convert.ToInt32(competitor.carNumber.Replace("R","").Trim() |> parseCarNumber)) 
                                                                                                                 (competitor.driver.Replace("'"," ")) 
                                                                                                                 (competitor.codriver.Replace("'"," ")) 
                                                                                                                 (competitor.address) 
                                                                                                                 (competitor.car) 
                                                                                                                 (competitor.category) 
                                                                                                                 (stageTimes)
                                                                                                                 (superRally)
        let command = new NpgsqlCommand(queryString, connection)
        let dataReader = command.ExecuteNonQuery()
//        log Level.Info (competitor.carNumber.Replace("R",""))      
        ()


module Main =

    open System
    open FSharp.Data
//    open Logger
    open Types
    open Npgsql


    let start =
//        log Level.Debug "Staring Replay"


        let StageToSeconds(str:string) = 
            let min = str.Substring(0,str.LastIndexOf(":"))
            let sec = str.Substring(str.LastIndexOf(":") + 1,4)
            let total = (60.0 * Convert.ToDouble(min) + Convert.ToDouble(sec))
            total


        let PenaltyToSeconds(str:string) = 
            let min = str.Substring(0,str.LastIndexOf(":"))
            let sec = str.Substring(str.LastIndexOf(":") + 1,2)
            let total = (60.0 * Convert.ToDouble(min) + Convert.ToDouble(sec))
            total


        let Starters =
            let results = HtmlDocument.Load("http://results.shannonsportsit.ie/entries.php?rally=" + Event.Code)
            let body = results.Descendants ["a"] 
            body
            |> Seq.skip 2
            |> Seq.filter (fun x -> x.InnerText().Contains("Back to Index") |> not)
            |> Seq.choose (fun x -> 
                    x.TryGetAttribute("href")
                    |> Option.map (fun a -> a.Value())
                    |> Option.map(fun x -> x.Substring(x.LastIndexOf("=")+1, x.Length - (x.LastIndexOf("=")+1)))
            )
        

        let EntrantInformation = Starters |> Seq.map(fun index ->
                let results = HtmlDocument.Load("http://results.shannonsportsit.ie/competitor.php?rally=" + Event.Code + "&entrant=" + index.ToString())
                let body = results.Descendants["td"]
                let DriverInformation = 
                    body 
                    |> Seq.map(fun x -> x.InnerText())
                    |> Seq.filter (fun (x) -> x <> "Driver" &&
                                                x <> "Codriver" &&
                                                x <> "Car Number" &&
                                                x <> "Make" &&
                                                x <> "Address" &&
                                                x <> "Class")
                    |> Seq.take 6

                let StageInformation = 
                    body 
                    |> Seq.skip 13
                    |> Seq.mapi(fun i x -> i, x.InnerText())
                    |> Seq.filter (fun (x,_) -> x % 6 = 0)
                    |> Seq.takeWhile (fun (_,y) -> y <> "")
                    |> Seq.map(fun (x,y) -> y |> StageToSeconds)

                let Penalty = 
                    body 
                    |> Seq.skip 15
                    |> Seq.mapi(fun i x -> i, x.InnerText())
                    |> Seq.filter (fun (x,_) -> x % 6 = 0)
                    |> Seq.takeWhile (fun (_,y) -> y <> "")
                    |> Seq.map(fun (x,y) -> y |> PenaltyToSeconds)

                let PenaltyInformation = 
                    Penalty |> Seq.mapi(fun stage stagePenaltyTime -> 
                        match stage with
                        | 0 -> stagePenaltyTime
                        | _ -> 
                            match (Penalty |> Seq.nth (stage - 1)) = stagePenaltyTime with
                            | true -> 0.0
                            | false -> stagePenaltyTime
                    )
                DriverInformation, StageInformation, PenaltyInformation 
        )


        let CompetitorEventInformation = 
            EntrantInformation |> Seq.map(fun (driverInformation:seq<string>, y:seq<float>, z:seq<float>) ->
                try
                    let stageAndPenaltyTimes = Seq.map2(fun (y:float) (penalty:float) -> (y), (penalty)) <| y <| z
                    let info = {carNumber = driverInformation |> Seq.nth 0;
                                driver =  driverInformation |> Seq.nth 1;
                                codriver = driverInformation |> Seq.nth 2;
                                address = driverInformation |> Seq.nth 3;
                                car = driverInformation |> Seq.nth 4;
                                category = driverInformation |> Seq.nth 5;
                                stageData = stageAndPenaltyTimes }
                    info        
                with
                    | :? System.InvalidOperationException as ex -> 
                    let info = {carNumber = driverInformation |> Seq.nth 0;
                                driver =  driverInformation |> Seq.nth 1;
                                codriver = driverInformation |> Seq.nth 2;
                                address = driverInformation |> Seq.nth 3;
                                car = driverInformation |> Seq.nth 4;
                                category = driverInformation |> Seq.nth 5;
                                stageData = Seq.empty }
                    info
                )

        let Connection = new Npgsql.NpgsqlConnection(Database.connectionString)
        Connection.Open()
//        log Level.Info "Opening db connection"
        CompetitorEventInformation |> Seq.iter(fun eventInformation -> Database.testDB(eventInformation, Connection))
        //CompetitorEventInformation |> Seq.iter(fun eventInformation -> printfn "%A" eventInformation)
        Connection.Close()
//        log Level.Info "Closed db connection"
//        Logger.log Level.Debug "Finished Replay"

        0 // return an integer exit code
