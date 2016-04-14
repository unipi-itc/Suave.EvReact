namespace Suave

module EvReact =
    open Suave
    open Suave.Sscanf
    open Suave.Http
    open Suave.Operators
    open Suave.EventSource
    open Suave.Filters
    open System.Text.RegularExpressions
    open Newtonsoft.Json.Linq

    type HttpEventArgs(h:HttpContext, path:string, m:Match, def:WebPart) =
        inherit System.EventArgs()

        let mutable result : WebPart = def

        member this.Context = h
        member this.Path = path
        member this.Match = m
        member this.Result with get() = result and set(v) = result <- v

        static member Empty = new HttpEventArgs(HttpContext.empty, null, null, never)

    type JsonEventArgs(h:HttpContext, o:JToken, path:string, m:Match, def:WebPart) =
        inherit HttpEventArgs(h, path, m, def)

        member this.Object = o

        static member Empty = new JsonEventArgs(HttpContext.empty, null, null, null, never)

    type HttpEvent = EvReact.Event<HttpEventArgs>
    type JsonEvent = EvReact.Event<JsonEventArgs>

    type HttpEventBind = string*HttpEvent*WebPart
    type JsonEventBind = string*JsonEvent*WebPart

    let http_react (evt : HttpEventBind) =
      let pat, e, def = evt 
      fun (h:HttpContext) ->
        let m = Regex.Match(h.request.url.AbsolutePath, pat)
        if m.Success then
          let evt = HttpEventArgs(h, pat, m, def)
          e.Trigger(evt)
          evt.Result(h)
        else
          fail

    let contentType (t:string) (arg:HttpContext) =
      async {
        match arg.request.header("content-type") with
        | Choice1Of2 v ->
          if v = t then
            return Some arg
          else
            return None
        | Choice2Of2 v ->
          return None
      }

    let json_react (evt : JsonEventBind) =
      let pat, e, def = evt 
      POST
      >=>
      contentType "application/json"
      >=>
      (fun (h:HttpContext) ->
        async {
          let m = Regex.Match(h.request.url.AbsolutePath, pat)
          if m.Success then
            let txt = System.Text.Encoding.ASCII.GetString(h.request.rawForm)
            try 
              let o = JToken.Parse(txt)
              let evt = JsonEventArgs(h, o, pat, m, def)
              e.Trigger(evt)
              return! evt.Result h
            with _ -> return None
          else
            return! fail
        })

    let chooseEvents (evts:HttpEventBind list) : WebPart =
        evts 
        |> List.map http_react 
        |> choose
