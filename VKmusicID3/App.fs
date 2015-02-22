(*
    A. Baygeldin (c) 2014
    Download music from your VK account with correct ID3 tags
*)

module Main

open System
open System.Windows
open System.Windows.Media
open System.Windows.Input
open System.ComponentModel
open VkAudio
open FSharpx
open ViewModels

let vk = new VkAudio(AppID = "4494604") //Your App ID

let helpWindow() =
    let window = new XAML<"Help.xaml">()
    window.Repository.RequestNavigate.Add(fun e -> System.Diagnostics.Process.Start(e.Uri.ToString())  |> ignore)
    window.Root

let resultWindow(viewModel) =
    let window = new XAML<"Result.xaml">()
    window.Root.DataContext <- viewModel
    window.Root.Owner <- Application.Current.MainWindow
    window.Close.Click.Add(fun _ -> window.Root.Close())
    window.Reload.Click.Add(fun _ -> window.Root.Close())
    window.Root

let authWindow() = 
    let window = new XAML<"Auth.xaml">()
    let viewModel = new ViewModels.AuthViewModel(vk) 
    window.Root.MouseLeftButtonDown.Add(fun _ -> window.Root.DragMove())
    (window.Browser :?> Forms.WebBrowser).DocumentCompleted.Add(fun e ->
        (viewModel.TryAuth :> ICommand).Execute(e.Url)
        (window.Browser :?> Forms.WebBrowser).Document.Body.Style <- "overflow:hidden")
    viewModel.OnClose.Add(window.Root.Close)
    window.Root.Owner <- Application.Current.MainWindow
    window.Root.DataContext <- viewModel
    window.Root

let mainWindow() =
    let window = new XAML<"Main.xaml">()
    let viewModel = new ViewModels.MainViewModel(vk) 
    window.Generate.Click.Add(fun _ -> 
        window.Generate.IsEnabled <- false)
    window.Scroll.PreviewMouseWheel.Add(fun e ->
        window.Scroll.ScrollToVerticalOffset(window.Scroll.VerticalOffset - float e.Delta))
    viewModel.LoadStarted.Add(fun _ -> 
        window.Wrap.IsEnabled <- false)
    viewModel.SongsLoaded.Add(fun _ ->
        window.Info.Visibility <- Visibility.Collapsed
        window.List.Visibility <- Visibility.Visible
        window.ListWrap.IsEnabled <- true
        window.Generate.IsEnabled <- true)
    viewModel.LoadCompleted.Add(fun _ ->
        window.Wrap.IsEnabled <- true
        resultWindow(viewModel).ShowDialog() |> ignore)
    viewModel.HelpRequested.Add(fun _ -> helpWindow().ShowDialog() |> ignore)
    viewModel.AuthRequested.Add(fun _ -> 
        if authWindow().ShowDialog().Value then window.Root.Close() else window.Root.IsEnabled <- true)
    viewModel.ShutdownRequested.Add(fun _ -> window.Root.Close())
    window.Root.DataContext <- viewModel
    window.Root

let com = new System.Uri("/VKmusicID3;component/App.xaml", System.UriKind.Relative)
let app = (System.Windows.Application.LoadComponent(com) :?> Application)

let errorShow (args:Threading.DispatcherUnhandledExceptionEventArgs) = 
    let window = new XAML<"Error.xaml">()
    window.Root.Owner <- Application.Current.MainWindow
    window.Error.Text <- args.Exception.Message
    window.Exit.Click.Add(fun _ -> window.Root.Close())
    window.Root.Closing.Add(fun _ -> app.Shutdown())
    window.Root.ShowDialog() |> ignore
    args.Handled <- true

app.DispatcherUnhandledException.Add(errorShow)

[<EntryPoint; STAThread>]
let main args = app.Run(mainWindow())
