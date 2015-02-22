namespace ViewModels

open VkAudio

type AuthViewModel (vkClient:VkAudio) = 
    inherit ViewModelBase()
    
    let vk = vkClient
    let closeRequested = new Event<_>()
    let mutable cancelled = false
    let mutable currentUri = vk.GetLink
    
    do vk.AuthSetted.Add(closeRequested.Trigger)
    
    [<CLIEvent>]
    member this.OnClose = closeRequested.Publish

    member this.ClearCookie() =
        this.CurrentUri <-
            "javascript:void((function(){var a,b,c,e,f;f=0;a=document.cookie.split('; ');"
            + "for(e=0;e<a.length&&a[e];e++){f++;for(b='.'+location.host;b;b=b.replace(/^(?:%5C.|[^%5C.]+)/,''))"
            + "{for(c=location.pathname;c;c=c.replace(/.$/,'')){document.cookie=(a[e]+'; domain='+b+'; "
            + "path='+c+'; expires='+new Date((new Date()).getTime()-1e11).toGMTString());}}}})())"

    member this.Cancelled
        with get () = cancelled
        and set value = cancelled <- value
                        this.OnPropertyChanged "Cancelled"

    member this.CurrentUri
        with get () = currentUri
        and set value = currentUri <- value
                        this.OnPropertyChanged "CurrentUri"

    member this.Close = 
        new RelayCommand ((fun _ -> true), (fun _ -> this.Cancelled <- true; closeRequested.Trigger()))

    member this.Navigate =
        new RelayCommand ((fun _ -> true), (fun url -> System.Diagnostics.Process.Start(url.ToString()) |> ignore))

    member this.TryAuth =
        new RelayCommand ((fun _ -> true), (fun src -> 
            //this.ClearCookie()
            let uri = new System.Uri(src.ToString())
            if uri.Fragment.Length > 0 then vk.SetAuth uri.Fragment))
    