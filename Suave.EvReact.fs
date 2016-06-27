namespace Suave

module EvReact =
    open Suave
    open Suave.Http
    open Suave.Operators
    open Suave.EventSource
    open Suave.Filters
    open System.Threading

    type ResponseEventArgs() =
      inherit System.EventArgs()
      let mutable result : Option<WebPart> = None
      let waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset)

      member this.Result with get()  = waitHandle.WaitOne() |> ignore
                                       waitHandle.Dispose()
                                       match result with
                                       | None -> ServerErrors.INTERNAL_ERROR "Inconsistent internal state"
                                       | Some v -> v

                          and set(v) = match result with
                                       | None -> result <- Some v
                                       | Some _ -> failwith "Result already set"
                                       waitHandle.Set() |> ignore

    type HttpEventArgs(h:HttpContext) =
        inherit ResponseEventArgs()
        member this.Context = h

    type HttpEvent = EvReact.Event<HttpEventArgs>

    let asyncTrigger (e:EvReact.Event<_>) args =
      async { e.Trigger(args) } |> Async.Start |> ignore

    let contentType t =
      fun ctx ->
        if ctx.request.header("Content-Type") = Choice1Of2(t) then
          succeed ctx
        else
          fail

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

    let httpReact () =
      let e = EvReact.Event()
      let webpart ctx =
        let args = HttpEventArgs(ctx)
        asyncTrigger e args
        args.Result ctx
      webpart,e.Publish

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
