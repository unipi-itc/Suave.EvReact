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

    type MsgContext =
      internal new : Uri * Suave.Sockets.SocketBinding -> MsgContext
      member Uri : Uri
      member Source : Suave.Sockets.SocketBinding

    type MsgRequestEventArgs<'T> =
      inherit ResponseEventArgs
      internal new : MsgContext * 'T -> MsgRequestEventArgs<'T>
      member Context : MsgContext
      member Message : 'T

    val httpReact : unit -> WebPart * IEvent<HttpEventArgs>
    val msgReact : unit -> WebPart * IEvent<MsgRequestEventArgs<'T>>

    val defaultSendJson : Uri -> byte[] -> unit
    val createRemoteTrigger : (byte[] -> unit) -> ('T -> unit)
    val createRemoteIEvent : unit -> WebPart * IEvent<'T>
