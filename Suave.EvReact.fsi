namespace Suave
  module EvReact =
    open System
    open Suave.Http
    open System.Text.RegularExpressions
    open Newtonsoft.Json.Linq

    type HttpEventArgs =
      inherit EventArgs
      internal new : HttpContext * string * Match -> HttpEventArgs
      member Context : HttpContext
      member Match : Match
      member Path : string
      member Result : WebPart with get, set

    type JsonEventArgs =
      inherit HttpEventArgs
      internal new : HttpContext * JToken * string * Match -> JsonEventArgs
      member Object : JToken

    type HttpEvent = EvReact.Event<HttpEventArgs>
    type JsonEvent = EvReact.Event<JsonEventArgs>

    type HttpEventBind = string * HttpEvent
    type JsonEventBind = string * JsonEvent

    val contentType : string -> WebPart

    val http_react : HttpEventBind -> WebPart
    val json_react : JsonEventBind -> WebPart

    val chooseEvents : HttpEventBind list -> WebPart

    val defaultSendJson : Uri -> byte[] -> unit
    val createRemoteTrigger : (byte[] -> unit) -> ('T -> unit)
    val createRemoteIEvent : unit -> WebPart * IEvent<'T>
