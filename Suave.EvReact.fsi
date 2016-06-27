namespace Suave
  module EvReact =
    open System
    open Suave.Http
    open System.Text.RegularExpressions

    type HttpEventArgs =
      inherit EventArgs
      internal new : HttpContext * string * Match -> HttpEventArgs
      member Context : HttpContext
      member Match : Match
      member Path : string
      member Result : WebPart with get, set

    type HttpEvent = EvReact.Event<HttpEventArgs>

    type HttpEventBind = string * HttpEvent

    val contentType : string -> WebPart

    val http_react : HttpEventBind -> WebPart

    val chooseEvents : HttpEventBind list -> WebPart

    val defaultSendJson : Uri -> byte[] -> unit
    val createRemoteTrigger : (byte[] -> unit) -> ('T -> unit)
    val createRemoteIEvent : unit -> WebPart * IEvent<'T>
