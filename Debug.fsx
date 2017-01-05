
#I __SOURCE_DIRECTORY__

#r @"packages/Suave.2.0.0/lib/net40/Suave.dll"
#r @"packages/Newtonsoft.Json.9.0.1/lib/net40/Newtonsoft.Json.dll"

#load "Suave.EvReact.fs"
open Suave.EvReact

open Suave
open Suave.Http
open Suave.Successful
open Suave.Web
open Suave.Operators

let wp, ev = createRemoteIEvent<int*int>()
// Starts Suave
startWebServer defaultConfig (choose [ Filters.path "/poster" >=> wp; Files.browse __SOURCE_DIRECTORY__ ])
