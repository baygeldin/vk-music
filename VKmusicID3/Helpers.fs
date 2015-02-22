namespace ViewHelpers

open System
open System.Windows
open System.Windows.Data
open System.Windows.Media
open System.Windows.Forms.Integration
open ViewModels
open VkAudio

type BooleanInverter () =
    interface IValueConverter with
        member this.Convert (value, _, _, _) =
            not (value :?> bool) :> obj
        member this.ConvertBack(value, _, _, _) =
            not (value :?> bool) :> obj

type SongConverter() =
    interface IMultiValueConverter with
        member this.Convert (values, _, _, _) =
            (values.[0].ToString()) + " - " + (values.[1].ToString()) :> obj
        member this.ConvertBack(_, _, _, _) =
            raise (new NotImplementedException())

type ProgressConverter() =
    interface IMultiValueConverter with
        member this.Convert (values, _, _, _) =
            match values.[0].ToString(), values.[1].ToString() with
            | a, b when a = b -> null
            | a, b -> a + "/" + b
            :> obj
        member this.ConvertBack(_, _, _, _) =
            raise (new NotImplementedException())

type CheckedAmountConverter () =
    interface IValueConverter with
        member this.Convert (value, _, _, _) =
            List.fold 
            <| (fun acc (a:CheckableItem<Song>) -> acc + if a.IsChecked then 1 else 0) 
            <| 0 <| (value :?> List<CheckableItem<Song>>) :> obj
        member this.ConvertBack(value, _, _, _) =
            raise (new NotImplementedException())

type ForegroundConverter () =
    interface IValueConverter with
        member this.Convert (value, _, _, _) =
            (if value = null then Brushes.Gray else Brushes.Black) :> obj
        member this.ConvertBack(value, _, _, _) =
            raise (new NotImplementedException())

type IsFailedConverter () =
    abstract Convert : obj -> bool
    default this.Convert value =
        List.exists (fun (a:CheckableItem<Song>) -> a.IsChecked) (value :?> List<CheckableItem<Song>>)

    interface IValueConverter with
        member this.Convert (value, _, _, _) =
            (if this.Convert value then Visibility.Visible else Visibility.Collapsed) :> obj
        member this.ConvertBack(value, _, _, _) =
            raise (new NotImplementedException())

type IsSuccessConverter () =
    inherit IsFailedConverter()

    override this.Convert value = not <| base.Convert value

type WebBrowserHelper() =
    static let sourceProperty =
        DependencyProperty.RegisterAttached("Source", typeof<string>, typeof<WebBrowserHelper>, new PropertyMetadata(WebBrowserHelper.SourcePropertyChanged))

    static member GetSource (a:DependencyObject) = a.GetValue(sourceProperty) :?> string
    
    static member SetSource (a:DependencyObject) (value:string) = a.SetValue(sourceProperty, value)
    
    static member SourcePropertyChanged (a:DependencyObject) (e:DependencyPropertyChangedEventArgs) =
        let host = a :?> WindowsFormsHost
        match host with
        | null -> failwith "Not a WindowsFormsHost"
        | _ -> 
            let url = e.NewValue :?> string
            (host.Child :?> Forms.WebBrowser).Navigate(url)

type WindowHelper() =
    static let dialogResultProperty =
        DependencyProperty.RegisterAttached("DialogResult", typeof<bool>, typeof<WindowHelper>, new PropertyMetadata(WindowHelper.DialogResultChanged))
    
    static member GetDialogResult (a:DependencyObject) = a.GetValue(dialogResultProperty) :?> bool
    
    static member SetDialogResult (a:DependencyObject) (value:string) = a.SetValue(dialogResultProperty, value)

    static member DialogResultChanged (a:DependencyObject) (e:DependencyPropertyChangedEventArgs) =
        let window = a :?> Window
        match window with
        | null -> failwith "Not a Window"
        | _ -> window.DialogResult <- System.Nullable(e.NewValue :?> bool)