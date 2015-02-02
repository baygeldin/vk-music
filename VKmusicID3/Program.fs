(*
    A. Baygeldin (c) 2014
    Download music from your VK account with correct ID3 tags
*)

module Main

open System
open System.Windows.Forms
open System.Windows.Forms.Integration
open System.Windows
open System.Windows.Media
open System.Net
open System.IO
open System.Text.RegularExpressions
open VkAudio
open FSharpx
open TagLib

let vk = new VkAudio(AppID = "4494604") //Your App ID

let clearCookie (browser:Forms.WebBrowser) =
    "javascript:void((function(){var a,b,c,e,f;f=0;a=document.cookie.split('; ');"
    + "for(e=0;e<a.length&&a[e];e++){f++;for(b='.'+location.host;b;b=b.replace(/^(?:%5C.|[^%5C.]+)/,''))"
    + "{for(c=location.pathname;c;c=c.replace(/.$/,'')){document.cookie=(a[e]+'; domain='+b+'; "
    + "path='+c+'; expires='+new Date((new Date()).getTime()-1e11).toGMTString());}}}})())"
    |> browser.Navigate

let mainThread func = Application.Current.Dispatcher.Invoke(new Action<_>(func), [null]) |> ignore

let errorShow (msg:exn) = 
    (fun _ ->
        MessageBox.Show ("Something went wrong! Check your internet connection. Full error: "
        + msg.InnerException.ToString()) |> ignore)
    |> mainThread

let authWindow() = 
    let window = new XAML<"Browser.xaml">()
    let tryAuth() =
        let fragment = (window.Browser :?> Forms.WebBrowser).Url.Fragment
        if fragment.Length > 0 then
            vk.SetAuth fragment
    let loadCompleted() =
        //clearCookie (window.Browser :?> Forms.WebBrowser)
        (window.Browser :?> Forms.WebBrowser).Document.Body.Style <- "overflow:hidden"
        tryAuth()
    window.Root.MouseLeftButtonDown.Add(fun _ -> window.Root.DragMove())
    window.Link.RequestNavigate.Add(fun e -> System.Diagnostics.Process.Start(e.Uri.ToString()) |> ignore)
    window.CloseButton.PreviewMouseDown.Add(fun _ -> 
        window.Root.DialogResult <- System.Nullable(true)
        window.Root.Close())
    (window.Browser :?> Forms.WebBrowser).DocumentCompleted.Add(fun _ -> loadCompleted())
    vk.AuthSetted.Add(fun _ -> window.Root.Close())
    vk.GetLink |> (window.Browser :?> Forms.WebBrowser).Navigate
    (window.Browser :?> Forms.WebBrowser).ScriptErrorsSuppressed <- true
    window.Root.Owner <- Application.Current.MainWindow
    window.Root

