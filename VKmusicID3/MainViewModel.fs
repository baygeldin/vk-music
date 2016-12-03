namespace ViewModels

open System
open System.Windows
open System.Windows.Forms
open System.ComponentModel
open System.Collections
open System.Threading
open System.Threading.Tasks
open System.Text.RegularExpressions
open System.Windows.Media
open System.Diagnostics
open System.Net
open System.IO
open VkAudio

module Helper =
    let mainThread func = Application.Current.Dispatcher.Invoke((new Action<_>(func)), [null]) |> ignore
    let awaitTask = Async.AwaitIAsyncResult >> Async.Ignore

type CheckableItem<'T> (value : 'T, isChecked : bool) =
    inherit ViewModelBase()

    let mutable isChecked = isChecked

    member this.Value
        with get () = value 
    
    member this.IsChecked
        with get () = isChecked
        and set value = isChecked <- value
                        this.OnPropertyChanged "IsChecked"

type MainViewModel (vkClient:VkAudio) as this = 
    inherit ViewModelBase()
    
    let vk = vkClient
    
    let songsLoaded = new Event<_>()
    let loadStarted = new Event<_>()
    let loadCompleted = new Event<_>()
    let aboutRequested = new Event<_>()
    let authRequested = new Event<_>()
    let logoutCompleted = new Event<_>()
    let logoutRequested = new Event<_>()
    let shutdownRequested = new Event<_>()
    let mutable (songs:CheckableItem<Song> list) = []
    let mutable format = "NUMX ARTIST - SONG"
    let mutable lyrics = true
    let mutable album = true
    let mutable folder = null
    let mutable name = null
    let mutable amount = 0
    let mutable downloaded = 0

    do vk.AuthSetted.Add(fun _ -> this.Name <- vk.GetName)

    [<CLIEvent>]
    member this.SongsLoaded = songsLoaded.Publish
    
    [<CLIEvent>]
    member this.LoadCompleted = loadCompleted.Publish

    [<CLIEvent>]
    member this.LoadStarted = loadStarted.Publish

    [<CLIEvent>]
    member this.AboutRequested = aboutRequested.Publish

    [<CLIEvent>]
    member this.AuthRequested = authRequested.Publish

    [<CLIEvent>]
    member this.LogoutRequested = logoutRequested.Publish

    [<CLIEvent>]
    member this.LogoutCompleted = logoutCompleted.Publish

    [<CLIEvent>]
    member this.ShutdownRequested = shutdownRequested.Publish

    member this.Name
        with get () = name
        and set value = name <- value
                        this.OnPropertyChanged "Name"

    member this.Amount
        with get () = amount
        and set value = amount <- value
                        this.OnPropertyChanged "Amount"

    member this.Downloaded
        with get () = downloaded
        and set value = downloaded <- value
                        this.OnPropertyChanged "Downloaded"

    member this.Lyrics
        with get () = lyrics
        and set value = lyrics <- value
                        this.OnPropertyChanged "Lyrics"

    member this.Album
        with get () = album
        and set value = album <- value
                        this.OnPropertyChanged "Album"

    member this.Folder
        with get () = folder
        and set value = folder <- value
                        this.OnPropertyChanged "Folder"
    
    member this.Format
        with get () = format
        and set value = format <- value
                        this.OnPropertyChanged "Format"

    member this.SongList
        with get () = songs
        and set value = songs <- value
                        this.OnPropertyChanged "SongList"
 
    member this.SelectFile = 
        let selectFile() = 
            let folderDialog = new FolderBrowserDialog()
            if folderDialog.ShowDialog() = DialogResult.OK then 
                this.Folder <- folderDialog.SelectedPath
        new RelayCommand ((fun _ -> true), (fun _ -> selectFile()))
    
    member this.GenerateList = 
        let generateList() = 
            async { 
                let! list = vk.GetSongsAsync()
                this.SongList <- List.map (fun a -> new CheckableItem<Song>(a, true)) list
                (fun _ -> songsLoaded.Trigger()) |> Helper.mainThread
            } |> Async.Start
        new RelayCommand ((fun _ -> true), (fun _ -> generateList()))

    member this.InvertSelecting = 
        new RelayCommand ((fun _ -> true), (fun _ -> 
            List.map (fun (a:CheckableItem<Song>) -> a.IsChecked <- not a.IsChecked; a) this.SongList |> ignore))
    
    member this.LogOut = 
        new RelayCommand ((fun _ -> true), (fun _ ->
             let web = new System.Windows.Forms.WebBrowser()
             web.DocumentCompleted.Add(fun _ -> 
                async {
                    web.Navigate("javascript:void((function(){var a,b,c,e,f;f=0;a=document.cookie.split('; ');"
                        + "for(e=0;e<a.length&&a[e];e++){f++;for(b='.'+location.host;b;b=b.replace(/^(?:%5C.|[^%5C.]+)/,''))"
                        + "{for(c=location.pathname;c;c=c.replace(/.$/,'')){document.cookie=(a[e]+'; domain='+b+'; "
                        + "path='+c+'; expires='+new Date((new Date()).getTime()-1e11).toGMTString());}}}})())")
                    do! Task.Delay(1500) |> Helper.awaitTask // Hack to await logout
                    (fun _ -> logoutCompleted.Trigger()) |> Helper.mainThread
                } |> Async.Start
             )
             logoutRequested.Trigger()
             web.Navigate("https://vk.com/")
        ))

    member this.ShowAbout = 
        new RelayCommand ((fun _ -> true), (fun _ -> aboutRequested.Trigger()))

    member this.ShowAuth = 
        new RelayCommand ((fun _ -> true), (fun _ -> authRequested.Trigger()))

    member this.Shutdown = 
        new RelayCommand ((fun _ -> true), (fun _ -> shutdownRequested.Trigger()))

    member this.DownloadSongs =
        let mutex = new SemaphoreSlim(1)
        let progressChange() =
            async.Bind(mutex.WaitAsync()  |> Helper.awaitTask, (fun _ -> async.Zero())) |> ignore
            this.Downloaded <- this.Downloaded + 1
            if (this.Downloaded = this.Amount) then 
                this.Downloaded <- 0
                this.Amount <- 0
                (fun _ -> loadCompleted.Trigger()) |> Helper.mainThread
            mutex.Release() |> ignore
        let uncheck (song:Song) =
            (List.find (fun (a:CheckableItem<Song>) -> a.Value = song) this.SongList).IsChecked <- false
        let rec downloadQueueAsync (list:Song list) =
            let processSong (song:Song) = 
                let web = new WebClient()
                let signal = new SemaphoreSlim(0, 1)
                let filename =
                    format
                        .Replace("ARTIST", song.Artist)
                        .Replace("SONG", song.Title)
                        .Replace("NUMX", song.Number.ToString()
                            .PadLeft(this.Amount.ToString().Length, '0'))
                        .Replace("NUM", song.Number.ToString())
                    |> (fun x -> Regex.Replace(x, "[\\/?:*\"\"><|]+", ""))
                let audioFileName = folder + "\\" + filename + ".mp3"
                let lyricsFileName = folder + "\\Lyrics\\" + filename + ".txt"
                let loadLyrics() =
                    try 
                        if not (File.Exists(lyricsFileName)) then
                            match song.LyricsID with
                            | null -> ()
                            | _ -> 
                                let text = vk.GetLyrics song.LyricsID
                                Thread.Sleep(1000) // VK limitation of requests (this works for 1 to 4 number of threads)
                                File.WriteAllText(lyricsFileName, text.Replace("\n", "\r\n"))
                            
                                let file = TagLib.File.Create(audioFileName)
                                try
                                    file.Tag.Lyrics <- text // UNSYNCEDLYRICS
                                    file.Save()
                                with
                                    | _ -> file.Save(); File.Delete(lyricsFileName)
                        uncheck song
                    with
                        | _ -> if (File.Exists(lyricsFileName)) then File.Delete(lyricsFileName)  
                let downloadCompleted (args:DownloadDataCompletedEventArgs) =
                    try
                        File.WriteAllBytes(audioFileName, args.Result)
                        // Working with ID3 tags
                        let file = TagLib.File.Create(audioFileName)
                        try
                            file.Tag.Title <- song.Title
                            file.Tag.Track <- Convert.ToUInt32(0)
                            file.Tag.Performers <- null
                            file.Tag.Performers <- [|song.Artist|]
                            if album then file.Tag.Album <- null
                            file.Save()

                            if lyrics then loadLyrics() else uncheck song                 
                        with
                            | _ -> file.Save(); File.Delete(audioFileName)
                    with
                        | _ -> if (File.Exists(audioFileName)) then File.Delete(audioFileName)
                    signal.Release() |> ignore
                web.DownloadDataCompleted.Add(downloadCompleted)
                if not (File.Exists(audioFileName)) then
                    web.DownloadDataAsync(new Uri(song.Url))
                    async.Bind(signal.WaitAsync()  |> Helper.awaitTask, (fun _ -> async.Zero()))
                elif lyrics then loadLyrics(); async.Zero() else async.Zero()

            async {
                let throttler = new SemaphoreSlim(4)

                for elem in list do
                    do! throttler.WaitAsync() |> Helper.awaitTask

                    async { 
                        do! processSong elem
                        throttler.Release() |> ignore
                        progressChange()
                    } |> Async.Start
            } |> Async.Start
            
        new RelayCommand ((fun _ -> true), (fun _ -> 
            let songs =
                this.SongList
                |> List.filter (fun (a:CheckableItem<Song>) -> a.IsChecked)
                |> List.map (fun (a:CheckableItem<Song>) -> a.Value)
            this.Amount <- songs.Length
            match this.Amount with
            | 0 -> ()
            | _ ->
                if lyrics && not (Directory.Exists(folder + "\\Lyrics")) then
                    Directory.CreateDirectory(folder + "\\Lyrics") |> ignore
                loadStarted.Trigger()
                downloadQueueAsync songs))

    interface IDataErrorInfo with
        member this.Error =  raise (new NotImplementedException())
        member this.Item 
            with get propertyName = 
                match propertyName with
                | "Format" ->
                    if not (Regex.IsMatch(this.Format, "^[^\\\./:\*\?\"<>\|]{1}[^\\/:\*\?\"<>\|]{0,254}$")) 
                    then "Wrong format!"
                    else null
                | _ -> null
