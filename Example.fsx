#I __SOURCE_DIRECTORY__

#r @"packages/Suave.2.0.0/lib/net40/Suave.dll"
#r @"packages/Newtonsoft.Json.9.0.1/lib/net40/Newtonsoft.Json.dll"
#r "evReact.dll"

#load "Suave.EvReact.fs"
open Suave.EvReact

open Suave
open Suave.Http
open Suave.Successful
open Suave.Web
open Suave.Operators
open System.Text.RegularExpressions

open EvReact
open EvReact.Expr

let regex (pattern:string) (arg:HttpContext) =
    async {
      if Regex.IsMatch(arg.request.url.AbsolutePath, pattern) then
        return Some arg
      else
        return None
    }

let rexm (pattern:string) (ctx:HttpEventArgs) =
  Regex.Match(ctx.Context.request.url.AbsolutePath, pattern)

// Create the EvReact events associated with URLs
let startp, (startwp, start) = "/start/(\\d+)", httpResponse(Some 1000)
let workp, (workwp, work) = "/work/(\\d+)/(\\d+)", httpResponse(Some 1000)
let stopp, (stopwp, stop) = "/stop/(\\d+)", httpResponse(Some 1000)
let statusp, (statuswp, status) = "/status", httpResponse(Some 1000)

let jobs = ResizeArray<string>()

type FooRec = { Foo: string; Bar: int[]; Baz: option<FooRec> }

let es,trigger = createJsonEventSource "exampleES"

async {
  while true do
    trigger { Foo = "hello";
              Bar = [| 42; 1; 2; 3 |]
              Baz = Some { Foo = "world"; Bar = [||]; Baz = None }
            }
    System.Threading.Thread.Sleep(1000)
} |> Async.Start

let fullPath = System.IO.Path.GetFullPath("static")

// In this example we have jobs that are started by accessing /start/id
// You perform some work only if the job is running with /work/id/arg
// You stop the job using /stop/id
let app = choose
            [
                regex startp >=> startwp
                regex workp >=> workwp
                regex stopp >=> stopwp
                regex statusp >=> statuswp
                Filters.path "/events" >=> es
                Filters.GET >=> Files.browse fullPath //serves file if exists
                Filters.GET >=> Files.dir fullPath //show directory listing
            ]

// This EvReact net simply react to the status event by printing the list of jobs
let statusReq = !!status |-> (fun (arg:HttpEventArgs) -> arg.Result <- OK (System.String.Join("<br/>", jobs)))

// Useful net generator expressing a loop until
let loopUntil terminator body = +( body / terminator ) - never

// The orchestrator used to run the nets
let orch = EvReact.Orchestrator.create()

// When start is received the function gets triggered
let startNet = !!start |-> (fun arg ->
  let m = rexm startp arg
  // Read the id from the argument
  let id = m.Groups.[1].Value
  jobs.Add(id)

  // Set the response
  arg.Result <- OK (sprintf "Started job %s" id)
  
  // The net performing the actual work is triggered only if the id is the one started
  let doWork = (work %- (fun arg -> let m = rexm workp arg in m.Groups.[1].Value = id)) |-> (fun arg ->
    let m = rexm workp arg
    let value = int(m.Groups.[2].Value)
    arg.Result <- OK ((value + 1).ToString())
  )
  
  // We get the stop event and only if relates to the current id trigger the stopNet event
  let stopNet = Event.create<HttpEventArgs>("stopNet")
  let stopThis = (stop %- (fun _ -> let m = rexm stopp arg in m.Groups.[1].Value = id))
                 |-> (fun (arg:HttpEventArgs) ->
                        jobs.Remove(id) |> ignore
                        stopNet.Trigger(arg)
                        arg.Result <- OK(sprintf "Job %s done" id)
                     )
  // Start a net listening for the stop event
  Expr.start Unchecked.defaultof<_> orch stopThis |> ignore

  // Net looping forever unless the stopNet event fires
  let net = (loopUntil [|stopNet.Publish|] doWork)

  // Starts the net
  Expr.start Unchecked.defaultof<_> orch net |> ignore
)

// Starts the startNet and statusReq nets looping forever
Expr.start Unchecked.defaultof<_> orch (+startNet)
Expr.start Unchecked.defaultof<_> orch (+statusReq)
  
// Starts Suave
startWebServer defaultConfig app

