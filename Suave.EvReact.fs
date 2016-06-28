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

    type MsgContext(u: System.Uri, src: Suave.Sockets.SocketBinding) =
      member this.Uri = u
      member this.Source = src

    type MsgRequestEventArgs<'T>(ctxt: MsgContext, m : 'T) =
      inherit ResponseEventArgs()
      member this.Context = ctxt
      member this.Message = m

    type HttpEventArgs(h:HttpContext) =
        inherit ResponseEventArgs()
        member this.Context = h

    let asyncTrigger (e:Event<_>) args =
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

    let defaultSendJson (uri: System.Uri) data =
      let client = new System.Net.WebClient()
      client.Headers.[System.Net.HttpRequestHeader.ContentType] <- "application/json"
      client.UploadData(uri, data) |> ignore

    let createRemoteTrigger sendJson =
      serializeJSON >> sendJson : _ -> unit

    let httpReact () =
      let e = Event<_>()
      let webpart ctx =
        let args = HttpEventArgs(ctx)
        asyncTrigger e args
        args.Result ctx
      webpart,e.Publish

    let handleJson (handler: _ -> WebPart) : WebPart =
      fun ctx ->
        try
          let x = deserializeJSON(ctx.request.rawForm)
          handler x ctx
        with _ -> fail

    let jsonReact handle =
      POST <|> RequestErrors.METHOD_NOT_ALLOWED "POST is the only accepted method"
      >=> contentType "application/json" <|> RequestErrors.BAD_REQUEST "Invalid content type"
      >=> handleJson handle <|> RequestErrors.BAD_REQUEST "Malformed data"

    let msgReact () =
      let e = Event<_>()
      let handle x ctx =
        let msgCtx = MsgContext(ctx.request.url, ctx.connection.socketBinding)
        let args = MsgRequestEventArgs(msgCtx, x)
        asyncTrigger e args
        args.Result ctx
      (jsonReact handle, e.Publish)

    let createRemoteIEvent () =
      let e = Event<_>()
      let handle x =
        asyncTrigger e x
        Successful. OK ""
      (jsonReact handle, e.Publish)
