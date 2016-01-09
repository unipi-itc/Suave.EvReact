﻿#I __SOURCE_DIRECTORY__

#r @"packages\Suave.1.0.0\lib\net40\Suave.dll"
#r @"packages\evReact.0.9.0\lib\evReact.dll"

#load "Suave.EvReact.fs"
open Suave.EvReact

open Suave
open Suave.Http
open Suave.Successful
open Suave.Web
open Suave.EvReact
open EvReact
open EvReact.Expr

// Create the EvReact events associated with URLs
let start = HttpEvent()
let work = HttpEvent()
let stop = HttpEvent()
let status = HttpEvent()

let jobs = ResizeArray<string>()

// chooseEvents is the only combiner currently featured by Suave.EvReact
// The list is (regex, event, default)
// Whenever the regex is matched by Suave the event is fired. 
// The default web part can be overridden by assigining the Result property
// in the event

// In this example we have jobs that are started by accessing /start/id
// You perform some work only if the job is running with /work/id/arg
// You stop the job using /stop/id
let app = chooseEvents 
            [
                ("/start/(\\d+)", start, NO_CONTENT)
                ("/work/(\\d+)/(\\d+)", work, NO_CONTENT)
                ("/stop/(\\d+)", stop, NO_CONTENT)
                ("/status", status, NO_CONTENT)
            ]

// This EvReact net simply react to the status event by printing the list of jobs
let statusReq = !!status.Publish |-> (fun arg -> arg.Result <- OK (System.String.Join("<br/>", jobs)))

// Useful net generator expressing a loop until
let loopUntil terminator body = +( body / terminator ) - never

// The orchestrator used to run the nets
let orch = EvReact.Orchestrator.create()

// When start is received the function gets triggered
let startNet = !!start.Publish |-> (fun arg ->
  // Read the id from the argument
  let id = arg.Match.Groups.[1].Value
  jobs.Add(id)

  // Set the response
  arg.Result <- OK (sprintf "Started job %s" id)
  
  // The net performing the actual work is triggered only if the id is the one started
  let doWork = (work.Publish %- (fun arg -> arg.Match.Groups.[1].Value = id)) |-> (fun arg ->
    let value = int(arg.Match.Groups.[2].Value)
    arg.Result <- OK ((value + 1).ToString())
  )
  
  // We get the stop event and only if relates to the current id trigger the stopNet event
  let stopNet = HttpEvent()
  let stopThis = (stop.Publish %- (fun arg -> arg.Match.Groups.[1].Value = id))
                 |-> (fun arg -> arg.Result <- OK(sprintf "Job %s done" id)
                                 jobs.Remove(id) |> ignore 
                                 stopNet.Trigger(arg)
                     )
  // Start a net listening for the stop event
  Expr.start HttpEventArgs.Empty orch stopThis |> ignore

  // Net looping forever unless the stopNet event fires
  let net = (loopUntil [|stopNet.Publish|] doWork)

  // Starts the net
  Expr.start HttpEventArgs.Empty orch net |> ignore
)

// Starts the startNet and statusReq nets looping forever
Expr.start HttpEventArgs.Empty orch (+startNet)
Expr.start HttpEventArgs.Empty orch (+statusReq)
  
// Starts Suave
startWebServer defaultConfig app

