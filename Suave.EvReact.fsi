namespace Suave
  module EvReact =
    open System

    type ResponseEventArgs =
      inherit EventArgs
      internal new : int option -> ResponseEventArgs
      member Result : WebPart with get, set

    type HttpEventArgs =
      inherit ResponseEventArgs
      internal new : HttpContext * int option -> HttpEventArgs
      member Context : HttpContext

    type MsgContext =
      internal new : Uri * Suave.Sockets.SocketBinding -> MsgContext
      member Uri : Uri
      member Source : Suave.Sockets.SocketBinding

    type MsgRequestEventArgs<'T> =
      inherit ResponseEventArgs
      internal new : MsgContext * 'T * int option -> MsgRequestEventArgs<'T>
      member Context : MsgContext
      member Message : 'T

    val httpResponse : int option -> WebPart * IEvent<HttpEventArgs>
    val msgResponse : int option -> WebPart * IEvent<MsgRequestEventArgs<'T>>
    val msgReact : unit -> WebPart * IEvent<MsgContext * 'T>

    val defaultSendJson : Uri -> byte[] -> unit
    val createRemoteTrigger : (byte[] -> unit) -> ('T -> unit)
    val createRemoteIEvent : unit -> WebPart * IEvent<'T>