let mainWindow() =
    let window = new XAML<"Layout.xaml">()
    let helpWindow = new XAML<"Help.xaml">()
    let folderDialog = new FolderBrowserDialog()
    let selectFile() =
        if folderDialog.ShowDialog() = DialogResult.OK then 
            window.Folder.Text <- folderDialog.SelectedPath
        window.Folder.Foreground <- Brushes.Black
        window.Download.IsEnabled <- true
    let generateList() =
        window.Generate.IsEnabled <- false
        async {
        try
            let list = vk.GetSongs()
            (fun _ ->
                window.List.Children.Remove(window.Info)
                window.List.IsEnabled <- true
                for song in list do
                    Controls.CheckBox(
                        IsChecked=System.Nullable(true), 
                        Name="id" + song.id, 
                        Content=(song.artist + " - " + song.title))
                    |> window.List.Children.Add |> ignore
                window.Generate.IsEnabled <- true)
            |> mainThread
        with
            | msg -> errorShow msg
        } |> Async.Start
    let invertSelecting() =
        if not window.Info.IsLoaded then
            for song in window.List.Children do
                if (song :?> Controls.CheckBox).IsChecked.Value then
                   (song :?> Controls.CheckBox).IsChecked  <- System.Nullable(false)
                else (song :?> Controls.CheckBox).IsChecked  <- System.Nullable(true)
    let downloadSongs() =
        let lockObject = new Object()
        let folder = window.Folder.Text
        let format = window.Format.Text
        let initial = Int32.Parse(window.Number.Text)
        let album = window.Album.IsChecked.Value
        let lyrics = window.Lyrics.IsChecked.Value
        let buffer = window.Download.Content
        let progressChange() =
            (fun _ ->
                let parts = window.Download.Content.ToString().Split('/')
                if (Array.length parts) > 1 then
                    let count = (Int32.Parse(parts.[0]) + 1).ToString()
                    if count = parts.[1] then
                        window.Download.Content <- buffer
                        window.Wrap.IsEnabled <- true
                    else
                        window.Download.Content <- count + "/" + parts.[1])
            |> mainThread
        let rec downloadQueueAsync (list:(Song * int) list) (amount:int) =
            let web = new WebClient()
            let processSong (song:Song * int) = 
                let filename =
                    format
                        .Replace("ARTIST", (fst song).artist)
                        .Replace("SONG", (fst song).title)
                        .Replace("NUMX", (initial + (snd song)).ToString()
                            .PadLeft(amount.ToString().Length, '0'))
                        .Replace("NUM", (initial + (snd song)).ToString())
                    |> (fun x -> Regex.Replace(x, "[\\/?:*\"\"><|]+", ""))
                let downloadCompleted (args:DownloadDataCompletedEventArgs) =
                    try
                        File.WriteAllBytes(folder + "\\" + filename + ".mp3", args.Result)
                        //Working with ID3 tags
                        let file = TagLib.File.Create(folder + "\\" + filename + ".mp3")
                        file.Tag.Title <- (fst song).title
                        file.Tag.Track <- Convert.ToUInt32(0)
                        file.Tag.Performers <- null
                        file.Tag.Performers <- [|(fst song).artist|]
                        if album then file.Tag.Album <- null
                        try
                            if lyrics then
                                let text = if (fst song).lyrics_id = "" then "" else (vk.GetLyrics (fst song).lyrics_id)
                                File.WriteAllText(folder + "\\Lyrics\\" + filename + ".txt", text.Replace("\n", "\r\n"))
                                file.Tag.Lyrics <- text //UNSYNCEDLYRICS
                            file.Save()
                            (fun _ -> 
                                LogicalTreeHelper.FindLogicalNode(window.List, "id" + (fst song).id) :?> UIElement
                                |> window.List.Children.Remove)
                            |> mainThread
                            progressChange()
                        with
                        | _ -> 
                            (fun _ -> 
                                (LogicalTreeHelper
                                    .FindLogicalNode(window.List, "id" + (fst song).id) :?> Controls.CheckBox)
                                    .Foreground <- Brushes.Orange)
                            |> mainThread
                    with
                        | _ ->
                            (fun _ -> 
                                (LogicalTreeHelper
                                    .FindLogicalNode(window.List, "id" + (fst song).id) :?> Controls.CheckBox)
                                    .Foreground <- Brushes.Red)
                            |> mainThread
                web.DownloadDataCompleted.Add(downloadCompleted)
                if not (File.Exists(folder + "\\" + filename + ".mp3")) then
                    web.DownloadDataAsync(new Uri((fst song).url))
            let lockFun() =
                match list with
                | hd::tl ->
                    processSong hd
                    downloadQueueAsync tl amount
                | [] -> ()
            lock lockObject lockFun |> ignore
        let checkedSongs (list:string list) (songs:Song list) =
            let rec checkedList' (list:string list) (songs:Song list) (acc:Song list) =
                match songs with
                | hd::tl -> 
                    if (Seq.exists ((=) hd.id) list) then
                        checkedList' list tl (hd::acc)
                    else
                        checkedList' list tl acc
                | [] -> acc
            checkedList' list songs []
        let checkedIDList (list:Controls.CheckBox list) =
            let rec checkedList' (list:Controls.CheckBox list) (acc:string list) =
                match list with
                | hd::tl -> 
                    if hd.IsChecked.Value then
                        checkedList' tl (hd.Name.Substring(2)::acc)
                    else
                        checkedList' tl acc
                | [] -> acc
            checkedList' list []
        let getCheckboxList (controls:Controls.UIElementCollection) =
            let enum = controls.GetEnumerator()
            let rec getCheckboxList' (acc:Controls.CheckBox list) =
                if (enum.MoveNext()) then
                    getCheckboxList' ((enum.Current :?> Controls.CheckBox)::acc)
                else acc
            getCheckboxList' []
        window.Wrap.IsEnabled <- false
        let songs =
            //vk.GetSongs(checkedIDList (getCheckboxList window.List.Children))
            checkedSongs (checkedIDList (getCheckboxList window.List.Children)) (vk.GetSongs())
            |> Seq.fold (fun acc song -> (song, acc.Length + 1)::acc) []
        window.Download.Content <- "0/" + songs.Length.ToString()
        if not (Directory.Exists(folder + "\\Lyrics")) then
            Directory.CreateDirectory(folder + "\\Lyrics") |> ignore
        for thread = 1 to Math.Min(10, songs.Length) do
            Async.Start(async { downloadQueueAsync songs songs.Length })
    window.SelectFolder.Click.Add(fun _ -> selectFile())
    window.Generate.Click.Add(fun _ -> generateList())
    window.Invert.Click.Add(fun _ -> invertSelecting())
    window.Help.Click.Add(fun _ -> helpWindow.Root.ShowDialog() |> ignore)
    window.Number.PreviewTextInput.Add(fun e -> 
        if not (Regex.IsMatch(e.Text, "^[0-9]+$")) then e.Handled <- true)
    window.Format.PreviewTextInput.Add(fun e -> 
        if not (Regex.IsMatch(e.Text, "^[^\\\./:\*\?\"<>\|]{1}[^\\/:\*\?\"<>\|]{0,254}$")) then e.Handled <- true)
    window.Root.ContentRendered.Add(fun _ -> if authWindow().ShowDialog().Value then window.Root.Close())
    vk.AuthSetted.Add(fun _ -> window.Root.Title <- window.Root.Title + " (" + vk.GetName + ")")
    window.Download.Click.Add(fun _ -> downloadSongs())
    window.Download.IsEnabled <- false
    window.Root

let com = new System.Uri("/VKmusicID3;component/App.xaml", System.UriKind.Relative)
let app = (System.Windows.Application.LoadComponent(com) :?> Application)
app.DispatcherUnhandledException.Add(fun args -> errorShow args.Exception)

[<STAThread>]
app.Run(mainWindow()) |> ignore