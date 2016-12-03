namespace ViewModels

open VkAudio

type AuthViewModel (vkClient:VkAudio) = 
    inherit ViewModelBase()
    
    let vk = vkClient
    let closeRequested = new Event<_>()
    let mutable cancelled = false
    let mutable currentUri = vk.GetLink()
    
    do vk.AuthSetted.Add(closeRequested.Trigger)
    
    [<CLIEvent>]
    member this.OnClose = closeRequested.Publish

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
            let uri = new System.Uri(src.ToString())
            if uri.Fragment.Length > 0 then vk.SetAuth uri.Fragment))
    