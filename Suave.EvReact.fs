namespace Suave

module EvReact =
    open Suave
    open Suave.Sscanf
    open Suave.Http
    open Suave.Operators
    open Suave.EventSource
    open Suave.Filters
    open System.Threading
    open System.Text.RegularExpressions
    open Newtonsoft.Json.Linq

    type HttpEventArgs (h:HttpContext, path:string, m:Match) =
        inherit System.EventArgs()

        let mutable result : Option<WebPart> = None
        let waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset)

        member this.Context = h
        member this.Path = path
        member this.Match = m
        member this.Result with get()  = waitHandle.WaitOne() |> ignore
                                         waitHandle.Dispose()
                                         match result with
                                         | None -> Suave.ServerErrors.INTERNAL_ERROR "Inconsistent internal state"
                                         | Some v -> v

                            and set(v) = match result with
                                         | None -> result <- Some v
                                         | Some _ -> failwith "Result already set"
                                         waitHandle.Set() |> ignore

    type JsonEventArgs (h:HttpContext, o:JToken, path:string, m:Match) =
        inherit HttpEventArgs(h, path, m)

        member this.Object = o

    type HttpEvent = EvReact.Event<HttpEventArgs>
    type JsonEvent = EvReact.Event<JsonEventArgs>

    type HttpEventBind = string*HttpEvent
    type JsonEventBind = string*JsonEvent

    let http_react (evt : HttpEventBind) =
      let pat, e = evt
      fun (h:HttpContext) ->
        let m = Regex.Match(h.request.url.AbsolutePath, pat)
        if m.Success then
          let evt = HttpEventArgs(h, pat, m)
          async { e.Trigger(evt) } |> Async.Start |> ignore
          evt.Result(h)
        else
          fail


    let contentType t =
      fun ctx ->
        if ctx.request.header("content-type") = Choice1Of2(t) then
          succeed ctx
        else
          fail

    let json_react (evt : JsonEventBind) =
      let pat, e = evt
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
              let evt = JsonEventArgs(h, o, pat, m)
              async { e.Trigger(evt) } |> Async.Start |> ignore
              return! evt.Result h
            with _ -> return None
          else
            return! fail
        })

    let chooseEvents (evts:HttpEventBind list) : WebPart =
        evts 
        |> List.map http_react 
        |> choose
