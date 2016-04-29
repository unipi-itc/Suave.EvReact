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


    let makeJsonEventArgs (ctx, pat, m) =
      let txt = System.Text.Encoding.ASCII.GetString(ctx.request.rawForm)
      let o = JToken.Parse(txt)
      JsonEventArgs(ctx, o, pat, m)

    let asyncTrigger (e:EvReact.Event<_>) args =
      async { e.Trigger(args) } |> Async.Start |> ignore


    let contentType t =
      fun ctx ->
        if ctx.request.header("content-type") = Choice1Of2(t) then
          succeed ctx
        else
          fail

    let webapi_react makeArgs pat evt =
      fun ctx ->
        let m = Regex.Match(ctx.request.url.AbsolutePath, pat)
        if m.Success then
          try
            let args : #HttpEventArgs = makeArgs (ctx, pat, m)
            asyncTrigger evt args
            args.Result ctx
          with _ -> RequestErrors.BAD_REQUEST "Malformed data" ctx
        else
          RequestErrors.FORBIDDEN "" ctx

    let http_react (pat,evt) =
      webapi_react HttpEventArgs pat evt

    let json_react (pat,evt) =
      POST
      >=> contentType "application/json"
      >=> webapi_react makeJsonEventArgs pat evt

    let chooseEvents (evts : HttpEventBind list) =
      evts |> List.map http_react |> choose
