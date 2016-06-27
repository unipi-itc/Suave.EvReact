namespace Suave
  module EvReact =
    open System

    type ResponseEventArgs =
      inherit EventArgs
      internal new : unit -> ResponseEventArgs
      member Result : WebPart with get, set

    type HttpEventArgs =
      inherit ResponseEventArgs
      internal new : HttpContext -> HttpEventArgs
      member Context : HttpContext

    val httpReact : unit -> WebPart * IEvent<HttpEventArgs>

    val defaultSendJson : Uri -> byte[] -> unit
    val createRemoteTrigger : (byte[] -> unit) -> ('T -> unit)
    val createRemoteIEvent : unit -> WebPart * IEvent<'T>
