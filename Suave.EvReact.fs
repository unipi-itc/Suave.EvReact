namespace Suave

module EvReact =
    open Suave
    open Suave.Sscanf
    open Suave.Http
    open Suave.EventSource
    open System.Text.RegularExpressions

    type HttpEventArgs(h:HttpContext, path:string, m:Match, def:WebPart) =
        inherit System.EventArgs()

        let mutable result : WebPart = def

        member this.Context = h
        member this.Path = path
        member this.Match = m
        member this.Result with get() = result and set(v) = result <- v

        static member Empty = new HttpEventArgs(HttpContext.empty, null, null, never)

    type HttpEvent = EvReact.Event<HttpEventArgs>

    type HttpEventBind = string*HttpEvent*WebPart

    let chooseEvents (evts:HttpEventBind list) : WebPart =
        evts 
        |> List.map (fun (pat, e, def) -> 
                        fun (h:HttpContext) ->
                            let m = Regex.Match(h.request.url.AbsolutePath, pat)
                            if m.Success then
                                let evt = HttpEventArgs(h, pat, m, def)
                                e.Trigger(evt)
                                evt.Result(h)
                            else
                                fail
                    ) 
        |> choose
