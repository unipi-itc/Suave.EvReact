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

    type HttpEvent = EvReact.Event<HttpEventArgs>
    type HttpEventBind = string*HttpEvent

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
          async.Return(None)

    let http_react (pat,evt) =
      webapi_react HttpEventArgs pat evt

    let chooseEvents (evts : HttpEventBind list) =
      evts |> List.map http_react |> choose

    let serializeJSON x =
      let json = Newtonsoft.Json.JsonConvert.SerializeObject(x)
      System.Text.Encoding.UTF8.GetBytes(json)

    let deserializeJSON data =
      let json = System.Text.Encoding.UTF8.GetString(data)
      Newtonsoft.Json.JsonConvert.DeserializeObject<_>(json)

    let defaultSendJson uri data =
      let client = new System.Net.WebClient()
      client.Headers.[System.Net.HttpRequestHeader.ContentType] <- "application/json"
      client.UploadDataAsync(uri, data)

    let createRemoteTrigger sendJson =
      serializeJSON >> sendJson : _ -> unit

    let createRemoteIEvent () =
      let e = EvReact.Event()
      let handleJson ctx =
        try
          let x = deserializeJSON(ctx.request.rawForm)
          asyncTrigger e x
          Successful.OK "" ctx
        with _ -> RequestErrors.BAD_REQUEST "Malformed data" ctx
      let webpart =
        POST
        >=> contentType "application/json"
        >=> handleJson
      webpart,e.Publish
